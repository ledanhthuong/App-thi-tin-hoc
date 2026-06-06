using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using App_thi_tin_hoc.Data;
using App_thi_tin_hoc.Models;
using App_thi_tin_hoc.Helpers;
using App_thi_tin_hoc.Services;

namespace App_thi_tin_hoc.Controllers
{
    [Authorize(Roles = "Teacher")] // Only teachers can access admin actions
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        private async Task<bool> CheckIsSuperAdminAsync()
        {
            var currentUsername = User.Identity?.Name;
            if (currentUsername == "superadmin") return true;
            var account = await _context.Accounts.FirstOrDefaultAsync(a => a.Username == currentUsername);
            return account?.IsSuperAdmin == true;
        }

        private int? GetCurrentUserId()
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdStr, out int id) ? id : null;
        }

        // Admin Dashboard
        public async Task<IActionResult> Index()
        {
            var studentCount = await _context.Accounts.CountAsync(a => a.Role == "Student");
            var teacherCount = await _context.Accounts.CountAsync(a => a.Role == "Teacher" && a.Username != "superadmin");
            var contestCount = await _context.Contests.CountAsync();
            var problemCount = await _context.Problems.CountAsync();
            var submissionCount = await _context.Submissions.CountAsync();

            var recentSubmissions = await _context.Submissions
                .Include(s => s.Problem)
                .Include(s => s.Account)
                .OrderByDescending(s => s.SubmittedAt)
                .Take(15)
                .ToListAsync();

            ViewBag.StudentCount = studentCount;
            ViewBag.TeacherCount = teacherCount;
            ViewBag.ContestCount = contestCount;
            ViewBag.ProblemCount = problemCount;
            ViewBag.SubmissionCount = submissionCount;

            return View(recentSubmissions);
        }

        // Contest Management
        public async Task<IActionResult> Contests()
        {
            if (!await CheckIsSuperAdminAsync()) return Forbid();
            var contests = await _context.Contests
                .Include(c => c.Problems)
                .OrderByDescending(c => c.StartTime)
                .ToListAsync();
            return View(contests);
        }

        [HttpGet]
        public async Task<IActionResult> CreateContest()
        {
            if (!await CheckIsSuperAdminAsync()) return Forbid();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateContest(Contest contest)
        {
            if (!await CheckIsSuperAdminAsync()) return Forbid();
            if (ModelState.IsValid)
            {
                _context.Contests.Add(contest);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Tạo kỳ thi mới thành công!";
                return RedirectToAction(nameof(Contests));
            }
            return View(contest);
        }

        [HttpGet]
        public async Task<IActionResult> EditContest(int id)
        {
            if (!await CheckIsSuperAdminAsync()) return Forbid();
            var contest = await _context.Contests.FindAsync(id);
            if (contest == null)
            {
                return NotFound();
            }
            return View(contest);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditContest(Contest contest)
        {
            if (!await CheckIsSuperAdminAsync()) return Forbid();
            if (ModelState.IsValid)
            {
                var existing = await _context.Contests.FindAsync(contest.Id);
                if (existing == null)
                {
                    return NotFound();
                }

                existing.Title = contest.Title;
                existing.Description = contest.Description;
                existing.StartTime = contest.StartTime;
                existing.EndTime = contest.EndTime;
                existing.ShowCodeHints = contest.ShowCodeHints;

                _context.Entry(existing).State = EntityState.Modified;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Cập nhật kỳ thi thành công!";
                return RedirectToAction(nameof(Contests));
            }
            return View(contest);
        }

        public async Task<IActionResult> DeleteContest(int id)
        {
            if (!await CheckIsSuperAdminAsync()) return Forbid();
            var contest = await _context.Contests.FindAsync(id);
            if (contest != null)
            {
                _context.Contests.Remove(contest);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Xóa kỳ thi thành công!";
            }
            return RedirectToAction(nameof(Contests));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartContest(int contestId, string? customSessionName)
        {
            if (!await CheckIsSuperAdminAsync()) return Forbid();
            var contest = await _context.Contests.FindAsync(contestId);
            if (contest == null)
            {
                return NotFound();
            }

            string sessionName = !string.IsNullOrWhiteSpace(customSessionName)
                ? customSessionName.Trim()
                : "Đợt thi ngày " + DateTime.UtcNow.AddHours(7).ToString("dd/MM/yyyy HH:mm");

            contest.CurrentSession = sessionName;
            contest.StartTime = DateTime.UtcNow;
            contest.EndTime = DateTime.UtcNow.AddDays(7);

            _context.Entry(contest).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã khởi động thành công đợt thi mới: \"{sessionName}\"!";
            return RedirectToAction(nameof(Contests));
        }

        // Problem Management
        public async Task<IActionResult> Problems(int contestId)
        {
            if (!await CheckIsSuperAdminAsync()) return Forbid();
            var contest = await _context.Contests
                .Include(c => c.Problems)
                .FirstOrDefaultAsync(c => c.Id == contestId);

            if (contest == null)
            {
                return NotFound();
            }

            return View(contest);
        }

        [HttpGet]
        public async Task<IActionResult> CreateProblem(int contestId)
        {
            if (!await CheckIsSuperAdminAsync()) return Forbid();
            ViewBag.ContestId = contestId;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateProblem(Problem problem, IFormFile? imageFile)
        {
            if (!await CheckIsSuperAdminAsync()) return Forbid();
            if (imageFile != null && imageFile.Length > 0)
            {
                var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/problems");
                if (!Directory.Exists(uploadsDir))
                {
                    Directory.CreateDirectory(uploadsDir);
                }

                var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(imageFile.FileName)}";
                var filePath = Path.Combine(uploadsDir, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(stream);
                }

                problem.ImageUrl = $"/uploads/problems/{uniqueFileName}";
            }

            // Re-evaluate model state after assigning ImageUrl if necessary, or just clear any errors related to ImageUrl/imageFile
            ModelState.Remove("imageFile");

            if (ModelState.IsValid)
            {
                _context.Problems.Add(problem);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Thêm đề bài mới thành công!";
                return RedirectToAction(nameof(Problems), new { contestId = problem.ContestId });
            }
            ViewBag.ContestId = problem.ContestId;
            return View(problem);
        }

        public async Task<IActionResult> DeleteProblem(int id)
        {
            if (!await CheckIsSuperAdminAsync()) return Forbid();
            var problem = await _context.Problems.FindAsync(id);
            int contestId = 0;
            if (problem != null)
            {
                contestId = problem.ContestId;
                _context.Problems.Remove(problem);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Xóa đề bài thành công!";
            }
            return RedirectToAction(nameof(Problems), new { contestId = contestId });
        }

        // Testcase Management
        public async Task<IActionResult> Testcases(int problemId)
        {
            if (!await CheckIsSuperAdminAsync()) return Forbid();
            var problem = await _context.Problems
                .Include(p => p.Testcases)
                .FirstOrDefaultAsync(p => p.Id == problemId);

            if (problem == null)
            {
                return NotFound();
            }

            return View(problem);
        }

        [HttpGet]
        public async Task<IActionResult> CreateTestcase(int problemId)
        {
            if (!await CheckIsSuperAdminAsync()) return Forbid();
            ViewBag.ProblemId = problemId;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTestcase(Testcase testcase)
        {
            if (!await CheckIsSuperAdminAsync()) return Forbid();
            if (ModelState.IsValid)
            {
                // Normalize newlines in expected outputs
                testcase.ExpectedOutput = testcase.ExpectedOutput.Replace("\r\n", "\n").Trim();
                if (!string.IsNullOrEmpty(testcase.InputData))
                {
                    testcase.InputData = testcase.InputData.Replace("\r\n", "\n");
                }

                _context.Testcases.Add(testcase);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Thêm testcase thành công!";
                return RedirectToAction(nameof(Testcases), new { problemId = testcase.ProblemId });
            }
            ViewBag.ProblemId = testcase.ProblemId;
            return View(testcase);
        }

        public async Task<IActionResult> DeleteTestcase(int id)
        {
            if (!await CheckIsSuperAdminAsync()) return Forbid();
            var tc = await _context.Testcases.FindAsync(id);
            int problemId = 0;
            if (tc != null)
            {
                problemId = tc.ProblemId;
                _context.Testcases.Remove(tc);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Xóa testcase thành công!";
            }
            return RedirectToAction(nameof(Testcases), new { problemId = problemId });
        }

        // Manual Grading for Scratch Link/File
        [HttpGet]
        public async Task<IActionResult> GradeScratch(int submissionId)
        {
            var submission = await _context.Submissions
                .Include(s => s.Problem)
                .Include(s => s.Account)
                .FirstOrDefaultAsync(s => s.Id == submissionId);

            if (submission == null)
            {
                return NotFound();
            }

            return View(submission);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GradeScratch(int submissionId, int score, string status, string feedback)
        {
            var submission = await _context.Submissions
                .Include(s => s.Problem)
                .Include(s => s.Account)
                .FirstOrDefaultAsync(s => s.Id == submissionId);

            if (submission == null || submission.Problem == null)
            {
                return NotFound();
            }

            if (score < 0 || score > submission.Problem!.Points)
            {
                ModelState.AddModelError("", $"Điểm chấm phải nằm trong khoảng từ 0 đến {submission.Problem.Points}!");
                return View(submission);
            }

            // Get historical max score before this evaluation
            var maxPastScore = await _context.Submissions
                .Where(s => s.AccountId == submission.AccountId && 
                            s.ProblemId == submission.ProblemId && 
                            s.Id != submission.Id &&
                            (s.Status == "Accepted" || s.Status == "Wrong Answer" || s.Status == "Submitted"))
                .Select(s => s.Score)
                .DefaultIfEmpty(0)
                .MaxAsync();

            submission.Score = score;
            submission.Status = status; // Accepted or Wrong Answer
            submission.Feedback = feedback;

            _context.Entry(submission).State = EntityState.Modified;

            // Apply points changes to student
            if (score > maxPastScore)
            {
                var account = await _context.Accounts.FindAsync(submission.AccountId);
                if (account != null)
                {
                    account.Points += (score - maxPastScore);
                    account.RankTitle = JudgingService.GetRankTitle(account.Points);
                    _context.Entry(account).State = EntityState.Modified;
                }
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Chấm bài Scratch thủ công thành công!";
            return RedirectToAction(nameof(Index));
        }

        // Bulk Account Generation for Class
        [HttpGet]
        public IActionResult GenerateAccounts()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateAccounts(string className, int quantity)
        {
            if (string.IsNullOrWhiteSpace(className) || quantity <= 0)
            {
                ModelState.AddModelError("", "Vui lòng nhập tên lớp và số lượng học sinh lớn hơn 0!");
                return View();
            }

            // Normalize class name to lowercase, alphanumeric only for username creation
            var baseUsername = new string(className.ToLower().Where(char.IsLetterOrDigit).ToArray());
            var createdAccounts = new List<Account>();
            var random = new Random();

            for (int i = 1; i <= quantity; i++)
            {
                var username = $"{baseUsername}_{i:D2}"; // e.g., lop5a_01, lop5a_02
                
                // Ensure unique
                var exists = await _context.Accounts.AnyAsync(a => a.Username == username);
                if (exists)
                {
                    continue;
                }

                // Random avatar
                var avatarUrl = $"/images/avatars/avatar{((i % 3) + 1)}.svg";
                
                // Unique easy-to-read 6-digit password
                var randomPassword = random.Next(100000, 999999).ToString();

                var newAccount = new Account
                {
                    Username = username,
                    PlainTextPassword = randomPassword,
                    PasswordHash = PasswordHelper.HashPassword(randomPassword),
                    FullName = $"Học sinh {i} - Lớp {className}",
                    Role = "Student",
                    Points = 0,
                    RankTitle = "Tập sự 🌟",
                    AvatarUrl = avatarUrl,
                    CreatedById = GetCurrentUserId()
                };

                _context.Accounts.Add(newAccount);
                createdAccounts.Add(newAccount);
            }

            await _context.SaveChangesAsync();

            ViewBag.CreatedAccounts = createdAccounts;
            TempData["SuccessMessage"] = $"Đã tạo hàng loạt {createdAccounts.Count} tài khoản học sinh thành công với mật khẩu tự động ngẫu nhiên!";
            return View();
        }

        // Student & Account Management List
        public async Task<IActionResult> Students(string? search)
        {
            var isSuperAdmin = await CheckIsSuperAdminAsync();

            // Filter to show only students
            var query = _context.Accounts.Where(a => a.Role == "Student");

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(a => a.Username.ToLower().Contains(term) || a.FullName.ToLower().Contains(term));
            }

            var accounts = await query.OrderByDescending(a => a.CreatedAt).ToListAsync();
            ViewBag.SearchTerm = search;
            ViewBag.IsSuperAdmin = isSuperAdmin;
            ViewBag.CurrentUserId = GetCurrentUserId();
            return View(accounts);
        }

        // Teacher Account Management List (Superadmin only)
        public async Task<IActionResult> Teachers(string? search)
        {
            var isSuperAdmin = await CheckIsSuperAdminAsync();
            if (!isSuperAdmin)
            {
                return Forbid();
            }

            // Show all teachers except the base superadmin account itself
            var query = _context.Accounts.Where(a => a.Role == "Teacher" && a.Username != "superadmin");

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(a => a.Username.ToLower().Contains(term) || a.FullName.ToLower().Contains(term));
            }

            var accounts = await query.OrderByDescending(a => a.CreatedAt).ToListAsync();
            ViewBag.SearchTerm = search;
            ViewBag.IsSuperAdmin = true;
            return View(accounts);
        }

        // Create Single Student
        [HttpGet]
        public IActionResult CreateStudent()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateStudent(string username, string fullName, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(fullName))
            {
                ModelState.AddModelError("", "Tên đăng nhập và Họ tên không được để trống!");
                return View();
            }

            var cleanUsername = new string(username.ToLower().Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
            if (string.IsNullOrWhiteSpace(cleanUsername))
            {
                ModelState.AddModelError("", "Tên đăng nhập không hợp lệ! Chỉ dùng chữ cái, chữ số và dấu gạch dưới.");
                return View();
            }

            var exists = await _context.Accounts.AnyAsync(a => a.Username == cleanUsername);
            if (exists)
            {
                ModelState.AddModelError("Username", "Tên đăng nhập này đã được sử dụng!");
                return View();
            }

            var generatedPassword = new Random().Next(100000, 999999).ToString();
            var finalPassword = string.IsNullOrWhiteSpace(password) ? generatedPassword : password;
            var avatarUrl = $"/images/avatars/avatar{new Random().Next(1, 4)}.svg";

            var account = new Account
            {
                Username = cleanUsername,
                PasswordHash = PasswordHelper.HashPassword(finalPassword),
                FullName = fullName.Trim(),
                Role = "Student",
                Points = 0,
                RankTitle = "Tập sự 🌟",
                AvatarUrl = avatarUrl,
                CreatedById = GetCurrentUserId(),
                CreatedAt = DateTime.UtcNow
            };

            _context.Accounts.Add(account);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã tạo tài khoản học sinh {account.Username} thành công! Mật khẩu truy cập là: {finalPassword}";
            return RedirectToAction(nameof(Students));
        }

        // Create Single Teacher (Superadmin only)
        [HttpGet]
        public IActionResult CreateTeacher()
        {
            var currentUsername = User.Identity?.Name;
            if (currentUsername != "superadmin")
            {
                return Forbid();
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTeacher(string username, string fullName, string password)
        {
            var currentUsername = User.Identity?.Name;
            if (currentUsername != "superadmin")
            {
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(fullName))
            {
                ModelState.AddModelError("", "Tên đăng nhập và Họ tên không được để trống!");
                return View();
            }

            var cleanUsername = new string(username.ToLower().Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
            if (string.IsNullOrWhiteSpace(cleanUsername))
            {
                ModelState.AddModelError("", "Tên đăng nhập không hợp lệ! Chỉ dùng chữ cái, chữ số và dấu gạch dưới.");
                return View();
            }

            var exists = await _context.Accounts.AnyAsync(a => a.Username == cleanUsername);
            if (exists)
            {
                ModelState.AddModelError("Username", "Tên đăng nhập này đã được sử dụng!");
                return View();
            }

            var generatedPassword = new Random().Next(100000, 999999).ToString();
            var finalPassword = string.IsNullOrWhiteSpace(password) ? generatedPassword : password;
            var avatarUrl = $"/images/avatars/avatar{new Random().Next(1, 4)}.svg";

            var account = new Account
            {
                Username = cleanUsername,
                PasswordHash = PasswordHelper.HashPassword(finalPassword),
                FullName = fullName.Trim(),
                Role = "Teacher",
                Points = 0,
                RankTitle = "Đại sư Lập trình 🧙‍♂️",
                AvatarUrl = avatarUrl,
                CreatedAt = DateTime.UtcNow
            };

            _context.Accounts.Add(account);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã tạo tài khoản giáo viên {account.Username} thành công! Mật khẩu truy cập là: {finalPassword}";
            return RedirectToAction(nameof(Students));
        }

        // Edit Student/Account Detail
        [HttpGet]
        public async Task<IActionResult> EditStudent(int id)
        {
            var currentUserIsSuper = await CheckIsSuperAdminAsync();

            var account = currentUserIsSuper
                ? await _context.Accounts.FirstOrDefaultAsync(a => a.Id == id && a.Username != "superadmin")
                : await _context.Accounts.FirstOrDefaultAsync(a => a.Id == id && a.Role == "Student");

            if (account == null)
            {
                return NotFound();
            }

            if (!currentUserIsSuper)
            {
                var currentUserId = GetCurrentUserId();
                if (account.CreatedById != currentUserId)
                {
                    return Forbid();
                }
            }

            ViewBag.IsSuperAdmin = currentUserIsSuper;
            return View(account);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditStudent(int id, string username, string fullName, int points, string? newPassword, string? role, bool isSuperAdmin = false)
        {
            var currentUserIsSuper = await CheckIsSuperAdminAsync();

            var account = currentUserIsSuper
                ? await _context.Accounts.FirstOrDefaultAsync(a => a.Id == id && a.Username != "superadmin")
                : await _context.Accounts.FirstOrDefaultAsync(a => a.Id == id && a.Role == "Student");

            if (account == null)
            {
                return NotFound();
            }

            if (!currentUserIsSuper)
            {
                var currentUserId = GetCurrentUserId();
                if (account.CreatedById != currentUserId)
                {
                    return Forbid();
                }
            }

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(fullName))
            {
                ModelState.AddModelError("", "Tên đăng nhập và Họ tên không được để trống!");
                ViewBag.IsSuperAdmin = currentUserIsSuper;
                return View(account);
            }

            var cleanUsername = new string(username.ToLower().Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
            if (string.IsNullOrWhiteSpace(cleanUsername))
            {
                ModelState.AddModelError("Username", "Tên đăng nhập không hợp lệ! Chỉ dùng chữ cái, chữ số và dấu gạch dưới.");
                ViewBag.IsSuperAdmin = currentUserIsSuper;
                return View(account);
            }

            if (cleanUsername != account.Username)
            {
                var exists = await _context.Accounts.AnyAsync(a => a.Username == cleanUsername && a.Id != id);
                if (exists)
                {
                    ModelState.AddModelError("Username", "Tên đăng nhập này đã được sử dụng bởi tài khoản khác!");
                    ViewBag.IsSuperAdmin = currentUserIsSuper;
                    return View(account);
                }
                account.Username = cleanUsername;
            }

            account.FullName = fullName.Trim();

            // Only update points & rank for Student accounts
            if (account.Role == "Student")
            {
                account.Points = points >= 0 ? points : 0;
                account.RankTitle = JudgingService.GetRankTitle(account.Points);
            }

            // Super admin can change roles between Student and Teacher, and assign superadmin rights
            if (currentUserIsSuper)
            {
                if (!string.IsNullOrEmpty(role))
                {
                    if (role == "Student" || role == "Teacher")
                    {
                        account.Role = role;
                        if (role == "Teacher")
                        {
                            account.RankTitle = "Đại sư Lập trình 🧙‍♂️";
                            account.IsSuperAdmin = isSuperAdmin;
                        }
                        else
                        {
                            account.RankTitle = JudgingService.GetRankTitle(account.Points);
                            account.IsSuperAdmin = false;
                        }
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(newPassword))
            {
                account.PasswordHash = PasswordHelper.HashPassword(newPassword);
            }

            _context.Entry(account).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Cập nhật thông tin tài khoản {account.Username} thành công!";
            return RedirectToAction(account.Role == "Teacher" ? nameof(Teachers) : nameof(Students));
        }

        // Delete Student Account
        public async Task<IActionResult> DeleteStudent(int id)
        {
            var currentUserIsSuper = await CheckIsSuperAdminAsync();

            var account = currentUserIsSuper
                ? await _context.Accounts.FirstOrDefaultAsync(a => a.Id == id && a.Username != "superadmin")
                : await _context.Accounts.FirstOrDefaultAsync(a => a.Id == id && a.Role == "Student");

            if (account != null)
            {
                if (!currentUserIsSuper)
                {
                    var currentUserId = GetCurrentUserId();
                    if (account.CreatedById != currentUserId)
                    {
                        return Forbid();
                    }
                }

                var role = account.Role;
                // Deleting student/teacher requires removing all associated submissions first due to Restrict key behavior
                var submissions = await _context.Submissions.Where(s => s.AccountId == id).ToListAsync();
                _context.Submissions.RemoveRange(submissions);

                _context.Accounts.Remove(account);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Xóa tài khoản {account.Username} thành công!";
                return RedirectToAction(role == "Teacher" ? nameof(Teachers) : nameof(Students));
            }
            return RedirectToAction(nameof(Students));
        }

        // Delete All Student Accounts (Reset for New Contest)
        public async Task<IActionResult> DeleteAllStudents()
        {
            var currentUserIsSuper = await CheckIsSuperAdminAsync();
            if (!currentUserIsSuper)
            {
                return Forbid();
            }

            var students = await _context.Accounts.Where(a => a.Role == "Student").ToListAsync();
            var studentIds = students.Select(s => s.Id).ToList();

            // Remove submissions for all students
            var submissions = await _context.Submissions.Where(s => studentIds.Contains(s.AccountId)).ToListAsync();
            _context.Submissions.RemoveRange(submissions);

            // Remove student accounts
            _context.Accounts.RemoveRange(students);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã xóa toàn bộ tài khoản học sinh và lịch sử bài làm thành công để chuẩn bị kỳ thi mới!";
            return RedirectToAction(nameof(Students));
        }

        // Delete All Student Accounts Created By This Teacher
        public async Task<IActionResult> DeleteMyStudents()
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Forbid();
            }

            var students = await _context.Accounts
                .Where(a => a.Role == "Student" && a.CreatedById == currentUserId.Value)
                .ToListAsync();

            if (students.Any())
            {
                var studentIds = students.Select(s => s.Id).ToList();

                // Remove submissions for these students
                var submissions = await _context.Submissions.Where(s => studentIds.Contains(s.AccountId)).ToListAsync();
                _context.Submissions.RemoveRange(submissions);

                // Remove student accounts
                _context.Accounts.RemoveRange(students);

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã xóa sạch {students.Count} học sinh do Thầy/Cô tạo thành công!";
            }
            else
            {
                TempData["SuccessMessage"] = "Không tìm thấy học sinh nào do Thầy/Cô tạo để xóa!";
            }

            return RedirectToAction(nameof(Students));
        }

        // --- AUTO CONFIGURATION & COMPILER SETUP ACTION ---
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, System.Text.StringBuilder> TaskLogs = 
            new System.Collections.Concurrent.ConcurrentDictionary<string, System.Text.StringBuilder>();

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> TaskStatuses = 
            new System.Collections.Concurrent.ConcurrentDictionary<string, string>();

        private void AppendLog(string type, string message)
        {
            var sb = TaskLogs.GetOrAdd(type, _ => new System.Text.StringBuilder());
            lock (sb)
            {
                sb.AppendLine($"[{DateTime.Now:HH:mm:ss}] {message}");
            }
        }

        private (bool Active, string Output, string ResolvedPath) GetCompilerStatus(string type)
        {
            var settings = CompilerSettingsHelper.GetSettings();
            string configuredPath = type == "cpp" ? settings.GppPath : settings.PythonPath;
            string resolvedPath = "";

            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                if (System.IO.File.Exists(configuredPath))
                {
                    resolvedPath = configuredPath;
                }
                else
                {
                    resolvedPath = configuredPath;
                }
            }
            else
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                if (type == "cpp")
                {
                    var testPaths = new[]
                    {
                        Path.Combine(baseDir, "Compiler/mingw64/bin/g++.exe"),
                        Path.Combine(baseDir, "Compiler/w64devkit/bin/g++.exe"),
                        Path.Combine(baseDir, "Compiler/w64devkit/w64devkit/bin/g++.exe"),
                        "g++"
                    };
                    foreach (var p in testPaths)
                    {
                        if (p == "g++" || System.IO.File.Exists(p))
                        {
                            resolvedPath = p;
                            break;
                        }
                    }
                }
                else
                {
                    var testPaths = new[]
                    {
                        Path.Combine(baseDir, "Compiler/python/python.exe"),
                        Path.Combine(baseDir, "Compiler/python/python3.exe"),
                        "python"
                    };
                    foreach (var p in testPaths)
                    {
                        if (p == "python" || System.IO.File.Exists(p))
                        {
                            resolvedPath = p;
                            break;
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(resolvedPath))
            {
                return (false, "Không tìm thấy trình biên dịch.", "");
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = resolvedPath,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                var dirName = Path.GetDirectoryName(resolvedPath);
                if (!string.IsNullOrEmpty(dirName) && System.IO.Directory.Exists(dirName))
                {
                    startInfo.EnvironmentVariables["PATH"] = dirName + ";" + (Environment.GetEnvironmentVariable("PATH") ?? "");
                }

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    bool completed = process.WaitForExit(3000);
                    if (completed)
                    {
                        string output = process.StandardOutput.ReadToEnd();
                        string error = process.StandardError.ReadToEnd();
                        if (process.ExitCode == 0)
                        {
                            return (true, string.IsNullOrWhiteSpace(output) ? error : output, resolvedPath);
                        }
                        else
                        {
                            return (false, $"Tiến trình chạy trả về lỗi (Mã lỗi {process.ExitCode}):\n{output}\n{error}", resolvedPath);
                        }
                    }
                    else
                    {
                        try { process.Kill(); } catch { }
                        return (false, "Kiểm tra phiên bản vượt quá thời gian chờ (3 giây).", resolvedPath);
                    }
                }
            }
            catch (Exception ex)
            {
                return (false, $"Lỗi khi khởi chạy tiến trình: {ex.Message}", resolvedPath);
            }

            return (false, "Không thể chạy trình biên dịch.", resolvedPath);
        }

        [HttpGet]
        public async Task<IActionResult> SystemSetup()
        {
            if (!await CheckIsSuperAdminAsync()) return Forbid();

            var settings = CompilerSettingsHelper.GetSettings();
            ViewBag.SettingsGppPath = settings.GppPath;
            ViewBag.SettingsPythonPath = settings.PythonPath;

            var cppStatus = GetCompilerStatus("cpp");
            ViewBag.GppStatus = cppStatus.Active ? "Active" : "Inactive";
            ViewBag.GppVersion = cppStatus.Output;
            ViewBag.GppPath = cppStatus.ResolvedPath;

            var pythonStatus = GetCompilerStatus("python");
            ViewBag.PythonStatus = pythonStatus.Active ? "Active" : "Inactive";
            ViewBag.PythonVersion = pythonStatus.Output;
            ViewBag.PythonPath = pythonStatus.ResolvedPath;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateCompilerSettings(string gppPath, string pythonPath)
        {
            if (!await CheckIsSuperAdminAsync()) return Forbid();

            var settings = new CompilerSettings
            {
                GppPath = gppPath?.Trim() ?? "",
                PythonPath = pythonPath?.Trim() ?? ""
            };

            if (CompilerSettingsHelper.SaveSettings(settings))
            {
                TempData["SuccessMessage"] = "Đã lưu cấu hình đường dẫn trình biên dịch thành công!";
            }
            else
            {
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi lưu tệp cấu hình.";
            }

            return RedirectToAction(nameof(SystemSetup));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AutoDetectCompilers()
        {
            if (!await CheckIsSuperAdminAsync()) return Forbid();

            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var settings = new CompilerSettings();

            // 1. Scan C++ (g++)
            var cppPaths = new[]
            {
                Path.Combine(baseDir, "Compiler/mingw64/bin/g++.exe"),
                Path.Combine(baseDir, "Compiler/w64devkit/bin/g++.exe"),
                Path.Combine(baseDir, "Compiler/w64devkit/w64devkit/bin/g++.exe"),
                @"C:\mingw64\bin\g++.exe",
                @"C:\MinGW\bin\g++.exe",
                @"D:\mingw64\bin\g++.exe"
            };

            foreach (var path in cppPaths)
            {
                if (System.IO.File.Exists(path))
                {
                    settings.GppPath = path;
                    break;
                }
            }

            // If not found in common folders, search environment PATH
            if (string.IsNullOrEmpty(settings.GppPath))
            {
                var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
                var paths = pathEnv.Split(';');
                foreach (var p in paths)
                {
                    try
                    {
                        var fullPath = Path.Combine(p.Trim(), "g++.exe");
                        if (System.IO.File.Exists(fullPath))
                        {
                            settings.GppPath = fullPath;
                            break;
                        }
                    }
                    catch { }
                }
            }

            // 2. Scan Python
            var pythonPaths = new[]
            {
                Path.Combine(baseDir, "Compiler/python/python.exe"),
                @"C:\Python312\python.exe",
                @"C:\Python311\python.exe",
                @"C:\Python310\python.exe",
                @"C:\Python39\python.exe",
                @"D:\Python310\python.exe"
            };

            foreach (var path in pythonPaths)
            {
                if (System.IO.File.Exists(path))
                {
                    settings.PythonPath = path;
                    break;
                }
            }

            if (string.IsNullOrEmpty(settings.PythonPath))
            {
                var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
                var paths = pathEnv.Split(';');
                foreach (var p in paths)
                {
                    try
                    {
                        var fullPath = Path.Combine(p.Trim(), "python.exe");
                        if (System.IO.File.Exists(fullPath))
                        {
                            settings.PythonPath = fullPath;
                            break;
                        }
                        var fullPath3 = Path.Combine(p.Trim(), "python3.exe");
                        if (System.IO.File.Exists(fullPath3))
                        {
                            settings.PythonPath = fullPath3;
                            break;
                        }
                    }
                    catch { }
                }
            }

            CompilerSettingsHelper.SaveSettings(settings);
            TempData["SuccessMessage"] = "Đã quét tự động và lưu đường dẫn tìm thấy!";
            return RedirectToAction(nameof(SystemSetup));
        }

        [HttpPost]
        public async Task<IActionResult> TestCompilerPath(string type, string path)
        {
            if (!await CheckIsSuperAdminAsync()) return Json(new { success = false, message = "Không có quyền truy cập." });

            if (string.IsNullOrWhiteSpace(path))
            {
                return Json(new { success = false, message = "Đường dẫn trống." });
            }

            string targetPath = path.Trim();
            if (targetPath != "g++" && targetPath != "python" && targetPath != "python3" && !System.IO.File.Exists(targetPath))
            {
                return Json(new { success = false, message = "Tệp không tồn tại ở đường dẫn đã chỉ định." });
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = targetPath,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                var dirName = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(dirName) && System.IO.Directory.Exists(dirName))
                {
                    startInfo.EnvironmentVariables["PATH"] = dirName + ";" + (Environment.GetEnvironmentVariable("PATH") ?? "");
                }

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    bool completed = process.WaitForExit(3000);
                    if (completed)
                    {
                        string output = process.StandardOutput.ReadToEnd();
                        string error = process.StandardError.ReadToEnd();
                        if (process.ExitCode == 0)
                        {
                            return Json(new { success = true, output = string.IsNullOrWhiteSpace(output) ? error : output });
                        }
                        else
                        {
                            return Json(new { success = false, message = $"Chương trình trả về mã lỗi {process.ExitCode}.\nOutput: {output}\nError: {error}" });
                        }
                    }
                    else
                    {
                        try { process.Kill(); } catch { }
                        return Json(new { success = false, message = "Quá thời gian chờ kiểm tra (3 giây)." });
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Lỗi thực thi: {ex.Message}" });
            }

            return Json(new { success = false, message = "Không thể chạy tệp." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DownloadCompiler(string type, bool useTempFolder = false)
        {
            if (!await CheckIsSuperAdminAsync()) return Forbid();

            if (type != "cpp" && type != "python")
            {
                return BadRequest("Loại trình biên dịch không hợp lệ.");
            }

            // Start background thread to download and unzip
            var log = TaskLogs.GetOrAdd(type, _ => new System.Text.StringBuilder());
            lock (log) { log.Clear(); }

            TaskStatuses[type] = "running";
            AppendLog(type, $"=== BẮT ĐẦU CÀI ĐẶT TRÌNH BIÊN DỊCH {type.ToUpper()} DI ĐỘNG ===");
            if (useTempFolder)
            {
                AppendLog(type, "Chế độ cài đặt: Cài đặt vào Thư mục Tạm hệ thống (System Temp).");
            }

            _ = Task.Run(async () =>
            {
                string tempZipPath = "";
                try
                {
                    var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    var tempDir = Path.Combine(Path.GetTempPath(), "CodeKidsTempSubmissions");
                    if (!Directory.Exists(tempDir))
                    {
                        Directory.CreateDirectory(tempDir);
                    }

                    string url = "";
                    string destFolder = "";
                    string exeName = "";

                    if (type == "cpp")
                    {
                        url = "https://github.com/skeeto/w64devkit/releases/download/v1.20.0/w64devkit-1.20.0.zip";
                        destFolder = useTempFolder
                            ? Path.Combine(Path.GetTempPath(), "CodeKidsCompiler", "w64devkit")
                            : Path.Combine(baseDir, "Compiler", "w64devkit");
                        exeName = "g++.exe";
                    }
                    else
                    {
                        url = "https://www.python.org/ftp/python/3.10.11/python-3.10.11-embed-amd64.zip";
                        destFolder = useTempFolder
                            ? Path.Combine(Path.GetTempPath(), "CodeKidsCompiler", "python")
                            : Path.Combine(baseDir, "Compiler", "python");
                        exeName = "python.exe";
                    }

                    tempZipPath = Path.Combine(tempDir, $"{type}_compiler_{Guid.NewGuid():N}.zip");

                    AppendLog(type, $"Tạo thư mục giải nén: {destFolder}");
                    if (Directory.Exists(destFolder))
                    {
                        AppendLog(type, $"Thư mục đã tồn tại, sẽ dọn dẹp và giải nén đè...");
                    }
                    else
                    {
                        Directory.CreateDirectory(destFolder);
                    }

                    AppendLog(type, $"Bắt đầu tải từ URL: {url}");
                    AppendLog(type, "Đang tải xuống bộ cài (vui lòng chờ trong giây lát)...");

                    using (var httpClient = new System.Net.Http.HttpClient())
                    {
                        httpClient.Timeout = TimeSpan.FromMinutes(5);
                        using (var response = await httpClient.GetAsync(url, System.Net.Http.HttpCompletionOption.ResponseHeadersRead))
                        {
                            response.EnsureSuccessStatusCode();
                            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                            AppendLog(type, $"Kích thước file: {(totalBytes > 0 ? (totalBytes / 1024.0 / 1024.0).ToString("F2") + " MB" : "Không xác định")}");

                            using (var contentStream = await response.Content.ReadAsStreamAsync())
                            using (var fileStream = new FileStream(tempZipPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                            {
                                var buffer = new byte[8192];
                                var totalRead = 0L;
                                var bytesRead = 0;
                                var lastReportPercent = -10;

                                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                                {
                                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                                    totalRead += bytesRead;

                                    if (totalBytes > 0)
                                    {
                                        int percent = (int)((totalRead * 100) / totalBytes);
                                        if (percent >= lastReportPercent + 10)
                                        {
                                            AppendLog(type, $"Đang tải: {percent}% ({(totalRead / 1024.0 / 1024.0).ToString("F2")} MB / {(totalBytes / 1024.0 / 1024.0).ToString("F2")} MB)");
                                            lastReportPercent = percent;
                                        }
                                    }
                                }
                            }
                        }
                    }

                    AppendLog(type, "Tải xuống thành công! Bắt đầu giải nén ZIP...");
                    
                    // Run extraction
                    System.IO.Compression.ZipFile.ExtractToDirectory(tempZipPath, destFolder, true);
                    AppendLog(type, "Đã giải nén thành công!");

                    // Resolve absolute path to the executable file
                    string absoluteExePath = "";
                    if (type == "cpp")
                    {
                        var checkPaths = new[]
                        {
                            Path.Combine(destFolder, "w64devkit", "bin", exeName),
                            Path.Combine(destFolder, "bin", exeName),
                            Path.Combine(destFolder, exeName)
                        };

                        foreach (var cp in checkPaths)
                        {
                            if (System.IO.File.Exists(cp))
                            {
                                absoluteExePath = cp;
                                break;
                            }
                        }
                    }
                    else
                    {
                        var checkPaths = new[]
                        {
                            Path.Combine(destFolder, exeName),
                            Path.Combine(destFolder, "bin", exeName)
                        };

                        foreach (var cp in checkPaths)
                        {
                            if (System.IO.File.Exists(cp))
                            {
                                absoluteExePath = cp;
                                break;
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(absoluteExePath) && System.IO.File.Exists(absoluteExePath))
                    {
                        AppendLog(type, $"Tìm thấy tệp chạy: {absoluteExePath}");
                        
                        // Update settings
                        var currentSettings = CompilerSettingsHelper.GetSettings();
                        if (type == "cpp")
                        {
                            currentSettings.GppPath = absoluteExePath;
                        }
                        else
                        {
                            currentSettings.PythonPath = absoluteExePath;
                        }
                        CompilerSettingsHelper.SaveSettings(currentSettings);
                        AppendLog(type, $"Đã cập nhật cấu hình hệ thống: {absoluteExePath}");

                        // Verify by running it
                        AppendLog(type, "Đang chạy thử để kiểm tra phiên bản...");
                        var status = GetCompilerStatus(type);
                        AppendLog(type, status.Active ? $"✅ HOÀN THÀNH THÀNH CÔNG! Phiên bản:\n{status.Output}" : $"⚠️ Cảnh báo: Giải nén thành công nhưng không chạy được tệp tin:\n{status.Output}");
                        TaskStatuses[type] = status.Active ? "success" : "error";
                    }
                    else
                    {
                        AppendLog(type, $"❌ Thất bại: Không tìm thấy tệp tin {exeName} sau khi giải nén.");
                        TaskStatuses[type] = "error";
                    }
                }
                catch (Exception ex)
                {
                    AppendLog(type, $"❌ Có lỗi xảy ra: {ex.Message}");
                    AppendLog(type, ex.StackTrace ?? "");
                    TaskStatuses[type] = "error";
                }
                finally
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(tempZipPath) && System.IO.File.Exists(tempZipPath))
                        {
                            System.IO.File.Delete(tempZipPath);
                            AppendLog(type, "Đã dọn dẹp tệp ZIP tạm.");
                        }
                    }
                    catch (Exception cleanupEx)
                    {
                        AppendLog(type, $"Không thể xóa tệp tạm: {cleanupEx.Message}");
                    }
                }
            });

            return Ok(new { success = true, message = "Background task started." });
        }

        [HttpGet]
        public IActionResult GetSetupLog(string type)
        {
            if (string.IsNullOrEmpty(type) || (type != "cpp" && type != "python"))
            {
                return BadRequest("Invalid type.");
            }

            var log = TaskLogs.TryGetValue(type, out var sb) ? sb.ToString() : "";
            var status = TaskStatuses.TryGetValue(type, out var st) ? st : "idle";

            return Json(new { log = log, status = status });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RunSystemDiagnostics()
        {
            if (!await CheckIsSuperAdminAsync()) return Forbid();

            var results = new List<DiagnosticResult>();

            // Test 1: Run system binary (cmd.exe)
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c echo test",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var p = Process.Start(startInfo);
                if (p != null && p.WaitForExit(2000))
                {
                    results.Add(new DiagnosticResult 
                    { 
                        Name = "Thực thi tiến trình hệ thống (cmd.exe)", 
                        Status = "Success", 
                        Detail = "Thành công. Hệ thống cho phép chạy các lệnh cơ bản." 
                    });
                }
                else
                {
                    results.Add(new DiagnosticResult 
                    { 
                        Name = "Thực thi tiến trình hệ thống (cmd.exe)", 
                        Status = "Fail", 
                        Detail = "Không thể phản hồi lệnh trong 2 giây." 
                    });
                }
            }
            catch (Exception ex)
            {
                results.Add(new DiagnosticResult 
                { 
                    Name = "Thực thi tiến trình hệ thống (cmd.exe)", 
                    Status = "Fail", 
                    Detail = $"Lỗi: {ex.Message}" 
                });
            }

            // Test 2: Run from Web Root (wwwroot/TempSubmissions)
            string webRootDest = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TempSubmissions", "diag_test.exe");
            try
            {
                // Ensure TempSubmissions directory exists in BaseDirectory
                string webRootTempDir = Path.GetDirectoryName(webRootDest) ?? "";
                if (!Directory.Exists(webRootTempDir))
                {
                    Directory.CreateDirectory(webRootTempDir);
                }

                string attribPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "attrib.exe");
                if (System.IO.File.Exists(attribPath))
                {
                    System.IO.File.Copy(attribPath, webRootDest, true);
                    
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = webRootDest,
                        Arguments = "/?",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var p = Process.Start(startInfo);
                    if (p != null && p.WaitForExit(2000))
                    {
                        results.Add(new DiagnosticResult 
                        { 
                            Name = "Thực thi từ thư mục Website (Web Root)", 
                            Status = "Success", 
                            Detail = "Thành công. Thư mục website cho phép chạy file thực thi (.exe)." 
                        });
                    }
                    else
                    {
                        results.Add(new DiagnosticResult 
                        { 
                            Name = "Thực thi từ thư mục Website (Web Root)", 
                            Status = "Fail", 
                            Detail = "Không thể chạy file thực thi." 
                        });
                    }
                }
                else
                {
                    results.Add(new DiagnosticResult 
                    { 
                        Name = "Thực thi từ thư mục Website (Web Root)", 
                        Status = "Fail", 
                        Detail = "Không tìm thấy file hệ thống attrib.exe để kiểm tra." 
                    });
                }
            }
            catch (Exception ex)
            {
                string msg = ex.Message;
                if (msg.Contains("blocked by group policy") || msg.Contains("chính sách nhóm") || msg.Contains("Group Policy"))
                {
                    msg = "Bị chặn bởi Chính sách nhóm hệ thống (Group Policy / AppLocker) của máy chủ.";
                }
                results.Add(new DiagnosticResult 
                { 
                    Name = "Thực thi từ thư mục Website (Web Root)", 
                    Status = "Blocked", 
                    Detail = $"Thất bại: {msg}" 
                });
            }
            finally
            {
                try { if (System.IO.File.Exists(webRootDest)) System.IO.File.Delete(webRootDest); } catch { }
            }

            // Test 3: Run from System Temp
            string tempDir = Path.Combine(Path.GetTempPath(), "CodeKidsTempSubmissions");
            string tempDest = Path.Combine(tempDir, "diag_test.exe");
            try
            {
                if (!Directory.Exists(tempDir))
                {
                    Directory.CreateDirectory(tempDir);
                }

                string attribPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "attrib.exe");
                if (System.IO.File.Exists(attribPath))
                {
                    System.IO.File.Copy(attribPath, tempDest, true);
                    
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = tempDest,
                        Arguments = "/?",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var p = Process.Start(startInfo);
                    if (p != null && p.WaitForExit(2000))
                    {
                        results.Add(new DiagnosticResult 
                        { 
                            Name = "Thực thi từ thư mục Tạm hệ thống (System Temp)", 
                            Status = "Success", 
                            Detail = "Thành công! Máy chủ cho phép chạy file thực thi trong thư mục Temp." 
                        });
                    }
                    else
                    {
                        results.Add(new DiagnosticResult 
                        { 
                            Name = "Thực thi từ thư mục Tạm hệ thống (System Temp)", 
                            Status = "Fail", 
                            Detail = "Không thể chạy file thực thi." 
                        });
                    }
                }
                else
                {
                    results.Add(new DiagnosticResult 
                    { 
                        Name = "Thực thi từ thư mục Tạm hệ thống (System Temp)", 
                        Status = "Fail", 
                        Detail = "Không tìm thấy file hệ thống attrib.exe để kiểm tra." 
                    });
                }
            }
            catch (Exception ex)
            {
                string msg = ex.Message;
                if (msg.Contains("blocked by group policy") || msg.Contains("chính sách nhóm") || msg.Contains("Group Policy"))
                {
                    msg = "Bị chặn bởi Chính sách nhóm hệ thống (Group Policy / AppLocker) của máy chủ.";
                }
                results.Add(new DiagnosticResult 
                { 
                    Name = "Thực thi từ thư mục Tạm hệ thống (System Temp)", 
                    Status = "Blocked", 
                    Detail = $"Thất bại: {msg}" 
                });
            }
            finally
            {
                try { if (System.IO.File.Exists(tempDest)) System.IO.File.Delete(tempDest); } catch { }
            }

            return Json(results);
        }

        public class DiagnosticResult
        {
            public string Name { get; set; } = "";
            public string Status { get; set; } = ""; // Success, Fail, Blocked
            public string Detail { get; set; } = "";
        }
    }
}
