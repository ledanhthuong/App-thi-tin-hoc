using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using App_thi_tin_hoc.Data;
using App_thi_tin_hoc.Models;
using App_thi_tin_hoc.Helpers;

namespace App_thi_tin_hoc.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Student Dashboard
        public async Task<IActionResult> Index()
        {
            // Fetch active and upcoming contests
            var contests = await _context.Contests
                .OrderByDescending(c => c.StartTime)
                .ToListAsync();

            // Fetch top 10 students for leaderboard
            var leaderboard = await _context.Accounts
                .Where(a => a.Role == "Student")
                .OrderByDescending(a => a.Points)
                .Take(10)
                .ToListAsync();

            ViewBag.Contests = contests;
            ViewBag.Leaderboard = leaderboard;

            // Get logged in student details if authenticated
            if (User.Identity?.IsAuthenticated == true)
            {
                var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (int.TryParse(userIdStr, out int userId))
                {
                    var student = await _context.Accounts.FindAsync(userId);
                    ViewBag.CurrentStudent = student;
                }
            }

            ViewBag.SchoolInfo = GetSchoolInfo();
            return View();
        }

        // Login GET
        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index");
            }
            return View();
        }

        // Login POST
        [HttpPost]
        public async Task<IActionResult> Login(string username, string password)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ModelState.AddModelError("", "Vui lòng nhập đầy đủ tên đăng nhập và mật khẩu!");
                return View();
            }

            var account = await _context.Accounts.FirstOrDefaultAsync(a => a.Username == username);
            if (account == null || !PasswordHelper.VerifyPassword(password, account.PasswordHash))
            {
                ModelState.AddModelError("", "Tên đăng nhập hoặc mật khẩu không chính xác!");
                return View();
            }

            // Authentication Cookie Setup
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, account.Username),
                new Claim(ClaimTypes.NameIdentifier, account.Id.ToString()),
                new Claim(ClaimTypes.Role, account.Role),
                new Claim("IsSuperAdmin", account.IsSuperAdmin.ToString()),
                new Claim("FullName", account.FullName),
                new Claim("Points", account.Points.ToString()),
                new Claim("RankTitle", account.RankTitle),
                new Claim("AvatarUrl", account.AvatarUrl)
            };

            var claimsIdentity = new ClaimsIdentity(claims, "CookieAuth");
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(2)
            };

            await HttpContext.SignInAsync("CookieAuth", new ClaimsPrincipal(claimsIdentity), authProperties);

            // Redirect based on role
            if (account.Role == "Teacher")
            {
                return RedirectToAction("Index", "Admin");
            }

            return RedirectToAction("Index");
        }

        // Register GET
        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index");
            }
            return View();
        }

        // Register POST
        [HttpPost]
        public async Task<IActionResult> Register(string username, string fullName, string password, string confirmPassword)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(fullName) || string.IsNullOrEmpty(password))
            {
                ModelState.AddModelError("", "Vui lòng nhập đầy đủ thông tin!");
                return View();
            }

            if (password != confirmPassword)
            {
                ModelState.AddModelError("", "Mật khẩu nhập lại không khớp!");
                return View();
            }

            var existing = await _context.Accounts.AnyAsync(a => a.Username == username);
            if (existing)
            {
                ModelState.AddModelError("", "Tên đăng nhập đã được sử dụng!");
                return View();
            }

            // Random kids avatar
            var random = new Random();
            int avatarNum = random.Next(1, 4); // avatar1, avatar2, avatar3
            var avatarUrl = $"/images/avatars/avatar{avatarNum}.svg";

            var account = new Account
            {
                Username = username,
                FullName = fullName,
                PasswordHash = PasswordHelper.HashPassword(password),
                Role = "Student",
                Points = 0,
                RankTitle = "Tập sự 🌟",
                AvatarUrl = avatarUrl
            };

            _context.Accounts.Add(account);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đăng ký tài khoản thành công! Hãy đăng nhập nhé.";
            return RedirectToAction("Login");
        }

        // Logout
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync("CookieAuth");
            return RedirectToAction("Index");
        }

        // Access Denied
        public IActionResult AccessDenied()
        {
            return View();
        }

        // Student Self-Edit Profile (Name and Avatar)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(string fullName, string? selectedAvatar, IFormFile? avatarFile)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return Challenge();
            }

            if (string.IsNullOrWhiteSpace(fullName))
            {
                TempData["ErrorMessage"] = "Họ và tên không được để trống!";
                return RedirectToAction("Index");
            }

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(userIdStr, out int userId))
            {
                var student = await _context.Accounts.FindAsync(userId);
                if (student != null)
                {
                    student.FullName = fullName.Trim();

                    // 1. Process custom avatar upload
                    if (avatarFile != null && avatarFile.Length > 0)
                    {
                        var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/avatars");
                        if (!Directory.Exists(uploadsDir))
                        {
                            Directory.CreateDirectory(uploadsDir);
                        }

                        var ext = Path.GetExtension(avatarFile.FileName).ToLower();
                        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".svg", ".webp" };
                        if (allowedExtensions.Contains(ext))
                        {
                            var uniqueFileName = $"{Guid.NewGuid()}{ext}";
                            var filePath = Path.Combine(uploadsDir, uniqueFileName);

                            using (var stream = new FileStream(filePath, FileMode.Create))
                            {
                                await avatarFile.CopyToAsync(stream);
                            }

                            student.AvatarUrl = $"/uploads/avatars/{uniqueFileName}";
                        }
                        else
                        {
                            TempData["ErrorMessage"] = "Định dạng ảnh không hợp lệ! Chỉ chấp nhận .jpg, .jpeg, .png, .svg, .webp.";
                            return RedirectToAction("Index");
                        }
                    }
                    // 2. Or process selected default avatar
                    else if (!string.IsNullOrEmpty(selectedAvatar))
                    {
                        var allowedAvatars = new[] { "/images/avatars/avatar1.svg", "/images/avatars/avatar2.svg", "/images/avatars/avatar3.svg" };
                        if (allowedAvatars.Contains(selectedAvatar))
                        {
                            student.AvatarUrl = selectedAvatar;
                        }
                    }

                    _context.Entry(student).State = EntityState.Modified;
                    await _context.SaveChangesAsync();

                    // Re-sign in the user to update claims in the cookie
                    var claimsIdentity = (ClaimsIdentity)User.Identity;
                    
                    var fullNameClaim = claimsIdentity.FindFirst("FullName");
                    if (fullNameClaim != null) claimsIdentity.RemoveClaim(fullNameClaim);
                    claimsIdentity.AddClaim(new Claim("FullName", student.FullName));

                    var avatarUrlClaim = claimsIdentity.FindFirst("AvatarUrl");
                    if (avatarUrlClaim != null) claimsIdentity.RemoveClaim(avatarUrlClaim);
                    claimsIdentity.AddClaim(new Claim("AvatarUrl", student.AvatarUrl));

                    // Sign out and sign in back with updated claims
                    await HttpContext.SignOutAsync("CookieAuth");
                    
                    var authProperties = new AuthenticationProperties
                    {
                        IsPersistent = true,
                        ExpiresUtc = DateTimeOffset.UtcNow.AddHours(2)
                    };
                    await HttpContext.SignInAsync("CookieAuth", new ClaimsPrincipal(claimsIdentity), authProperties);

                    TempData["SuccessMessage"] = "Cập nhật thông tin cá nhân thành công! 🎉";
                }
            }

            return RedirectToAction("Index");
        }

        public class SchoolInfo
        {
            public string Title { get; set; } = "Về Trường Tiểu Học Ban Mai 🏫";
            public string Description { get; set; } = "Chào mừng các bé đến với không gian học tập sáng tạo tại Trường Tiểu Học Ban Mai! Chúng tôi tự hào là ngôi trường tiên phong kết hợp giáo dục tiểu học chuẩn quốc gia với chương trình tư duy logic và khoa học máy tính từ sớm, giúp các em phát triển tư duy thuật toán thông qua các bài học lập trình vui nhộn.";
            public string Address { get; set; } = "Khu đô thị Xanh, Cầu Giấy, Hà Nội";
            public string Hotline { get; set; } = "024.1234.5678";
            public string BannerUrl { get; set; } = "/images/school_banner.png";
        }

        private SchoolInfo GetSchoolInfo()
        {
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/school_info.json");
            if (!System.IO.File.Exists(filePath))
            {
                return new SchoolInfo();
            }

            try
            {
                var json = System.IO.File.ReadAllText(filePath);
                return System.Text.Json.JsonSerializer.Deserialize<SchoolInfo>(json) ?? new SchoolInfo();
            }
            catch
            {
                return new SchoolInfo();
            }
        }

        private void SaveSchoolInfo(SchoolInfo info)
        {
            var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads");
            if (!Directory.Exists(uploadsDir))
            {
                Directory.CreateDirectory(uploadsDir);
            }
            var filePath = Path.Combine(uploadsDir, "school_info.json");
            var json = System.Text.Json.JsonSerializer.Serialize(info, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            System.IO.File.WriteAllText(filePath, json);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateSchoolInfo(string title, string description, string address, string hotline, IFormFile? bannerFile)
        {
            if (User.Identity?.IsAuthenticated != true || User.Identity.Name != "superadmin")
            {
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(description))
            {
                TempData["ErrorMessage"] = "Tiêu đề và Đôi dòng giới thiệu không được để trống!";
                return RedirectToAction("Index");
            }

            var info = GetSchoolInfo();
            info.Title = title.Trim();
            info.Description = description.Trim();
            info.Address = (address ?? "").Trim();
            info.Hotline = (hotline ?? "").Trim();

            if (bannerFile != null && bannerFile.Length > 0)
            {
                var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads");
                if (!Directory.Exists(uploadsDir))
                {
                    Directory.CreateDirectory(uploadsDir);
                }

                var ext = Path.GetExtension(bannerFile.FileName).ToLower();
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".svg", ".webp" };
                if (allowedExtensions.Contains(ext))
                {
                    var uniqueFileName = $"school_banner_custom{ext}";
                    var filePath = Path.Combine(uploadsDir, uniqueFileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await bannerFile.CopyToAsync(stream);
                    }

                    var timestamp = DateTime.UtcNow.Ticks;
                    info.BannerUrl = $"/uploads/{uniqueFileName}?t={timestamp}";
                }
                else
                {
                    TempData["ErrorMessage"] = "Định dạng ảnh banner không hợp lệ! Chỉ chấp nhận .jpg, .jpeg, .png, .svg, .webp.";
                    return RedirectToAction("Index");
                }
            }

            SaveSchoolInfo(info);
            TempData["SuccessMessage"] = "Cập nhật giới thiệu trường học thành công! 🎉";
            return RedirectToAction("Index");
        }
    }
}
