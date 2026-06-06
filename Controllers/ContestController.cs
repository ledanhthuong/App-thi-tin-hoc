using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using App_thi_tin_hoc.Data;
using App_thi_tin_hoc.Models;
using App_thi_tin_hoc.Services;

namespace App_thi_tin_hoc.Controllers
{
    [Authorize] // Requires login for all contest operations
    public class ContestController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly JudgingService _judgingService;

        public ContestController(ApplicationDbContext context, JudgingService judgingService)
        {
            _context = context;
            _judgingService = judgingService;
        }

        // Contest Dashboard showing Problems (Quests)
        public async Task<IActionResult> Detail(int id)
        {
            var contest = await _context.Contests
                .Include(c => c.Problems)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (contest == null)
            {
                return NotFound();
            }

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            // Get the best submission status for each problem for this user in the current session
            var currentSession = contest.CurrentSession ?? "Đợt mặc định";
            var userBestSubmissions = await _context.Submissions
                .Where(s => s.AccountId == userId && s.ContestId == id && s.SessionGroup == currentSession)
                .GroupBy(s => s.ProblemId)
                .Select(g => new
                {
                    ProblemId = g.Key,
                    MaxScore = g.Max(s => s.Score),
                    Status = g.Any(s => s.Status == "Accepted") ? "Accepted" : 
                             g.Any(s => s.Status == "Wrong Answer") ? "Wrong Answer" : "Submitted"
                })
                .ToDictionaryAsync(x => x.ProblemId, x => new UserSubmissionSummary { MaxScore = x.MaxScore, Status = x.Status });

            ViewBag.UserBestSubmissions = userBestSubmissions;
            return View(contest);
        }

        // Problem details & Coding workspace
        public async Task<IActionResult> Problem(int id)
        {
            var problem = await _context.Problems
                .Include(p => p.Contest)
                .Include(p => p.Testcases)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (problem == null)
            {
                return NotFound();
            }

            // Fetch sample testcases for preview
            var sampleTestcases = problem.Testcases.Where(t => t.IsSample).ToList();
            ViewBag.SampleTestcases = sampleTestcases;

            // Fetch last submission code for convenience in the current session
            var currentSession = problem.Contest?.CurrentSession ?? "Đợt mặc định";
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var lastSubmission = await _context.Submissions
                .Where(s => s.AccountId == userId && s.ProblemId == id && s.SessionGroup == currentSession)
                .OrderByDescending(s => s.SubmittedAt)
                .FirstOrDefaultAsync();

            ViewBag.LastCode = lastSubmission?.CodeContent ?? "";
            ViewBag.LastLanguage = lastSubmission?.Language ?? "Python";

            return View(problem);
        }

        // POST: Submit solution
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(int problemId, string language, string? codeContent, string? scratchLink, IFormFile? scratchFile)
        {
            var problem = await _context.Problems
                .Include(p => p.Contest)
                .FirstOrDefaultAsync(p => p.Id == problemId);
            if (problem == null || problem.Contest == null)
            {
                return NotFound();
            }

            if (DateTime.UtcNow > problem.Contest.EndTime)
            {
                TempData["ErrorMessage"] = "Đợt thi này đã kết thúc. Bạn không thể nộp bài nữa!";
                return RedirectToAction("Problem", new { id = problemId });
            }

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var submission = new Submission
                {
                    AccountId = userId,
                    ProblemId = problemId,
                    ContestId = problem.ContestId,
                    Language = language,
                    SubmittedAt = DateTime.UtcNow,
                    Status = "Pending",
                    SessionGroup = problem.Contest.CurrentSession ?? "Đợt mặc định"
                };

            if (language == "Scratch")
            {
                if (scratchFile != null && scratchFile.Length > 0)
                {
                    var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/scratch");
                    if (!Directory.Exists(uploadsDir))
                    {
                        Directory.CreateDirectory(uploadsDir);
                    }

                    var uniqueFileName = $"{Guid.NewGuid()}_{scratchFile.FileName}";
                    var filePath = Path.Combine(uploadsDir, uniqueFileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await scratchFile.CopyToAsync(stream);
                    }

                    submission.ScratchLinkOrFile = $"/uploads/scratch/{uniqueFileName}";
                    submission.CodeContent = $"File Scratch uploaded: {scratchFile.FileName}";
                }
                else if (!string.IsNullOrWhiteSpace(scratchLink))
                {
                    submission.ScratchLinkOrFile = scratchLink;
                    submission.CodeContent = $"Scratch Project Link: {scratchLink}";
                }
                else
                {
                    TempData["ErrorMessage"] = "Vui lòng dán link dự án hoặc tải lên file Scratch (.sb3)!";
                    return RedirectToAction("Problem", new { id = problemId });
                }

                submission.Status = "Submitted";
                submission.Feedback = "Bài làm Scratch đã được ghi nhận. Thầy cô sẽ chấm và đánh giá sớm nhất nhé!";
                _context.Submissions.Add(submission);
                await _context.SaveChangesAsync();
            }
            else
            {
                if (string.IsNullOrWhiteSpace(codeContent))
                {
                    TempData["ErrorMessage"] = "Bạn chưa nhập code bài làm!";
                    return RedirectToAction("Problem", new { id = problemId });
                }

                submission.CodeContent = codeContent;
                submission.Status = "Pending";
                
                _context.Submissions.Add(submission);
                await _context.SaveChangesAsync();

                // Queue the judging in the background
                _judgingService.QueueSubmission(submission.Id);
            }

            return RedirectToAction("Submissions", new { contestId = problem.ContestId });
        }

