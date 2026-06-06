using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using App_thi_tin_hoc.Models;

namespace App_thi_tin_hoc.Services
{
    public class JudgingService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<JudgingService> _logger;

        public JudgingService(IServiceScopeFactory scopeFactory, ILogger<JudgingService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        // Runs the judging process in the background
        public void QueueSubmission(int submissionId)
        {
            Task.Run(async () =>
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<Data.ApplicationDbContext>();
                    await JudgeSubmissionAsync(submissionId, dbContext);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error executing background judge for submission {submissionId}");
                }
            });
        }

        public async Task JudgeSubmissionAsync(int submissionId, Data.ApplicationDbContext dbContext)
        {
            var submission = await dbContext.Submissions
                .Include(s => s.Problem)
                .ThenInclude(p => p!.Testcases)
                .Include(s => s.Account)
                .FirstOrDefaultAsync(s => s.Id == submissionId);

            if (submission == null || submission.Problem == null)
            {
                return;
            }

            submission.Status = "Judging";
            await dbContext.SaveChangesAsync();

            if (submission.Language == "Scratch")
            {
                submission.Status = "Submitted";
                submission.Feedback = "Bài Scratch đã được ghi nhận. Thầy cô sẽ chấm điểm và nhận xét thủ công nhé!";
                await dbContext.SaveChangesAsync();
                return;
            }

            if (submission.Language == "C++")
            {
                var testcases = submission.Problem.Testcases.ToList();
                if (!testcases.Any())
                {
                    submission.Status = "Accepted";
                    submission.Score = submission.Problem.Points;
                    submission.Feedback = "Bài này chưa được cấu hình bộ testcase. Chấp nhận tự động!";
                    await dbContext.SaveChangesAsync();
                    return;
                }

                var tempDir = Path.Combine(Path.GetTempPath(), "CodeKidsTempSubmissions");
                if (!Directory.Exists(tempDir))
                {
                    Directory.CreateDirectory(tempDir);
                }

                var cppFileName = $"sub_{submission.Id}.cpp";
                var exeFileName = $"sub_{submission.Id}.exe";
                var cppFilePath = Path.Combine(tempDir, cppFileName);
                var exeFilePath = Path.Combine(tempDir, exeFileName);

                try
                {
                    await File.WriteAllTextAsync(cppFilePath, submission.CodeContent, new UTF8Encoding(false));

                    var settings = App_thi_tin_hoc.Helpers.CompilerSettingsHelper.GetSettings();
                    var gppPath = "g++";
                    var localBin = "";

                    if (!string.IsNullOrWhiteSpace(settings.GppPath) && File.Exists(settings.GppPath))
                    {
                        gppPath = settings.GppPath;
                        localBin = Path.GetDirectoryName(gppPath) ?? "";
                    }
                    else
                    {
                        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                        var testPaths = new[]
                        {
                            Path.Combine(baseDir, "Compiler/mingw64/bin"),
                            Path.Combine(baseDir, "Compiler/w64devkit/bin"),
                            Path.Combine(baseDir, "Compiler/w64devkit/w64devkit/bin")
                        };

                        foreach (var path in testPaths)
                        {
                            if (Directory.Exists(path) && File.Exists(Path.Combine(path, "g++.exe")))
                            {
                                localBin = path;
                                gppPath = Path.Combine(path, "g++.exe");
                                break;
                            }
                        }
                    }

                    // Compile the C++ code
                    var compileStartInfo = new ProcessStartInfo
                    {
                        FileName = gppPath,
                        Arguments = $"-O3 -std=c++17 \"{cppFilePath}\" -o \"{exeFilePath}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    if (!string.IsNullOrEmpty(localBin) && Directory.Exists(localBin))
                    {
                        compileStartInfo.EnvironmentVariables["PATH"] = localBin + ";" + (Environment.GetEnvironmentVariable("PATH") ?? "");
                    }

                    using (var compileProcess = new Process { StartInfo = compileStartInfo })
                    {
                        try
                        {
                            compileProcess.Start();
                        }
                        catch (Exception ex)
                        {
                            submission.Status = "Judging Error";
                            submission.Feedback = "❌ Lỗi hệ thống: Máy chủ chưa cấu hình hoặc không tìm thấy trình biên dịch C++ (g++). Vui lòng báo với Thầy/Cô quản trị viên kiểm tra nhé!\n\nChi tiết lỗi: " + ex.Message;
                            await dbContext.SaveChangesAsync();
                            return;
                        }

                        var compileOutput = await compileProcess.StandardOutput.ReadToEndAsync();
                        var compileError = await compileProcess.StandardError.ReadToEndAsync();
                        bool compileCompleted = true;
                        using (var cts = new System.Threading.CancellationTokenSource(10000))
                        {
                            try
                            {
                                await compileProcess.WaitForExitAsync(cts.Token);
                            }
                            catch (OperationCanceledException)
                            {
                                compileCompleted = false;
                            }
                        }

                        if (!compileCompleted)
                        {
                            try 
                            { 
                                compileProcess.Kill(); 
                                await compileProcess.WaitForExitAsync();
                            } 
                            catch { }
                            submission.Status = "Compile Error";
                            submission.Feedback = "❌ Lỗi biên dịch: Biên dịch vượt quá thời gian giới hạn (10 giây).";
                            await dbContext.SaveChangesAsync();
                            return;
                        }

                        if (compileProcess.ExitCode != 0)
                        {
                            submission.Status = "Compile Error";
                            submission.Feedback = $"❌ Lỗi biên dịch (Compile Error):\n{compileError}\n{compileOutput}";
                            await dbContext.SaveChangesAsync();
                            return;
                        }
                    }

                    if (!File.Exists(exeFilePath))
                    {
                        submission.Status = "Compile Error";
                        submission.Feedback = "❌ Lỗi biên dịch: Không tìm thấy file chạy (.exe) sau khi biên dịch.";
                        await dbContext.SaveChangesAsync();
                        return;
                    }

                    bool allPassed = true;
                    int passedCount = 0;
                    int maxTimeMs = 0;
                    var feedbackBuilder = new StringBuilder();

                    foreach (var tc in testcases)
                    {
                        var startInfo = new ProcessStartInfo
                        {
                            FileName = exeFilePath,
                            RedirectStandardInput = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };

                        if (!string.IsNullOrEmpty(localBin) && Directory.Exists(localBin))
                        {
                            startInfo.EnvironmentVariables["PATH"] = localBin + ";" + (Environment.GetEnvironmentVariable("PATH") ?? "");
                        }

                        using var process = new Process { StartInfo = startInfo };
                        var stopwatch = Stopwatch.StartNew();

                        try
                        {
                            process.Start();
                        }
                        catch (Exception ex)
                        {
                            submission.Status = "Judging Error";
                            submission.Feedback = $"Không thể khởi chạy file thực thi: {ex.Message}";
                            await dbContext.SaveChangesAsync();
                            return;
                        }

                        // Feed the input data
                        if (!string.IsNullOrEmpty(tc.InputData))
                        {
                            await process.StandardInput.WriteLineAsync(tc.InputData);
                            await process.StandardInput.FlushAsync();
                        }
                        process.StandardInput.Close();

                        var outputTask = process.StandardOutput.ReadToEndAsync();
                        var errorTask = process.StandardError.ReadToEndAsync();

                        var timeLimit = submission.Problem.TimeLimitMs > 0 ? submission.Problem.TimeLimitMs : 2000;
                        bool completed = true;

                        using (var cts = new System.Threading.CancellationTokenSource(timeLimit))
                        {
                            try
                            {
                                await process.WaitForExitAsync(cts.Token);
                            }
                            catch (OperationCanceledException)
                            {
                                completed = false;
                            }
                        }

                        stopwatch.Stop();
                        var elapsedMs = (int)stopwatch.ElapsedMilliseconds;
                        if (elapsedMs > maxTimeMs) maxTimeMs = elapsedMs;

                        if (!completed)
                        {
                            try 
                            { 
                                process.Kill(); 
                                await process.WaitForExitAsync();
                            } 
                            catch { }
                            allPassed = false;
                            feedbackBuilder.AppendLine($"❌ Testcase #{tc.Id}: Quá thời gian chạy (Time Limit Exceeded) (> {timeLimit}ms)");
                            continue;
                        }

                        var output = await outputTask;
                        var error = await errorTask;

                        if (process.ExitCode != 0)
                        {
                            allPassed = false;
                            feedbackBuilder.AppendLine($"❌ Testcase #{tc.Id}: Lỗi khi chạy (Runtime Error)");
                            if (!string.IsNullOrWhiteSpace(error))
                            {
                                feedbackBuilder.AppendLine($"   Chi tiết lỗi: {error.Trim()}");
                            }
                            continue;
                        }

                        var normalizedOutput = NormalizeOutput(output);
                        var normalizedExpected = NormalizeOutput(tc.ExpectedOutput);

                        if (normalizedOutput == normalizedExpected)
                        {
                            passedCount++;
                            feedbackBuilder.AppendLine($"✅ Testcase #{tc.Id}: Chính xác!");
                        }
                        else
                        {
                            allPassed = false;
                            feedbackBuilder.AppendLine($"❌ Testcase #{tc.Id}: Sai kết quả (Wrong Answer)");
                            feedbackBuilder.AppendLine($"   - Input: {tc.InputData.Trim()}");
                            feedbackBuilder.AppendLine($"   - Kết quả mong đợi: '{normalizedExpected}'");
                            feedbackBuilder.AppendLine($"   - Kết quả của bạn: '{normalizedOutput}'");
                        }
                    }

                    submission.ExecutionTimeMs = maxTimeMs;
                    if (allPassed)
                    {
                        submission.Status = "Accepted";
                        submission.Score = submission.Problem.Points;
                        submission.Feedback = $"🏆 TUYỆT VỜI! Bài làm hoàn toàn chính xác ({passedCount}/{testcases.Count} testcase).\n\n" + feedbackBuilder.ToString();

                        await UpdateStudentPointsAsync(submission.AccountId, submission.ProblemId, submission.Problem.Points, dbContext);
                    }
                    else
                    {
                        submission.Status = "Wrong Answer";
                        double ratio = (double)passedCount / testcases.Count;
                        int computedScore = (int)(submission.Problem.Points * ratio);
                        submission.Score = computedScore;
                        submission.Feedback = $"🌟 Hãy cố gắng lên! Đúng {passedCount}/{testcases.Count} testcase. Đạt {computedScore} điểm.\n\n" + feedbackBuilder.ToString();

                        await UpdateStudentPointsAsync(submission.AccountId, submission.ProblemId, computedScore, dbContext);
                    }
                }
                catch (Exception ex)
                {
                    submission.Status = "Judging Error";
                    submission.Feedback = $"Có lỗi hệ thống xảy ra khi chấm bài: {ex.Message}";
                }
                finally
                {
                    try
                    {
                        if (File.Exists(cppFilePath)) File.Delete(cppFilePath);
                        if (File.Exists(exeFilePath)) File.Delete(exeFilePath);
                    }
                    catch { }

                    await dbContext.SaveChangesAsync();
                }
            }

            if (submission.Language == "Python")
            {
                var testcases = submission.Problem.Testcases.ToList();
                if (!testcases.Any())
                {
                    submission.Status = "Accepted";
                    submission.Score = submission.Problem.Points;
                    submission.Feedback = "Bài này chưa được cấu hình bộ testcase. Chấp nhận tự động!";
                    await dbContext.SaveChangesAsync();
                    return;
                }

                var tempDir = Path.Combine(Path.GetTempPath(), "CodeKidsTempSubmissions");
                if (!Directory.Exists(tempDir))
                {
                    Directory.CreateDirectory(tempDir);
                }

                var fileName = $"sub_{submission.Id}.py";
                var filePath = Path.Combine(tempDir, fileName);

                try
                {
                    await File.WriteAllTextAsync(filePath, submission.CodeContent, new UTF8Encoding(false));

                    bool allPassed = true;
                    int passedCount = 0;
                    int maxTimeMs = 0;
                    var feedbackBuilder = new StringBuilder();

                    foreach (var tc in testcases)
                    {
                        var settings = App_thi_tin_hoc.Helpers.CompilerSettingsHelper.GetSettings();
                        var pyPath = "python";

                        if (!string.IsNullOrWhiteSpace(settings.PythonPath) && File.Exists(settings.PythonPath))
                        {
                            pyPath = settings.PythonPath;
                        }
                        else
                        {
                            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                            var testPaths = new[]
                            {
                                Path.Combine(baseDir, "Compiler/python/python.exe"),
                                Path.Combine(baseDir, "Compiler/python/python3.exe")
                            };

                            foreach (var path in testPaths)
                            {
                                if (File.Exists(path))
                                {
                                    pyPath = path;
                                    break;
                                }
                            }
                        }

                        var startInfo = new ProcessStartInfo
                        {
                            FileName = pyPath,
                            Arguments = $"\"{filePath}\"",
                            RedirectStandardInput = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };

                        var pyDir = Path.GetDirectoryName(pyPath);
                        if (!string.IsNullOrEmpty(pyDir) && Directory.Exists(pyDir))
                        {
                            startInfo.EnvironmentVariables["PATH"] = pyDir + ";" + (Environment.GetEnvironmentVariable("PATH") ?? "");
                        }

                        using var process = new Process { StartInfo = startInfo };
                        var stopwatch = Stopwatch.StartNew();

                        try
                        {
                            process.Start();
                        }
                        catch (Exception ex)
                        {
                            submission.Status = "Judging Error";
                            submission.Feedback = $"Không thể khởi chạy Python: {ex.Message}. Hãy chắc chắn máy chủ đã cài đặt Python 3.";
                            await dbContext.SaveChangesAsync();
                            return;
                        }

                        // Feed the input data
                        if (!string.IsNullOrEmpty(tc.InputData))
                        {
                            await process.StandardInput.WriteLineAsync(tc.InputData);
                            await process.StandardInput.FlushAsync();
                        }
                        process.StandardInput.Close();

                        // Read output and error streams
                        var outputTask = process.StandardOutput.ReadToEndAsync();
                        var errorTask = process.StandardError.ReadToEndAsync();

                        var timeLimit = submission.Problem.TimeLimitMs > 0 ? submission.Problem.TimeLimitMs : 2000;
                        bool completed = true;

                        using (var cts = new System.Threading.CancellationTokenSource(timeLimit))
                        {
                            try
                            {
                                await process.WaitForExitAsync(cts.Token);
                            }
                            catch (OperationCanceledException)
                            {
                                completed = false;
                            }
                        }

                        stopwatch.Stop();
                        var elapsedMs = (int)stopwatch.ElapsedMilliseconds;
                        if (elapsedMs > maxTimeMs) maxTimeMs = elapsedMs;

                        if (!completed)
                        {
                            try 
                            { 
                                process.Kill(); 
                                await process.WaitForExitAsync();
                            } 
                            catch { }
                            allPassed = false;
                            feedbackBuilder.AppendLine($"❌ Testcase #{tc.Id}: Quá thời gian chạy (Time Limit Exceeded) (> {timeLimit}ms)");
                            continue;
                        }

                        var output = await outputTask;
                        var error = await errorTask;

                        if (process.ExitCode != 0)
                        {
                            allPassed = false;
                            feedbackBuilder.AppendLine($"❌ Testcase #{tc.Id}: Lỗi khi chạy (Runtime Error)");
                            if (!string.IsNullOrWhiteSpace(error))
                            {
                                feedbackBuilder.AppendLine($"   Chi tiết lỗi: {error.Trim()}");
                            }
                            continue;
                        }

                        var normalizedOutput = NormalizeOutput(output);
                        var normalizedExpected = NormalizeOutput(tc.ExpectedOutput);

                        if (normalizedOutput == normalizedExpected)
                        {
                            passedCount++;
                            feedbackBuilder.AppendLine($"✅ Testcase #{tc.Id}: Chính xác!");
                        }
                        else
                        {
                            allPassed = false;
                            feedbackBuilder.AppendLine($"❌ Testcase #{tc.Id}: Sai kết quả (Wrong Answer)");
                            feedbackBuilder.AppendLine($"   - Input: {tc.InputData.Trim()}");
                            feedbackBuilder.AppendLine($"   - Kết quả mong đợi: '{normalizedExpected}'");
                            feedbackBuilder.AppendLine($"   - Kết quả của bạn: '{normalizedOutput}'");
                        }
                    }

                    submission.ExecutionTimeMs = maxTimeMs;
                    if (allPassed)
                    {
                        submission.Status = "Accepted";
                        submission.Score = submission.Problem.Points;
                        submission.Feedback = $"🏆 TUYỆT VỜI! Bài làm hoàn toàn chính xác ({passedCount}/{testcases.Count} testcase).\n\n" + feedbackBuilder.ToString();

                        await UpdateStudentPointsAsync(submission.AccountId, submission.ProblemId, submission.Problem.Points, dbContext);
                    }
                    else
                    {
                        submission.Status = "Wrong Answer";
                        double ratio = (double)passedCount / testcases.Count;
                        int computedScore = (int)(submission.Problem.Points * ratio);
                        submission.Score = computedScore;
                        submission.Feedback = $"🌟 Hãy cố gắng lên! Đúng {passedCount}/{testcases.Count} testcase. Đạt {computedScore} điểm.\n\n" + feedbackBuilder.ToString();

                        await UpdateStudentPointsAsync(submission.AccountId, submission.ProblemId, computedScore, dbContext);
                    }
                }
                catch (Exception ex)
                {
                    submission.Status = "Judging Error";
                    submission.Feedback = $"Có lỗi hệ thống xảy ra khi chấm bài: {ex.Message}";
                }
                finally
                {
                    try
                    {
                        if (File.Exists(filePath))
                        {
                            File.Delete(filePath);
                        }
                    }
                    catch { }

                    await dbContext.SaveChangesAsync();
                }
            }
        }

        private async Task UpdateStudentPointsAsync(int accountId, int problemId, int newScore, Data.ApplicationDbContext dbContext)
        {
            // Find current highest score for this problem by this user (excluding the current submission)
            var pastScores = await dbContext.Submissions
                .Where(s => s.AccountId == accountId && s.ProblemId == problemId && s.Status != "Judging" && s.Status != "Pending")
                .Select(s => s.Score)
                .ToListAsync();
            var maxPastScore = pastScores.Any() ? pastScores.Max() : 0;

            if (newScore > maxPastScore)
            {
                var account = await dbContext.Accounts.FindAsync(accountId);
                if (account != null)
                {
                    // Add the score improvement
                    account.Points += (newScore - maxPastScore);
                    account.RankTitle = GetRankTitle(account.Points);
                    dbContext.Entry(account).State = EntityState.Modified;
                }
            }
        }

        public static string GetRankTitle(int points)
        {
            if (points >= 1000) return "Huyền thoại Thuật toán 🏆";
            if (points >= 700) return "Đại sư Lập trình 🧙‍♂️";
            if (points >= 500) return "Dũng sĩ Thuật toán ⚔️";
            if (points >= 300) return "Hiệp sĩ Code 🛡️";
            if (points >= 150) return "Thợ rèn Mật mã 🔨";
            return "Tập sự 🌟";
        }

        private string NormalizeOutput(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var lines = s.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None)
                         .Select(l => l.TrimEnd())
                         .ToList();
            
            while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines.Last()))
            {
                lines.RemoveAt(lines.Count - 1);
            }

            return string.Join("\n", lines).Trim();
        }
    }
}