        // Submissions history list for student
        public async Task<IActionResult> Submissions(int contestId, string? session)
        {
            var contest = await _context.Contests.FindAsync(contestId);
            if (contest == null)
            {
                return NotFound();
            }

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            // Fetch all available sessions for this contest
            var sessions = await _context.Submissions
                .Where(s => s.ContestId == contestId)
                .Select(s => s.SessionGroup)
                .Distinct()
                .Where(s => !string.IsNullOrEmpty(s))
                .ToListAsync();

            // Default to current contest session if not specified
            if (string.IsNullOrEmpty(session))
            {
                session = contest.CurrentSession ?? "Đợt mặc định";
            }

            if (!sessions.Contains(session) && !string.IsNullOrEmpty(contest.CurrentSession))
            {
                sessions.Add(contest.CurrentSession);
            }

            var submissions = await _context.Submissions
                .Include(s => s.Problem)
                .Where(s => s.ContestId == contestId && s.AccountId == userId && s.SessionGroup == session)
                .OrderByDescending(s => s.SubmittedAt)
                .ToListAsync();

            ViewBag.Contest = contest;
            ViewBag.Sessions = sessions.OrderByDescending(s => s).ToList();
            ViewBag.SelectedSession = session;
            return View(submissions);
        }

        // Submission detailed feedback
        public async Task<IActionResult> SubmissionDetail(int id)
        {
            var submission = await _context.Submissions
                .Include(s => s.Problem)
                .Include(s => s.Account)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (submission == null)
            {
                return NotFound();
            }

            // Standard safety check: Students can only view their own submissions
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var userRole = User.FindFirstValue(ClaimTypes.Role);

            if (submission.AccountId != userId && userRole != "Teacher")
            {
                return Forbid();
            }

            return View(submission);
        }

        // Contest scoreboard
        public async Task<IActionResult> Leaderboard(int contestId, string? session)
        {
            var contest = await _context.Contests
                .Include(c => c.Problems)
                .FirstOrDefaultAsync(c => c.Id == contestId);

            if (contest == null)
            {
                return NotFound();
            }

            // Fetch all available sessions for this contest
            var sessions = await _context.Submissions
                .Where(s => s.ContestId == contestId)
                .Select(s => s.SessionGroup)
                .Distinct()
                .Where(s => !string.IsNullOrEmpty(s))
                .ToListAsync();

            if (string.IsNullOrEmpty(session))
            {
                session = contest.CurrentSession ?? "Đợt mặc định";
            }

            if (!sessions.Contains(session) && !string.IsNullOrEmpty(contest.CurrentSession))
            {
                sessions.Add(contest.CurrentSession);
            }

            // Calculate student ranks for this contest in the selected session:
            var rawBestScores = await _context.Submissions
                .Where(s => s.ContestId == contestId && s.Account!.Role == "Student" && s.SessionGroup == session)
                .GroupBy(s => new { s.AccountId, s.ProblemId })
                .Select(g => new
                {
                    AccountId = g.Key.AccountId,
                    ProblemId = g.Key.ProblemId,
                    MaxScore = g.Max(s => s.Score)
                })
                .ToListAsync();

            // Sum up scores per student
            var sumScores = rawBestScores
                .GroupBy(s => s.AccountId)
                .ToDictionary(g => g.Key, g => g.Sum(s => s.MaxScore));

            // Pull student details
            var students = await _context.Accounts
                .Where(a => a.Role == "Student")
                .ToListAsync();

            var leaderboardData = students.Select(st => new ContestRankRow
            {
                AccountId = st.Id,
                FullName = st.FullName,
                RankTitle = st.RankTitle,
                AvatarUrl = st.AvatarUrl,
                Points = st.Points, // Global points
                ContestScore = sumScores.ContainsKey(st.Id) ? sumScores[st.Id] : 0
            })
            .OrderByDescending(r => r.ContestScore)
            .ThenByDescending(r => r.Points)
            .ToList();

            ViewBag.Contest = contest;
            ViewBag.Sessions = sessions.OrderByDescending(s => s).ToList();
            ViewBag.SelectedSession = session;
            return View(leaderboardData);
        }
    }

    public class ContestRankRow
    {
        public int AccountId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string RankTitle { get; set; } = string.Empty;
        public string AvatarUrl { get; set; } = string.Empty;
        public int Points { get; set; } // Global points
        public int ContestScore { get; set; } // Contest-only score
    }

    public class UserSubmissionSummary
    {
        public int MaxScore { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
