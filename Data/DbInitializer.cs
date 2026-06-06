using System;
using System.Linq;
using App_thi_tin_hoc.Models;
using App_thi_tin_hoc.Helpers;
using Microsoft.EntityFrameworkCore;

namespace App_thi_tin_hoc.Data
{
    public static class DbInitializer
    {
        public static void Initialize(ApplicationDbContext context)
        {
            context.Database.EnsureCreated();

            // 1. Run raw SQL to ensure new columns exist in case the DB was already created
            try
            {
                context.Database.ExecuteSqlRaw("IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Contests') AND name = 'CurrentSession') ALTER TABLE Contests ADD CurrentSession NVARCHAR(255) NULL;");
                context.Database.ExecuteSqlRaw("IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Submissions') AND name = 'SessionGroup') ALTER TABLE Submissions ADD SessionGroup NVARCHAR(255) NULL;");
                context.Database.ExecuteSqlRaw("IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Contests') AND name = 'ShowCodeHints') ALTER TABLE Contests ADD ShowCodeHints BIT NOT NULL DEFAULT 1;");
                context.Database.ExecuteSqlRaw("IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Accounts') AND name = 'IsSuperAdmin') ALTER TABLE Accounts ADD IsSuperAdmin BIT NOT NULL DEFAULT 0;");
                context.Database.ExecuteSqlRaw("IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Accounts') AND name = 'CreatedById') ALTER TABLE Accounts ADD CreatedById INT NULL;");
            }
            catch (Exception) { }

            // 2. Ensure superadmin exists
            if (!context.Accounts.Any(a => a.Username == "superadmin"))
            {
                var superadmin = new Account
                {
                    Username = "superadmin",
                    PasswordHash = PasswordHelper.HashPassword("admin123"),
                    FullName = "Super Admin",
                    Role = "Teacher",
                    IsSuperAdmin = true,
                    Points = 0,
                    RankTitle = "Đại sư Lập trình 🧙‍♂️",
                    AvatarUrl = "/images/avatars/teacher.svg"
                };
                context.Accounts.Add(superadmin);
                context.SaveChanges();
            }

            // 3. Seed default teacher and students if empty
            if (!context.Accounts.Any(a => a.Username == "giauviem1"))
            {
                var teacher = new Account
                {
                    Username = "giauviem1",
                    PasswordHash = PasswordHelper.HashPassword("123456"),
                    FullName = "Thầy Nguyễn Văn A",
                    Role = "Teacher",
                    Points = 0,
                    RankTitle = "Đại sư Lập trình 🧙‍♂️",
                    AvatarUrl = "/images/avatars/teacher.svg"
                };

                var student1 = new Account
                {
                    Username = "hocsinh1",
                    PasswordHash = PasswordHelper.HashPassword("123456"),
                    FullName = "Lê Hoàng Nam",
                    Role = "Student",
                    Points = 100,
                    RankTitle = "Tập sự 🌟",
                    AvatarUrl = "/images/avatars/avatar1.svg"
                };

                var student2 = new Account
                {
                    Username = "hocsinh2",
                    PasswordHash = PasswordHelper.HashPassword("123456"),
                    FullName = "Nguyễn Minh Thư",
                    Role = "Student",
                    Points = 200,
                    RankTitle = "Thợ rèn Mật mã 🔨",
                    AvatarUrl = "/images/avatars/avatar2.svg"
                };

                var student3 = new Account
                {
                    Username = "hocsinh3",
                    PasswordHash = PasswordHelper.HashPassword("123456"),
                    FullName = "Trần Gia Bảo",
                    Role = "Student",
                    Points = 350,
                    RankTitle = "Hiệp sĩ Code 🛡️",
                    AvatarUrl = "/images/avatars/avatar3.svg"
                };

                context.Accounts.AddRange(teacher, student1, student2, student3);
                context.SaveChanges();
            }

            // 4. Remove old seeded single contest if it exists
            var oldContest = context.Contests.FirstOrDefault(c => c.Title == "Hội Thi Lập Trình Nhí - Vòng 1");
            if (oldContest != null)
            {
                context.Contests.Remove(oldContest);
                context.SaveChanges();
            }

            // 5. Seed the 5 contests
            if (!context.Contests.Any(c => c.Title.StartsWith("Vòng ")))
            {
                var initSessionName = "Đợt khởi tạo - " + DateTime.UtcNow.AddHours(7).ToString("dd/MM/yyyy HH:mm");

                var contestsData = new[]
                {
                    new {
                        Title = "Vòng 1: Khởi Động Nhẹ Nhàng 🚀",
                        Description = "Sân chơi khởi động giúp bé làm quen với các bài toán cộng trừ, nhân đôi cơ bản.",
                        Problems = new[] {
                            new {
                                Title = "Giúp Gấu Cộng Táo 🍎",
                                Diff = "Dễ", Points = 100,
                                Story = "<h3>Câu chuyện:</h3><p>Gấu Teddy nhặt được A quả táo đỏ và B quả táo xanh. Tính tổng số táo Teddy có.</p><h4>Đầu vào:</h4><p>Hai số nguyên A và B, mỗi số trên một dòng.</p><h4>Đầu ra:</h4><p>Một số nguyên duy nhất là tổng số táo A + B.</p>",
                                Inputs = new[] { "5 3", "10 20", "0 0" },
                                Outputs = new[] { "8", "30", "0" }
                            },
                            new {
                                Title = "Gấp Đôi Niềm Vui 🍬",
                                Diff = "Dễ", Points = 100,
                                Story = "<h3>Câu chuyện:</h3><p>Mẹ cho bé N viên kẹo. Bố cho bé gấp đôi số kẹo đó. Tính tổng số kẹo bé nhận từ bố.</p><h4>Đầu vào:</h4><p>Một số nguyên N.</p><h4>Đầu ra:</h4><p>Số kẹo của bố cho (N * 2).</p>",
                                Inputs = new[] { "5", "10", "1" },
                                Outputs = new[] { "10", "20", "2" }
                            },
                            new {
                                Title = "Chu Vi Sân Chơi 🏫",
                                Diff = "Trung bình", Points = 150,
                                Story = "<h3>Câu chuyện:</h3><p>Sân trường của bé hình vuông có cạnh dài A mét. Em hãy tính chu vi của sân chơi nhé.</p><h4>Đầu vào:</h4><p>Một số nguyên A.</p><h4>Đầu ra:</h4><p>Chu vi sân chơi (A * 4).</p>",
                                Inputs = new[] { "5", "10", "100" },
                                Outputs = new[] { "20", "40", "400" }
                            },
                            new {
                                Title = "Tìm Số Lớn Nhất 🏆",
                                Diff = "Trung bình", Points = 150,
                                Story = "<h3>Câu chuyện:</h3><p>Bé Nam có A viên bi, bé Bắc có B viên bi. Hãy in ra số lượng bi lớn nhất của một trong hai bạn.</p><h4>Đầu vào:</h4><p>Hai số nguyên A và B, mỗi số trên một dòng.</p><h4>Đầu ra:</h4><p>Số lớn nhất trong hai số.</p>",
                                Inputs = new[] { "5 8", "12 10", "7 7" },
                                Outputs = new[] { "8", "12", "7" }
                            },
                            new {
                                Title = "Chia Kẹo Cho Bạn 🧑‍🤝‍🧑",
                                Diff = "Khó", Points = 200,
                                Story = "<h3>Câu chuyện:</h3><p>Mẹ có K viên kẹo muốn chia đều cho N bạn nhỏ. Tính số kẹo mỗi bạn nhận được và số kẹo còn dư.</p><h4>Đầu vào:</h4><p>Hai số K và N trên 2 dòng.</p><h4>Đầu ra:</h4><p>Hai số cách nhau dấu cách: số kẹo mỗi bạn nhận và số kẹo dư.</p>",
                                Inputs = new[] { "23 5", "50 10", "17 4" },
                                Outputs = new[] { "4 3", "5 0", "4 1" }
                            }
                        }
                    },
                    new {
                        Title = "Vòng 2: Khám Phá Rừng Xanh 🌳",
                        Description = "Cùng thỏ con và sóc con tính toán diện tích, đếm bước chân vượt chướng ngại vật trong rừng.",
                        Problems = new[] {
                            new {
                                Title = "Chu Vi Ao Cá Rừng 🐟",
                                Diff = "Dễ", Points = 100,
                                Story = "<h3>Câu chuyện:</h3><p>Ao cá trong rừng hình chữ nhật có chiều dài L và rộng W. Tính chu vi ao cá.</p><h4>Đầu vào:</h4><p>Hai số nguyên L và W trên 2 dòng.</p><h4>Đầu ra:</h4><p>Chu vi của ao (bằng (L + W) * 2).</p>",
                                Inputs = new[] { "5 4", "10 8", "3 3" },
                                Outputs = new[] { "18", "36", "12" }
                            },
                            new {
                                Title = "Diện Tích Trảng Cỏ 🌾",
                                Diff = "Dễ", Points = 100,
                                Story = "<h3>Câu chuyện:</h3><p>Trảng cỏ hình vuông có cạnh dài A. Tính diện tích trảng cỏ.</p><h4>Đầu vào:</h4><p>Một số nguyên A.</p><h4>Đầu ra:</h4><p>Diện tích trảng cỏ (A * A).</p>",
                                Inputs = new[] { "5", "10", "12" },
                                Outputs = new[] { "25", "100", "144" }
                            },
                            new {
                                Title = "Bậc Thang Nhà Sàn 🪵",
                                Diff = "Trung bình", Points = 150,
                                Story = "<h3>Câu chuyện:</h3><p>Nhà sàn có N bậc thang. Hoa đi lên hết rồi đi xuống hết thì tổng số bước chân là bao nhiêu?</p><h4>Đầu vào:</h4><p>Một số nguyên N.</p><h4>Đầu ra:</h4><p>Tổng số bước chân Hoa bước (N * 2).</p>",
                                Inputs = new[] { "7", "9", "5" },
                                Outputs = new[] { "14", "18", "10" }
                            },
                            new {
                                Title = "Đếm Xe Đạp 🚲",
                                Diff = "Trung bình", Points = 150,
                                Story = "<h3>Câu chuyện:</h3><p>Trong bãi xe có W bánh xe đạp. Biết mỗi xe đạp có 2 bánh, hỏi có bao nhiêu xe đạp?</p><h4>Đầu vào:</h4><p>Một số nguyên chẵn W.</p><h4>Đầu ra:</h4><p>Số xe đạp (W / 2).</p>",
                                Inputs = new[] { "10", "20", "2" },
                                Outputs = new[] { "5", "10", "1" }
                            },
                            new {
                                Title = "Phân Loại Chẵn Lẻ 🔢",
                                Diff = "Khó", Points = 200,
                                Story = "<h3>Câu chuyện:</h3><p>Đọc số nguyên N, in ra từ 'Chan' nếu số đó là số chẵn, in ra 'Le' nếu số lẻ.</p><h4>Đầu vào:</h4><p>Một số nguyên N.</p><h4>Đầu ra:</h4><p>'Chan' hoặc 'Le'.</p>",
                                Inputs = new[] { "8", "15", "0" },
                                Outputs = new[] { "Chan", "Le", "Chan" }
                            }
                        }
                    },
                    new {
                        Title = "Vòng 3: Giải Cứu Đại Dương 🌊",
                        Description = "Độ khó tăng dần! Hãy giúp các chú sứa và rùa biển tính toán và so sánh số lượng.",
                        Problems = new[] {
                            new {
                                Title = "Hiệu Số Cua Biển 🦀",
                                Diff = "Dễ", Points = 100,
                                Story = "<h3>Câu chuyện:</h3><p>Bại cát có A chú cua. Sóng biển cuốn đi B chú. Tính số cua còn lại.</p><h4>Đầu vào:</h4><p>Hai số nguyên A và B trên 2 dòng.</p><h4>Đầu ra:</h4><p>Hiệu số cua còn lại (A - B).</p>",
                                Inputs = new[] { "10 3", "20 20", "5 0" },
                                Outputs = new[] { "7", "0", "5" }
                            },
                            new {
                                Title = "Tính Tuổi Rùa Con 🐢",
                                Diff = "Dễ", Points = 100,
                                Story = "<h3>Câu chuyện:</h3><p>Rùa sinh năm Y. Năm nay là năm C. Tính tuổi của rùa con.</p><h4>Đầu vào:</h4><p>Hai số nguyên Y và C trên 2 dòng.</p><h4>Đầu ra:</h4><p>Tuổi rùa con (C - Y).</p>",
                                Inputs = new[] { "2020 2026", "2018 2026", "2026 2026" },
                                Outputs = new[] { "6", "8", "0" }
                            },
                            new {
                                Title = "Tổng Điểm Sao Biển ⭐",
                                Diff = "Trung bình", Points = 150,
                                Story = "<h3>Câu chuyện:</h3><p>Sao biển đỏ có A điểm, sao biển xanh có B điểm, sao biển vàng có C điểm. Tính tổng điểm.</p><h4>Đầu vào:</h4><p>Ba số nguyên A, B, C trên 3 dòng.</p><h4>Đầu ra:</h4><p>Tổng A + B + C.</p>",
                                Inputs = new[] { "1 2 3", "10 20 30", "5 5 5" },
                                Outputs = new[] { "6", "60", "15" }
                            },
                            new {
                                Title = "Mua Vở Học Tập 📔",
                                Diff = "Trung bình", Points = 150,
                                Story = "<h3>Câu chuyện:</h3><p>Bé mua N cuốn vở với giá P đồng mỗi cuốn. Tính số tiền bé phải trả.</p><h4>Đầu vào:</h4><p>Hai số nguyên N và P trên 2 dòng.</p><h4>Đầu ra:</h4><p>Số tiền phải trả (N * P).</p>",
                                Inputs = new[] { "5 5000", "10 3000", "2 8000" },
                                Outputs = new[] { "25000", "30000", "16000" }
                            },
                            new {
                                Title = "Sao Biển Lớn Nhất ⭐",
                                Diff = "Khó", Points = 200,
                                Story = "<h3>Câu chuyện:</h3><p>Cho 3 số nguyên A, B, C. Tìm số lớn nhất.</p><h4>Đầu vào:</h4><p>Ba số nguyên A, B, C trên 3 dòng.</p><h4>Đầu ra:</h4><p>Số lớn nhất trong ba số.</p>",
                                Inputs = new[] { "5 9 3", "12 10 15", "7 7 7" },
                                Outputs = new[] { "9", "15", "7" }
                            }
                        }
                    },
                    new {
                        Title = "Vòng 4: Du Hành Vũ Trụ 🚀",
                        Description = "Tính toán khoảng cách, vận tốc và chuyển đổi các đơn vị đo lường trong không gian.",
                        Problems = new[] {
                            new {
                                Title = "Thời Giờ Đổi Phút ⏰",
                                Diff = "Dễ", Points = 100,
                                Story = "<h3>Câu chuyện:</h3><p>Phi hành gia du hành trong H giờ. Hãy tính xem chuyến đi kéo dài bao nhiêu phút.</p><h4>Đầu vào:</h4><p>Một số nguyên H là số giờ.</p><h4>Đầu ra:</h4><p>Số phút tương ứng (H * 60).</p>",
                                Inputs = new[] { "2", "5", "24" },
                                Outputs = new[] { "120", "300", "1440" }
                            },
                            new {
                                Title = "Đổi Đơn Vị Mét 🌌",
                                Diff = "Dễ", Points = 100,
                                Story = "<h3>Câu chuyện:</h3><p>Khoảng cách từ trạm vũ trụ đến vệ tinh là K km. Hãy đổi sang mét.</p><h4>Đầu vào:</h4><p>Một số nguyên K.</p><h4>Đầu ra:</h4><p>Khoảng cách tính theo mét (K * 1000).</p>",
                                Inputs = new[] { "3", "12", "1" },
                                Outputs = new[] { "3000", "12000", "1000" }
                            },
                            new {
                                Title = "Đếm Chân Thỏ Ngọc 🐇",
                                Diff = "Trung bình", Points = 150,
                                Story = "<h3>Câu chuyện:</h3><p>Trên mặt trăng có R chú thỏ ngọc. Hãy tính xem có tất cả bao nhiêu chiếc chân thỏ.</p><h4>Đầu vào:</h4><p>Một số nguyên R.</p><h4>Đầu ra:</h4><p>Tổng số chân thỏ (R * 4).</p>",
                                Inputs = new[] { "5", "10", "2" },
                                Outputs = new[] { "20", "40", "8" }
                            },
                            new {
                                Title = "Điểm Trung Bình Cộng 📊",
                                Diff = "Trung bình", Points = 150,
                                Story = "<h3>Câu chuyện:</h3><p>Cho hai số nguyên A và B. Hãy tính trung bình cộng (chia lấy nguyên) của hai số đó.</p><h4>Đầu vào:</h4><p>Hai số nguyên A và B trên 2 dòng.</p><h4>Đầu ra:</h4><p>Giá trị trung bình cộng (A + B) / 2.</p>",
                                Inputs = new[] { "6 8", "10 20", "7 9" },
                                Outputs = new[] { "7", "15", "8" }
                            },
                            new {
                                Title = "Năm Nhuận Vũ Trụ 📅",
                                Diff = "Khó", Points = 200,
                                Story = "<h3>Câu chuyện:</h3><p>Năm nhuận là năm chia hết cho 4. Hãy kiểm tra xem năm Y có phải năm nhuận không. Nếu có in 'Nhuan', ngược lại in 'Khong Nhuan'.</p><h4>Đầu vào:</h4><p>Một số nguyên dương Y.</p><h4>Đầu ra:</h4><p>'Nhuan' hoặc 'Khong Nhuan'.</p>",
                                Inputs = new[] { "2024", "2025", "2000" },
                                Outputs = new[] { "Nhuan", "Khong Nhuan", "Nhuan" }
                            }
                        }
                    },
                    new {
                        Title = "Vòng 5: Đấu Trường Siêu Cúp 🏆",
                        Description = "Vòng chung kết đầy kịch tính với các thử thách tư duy logic cao nhất.",
                        Problems = new[] {
                            new {
                                Title = "Đường Chạy Của Thỏ 🐇",
                                Diff = "Dễ", Points = 100,
                                Story = "<h3>Câu chuyện:</h3><p>Chú thỏ chạy vòng quanh sân hình chữ nhật có chiều rộng A và chiều dài gấp 3 lần chiều rộng. Tính chu vi sân.</p><h4>Đầu vào:</h4><p>Một số nguyên A.</p><h4>Đầu ra:</h4><p>Chu vi của sân chạy.</p>",
                                Inputs = new[] { "5", "10", "2" },
                                Outputs = new[] { "40", "80", "16" }
                            },
                            new {
                                Title = "Số Mũ Thần Kỳ 🎩",
                                Diff = "Dễ", Points = 100,
                                Story = "<h3>Câu chuyện:</h3><p>Tính giá trị 2 mũ N. Cho số nguyên N, hãy tính và in ra giá trị 2^N.</p><h4>Đầu vào:</h4><p>Một số nguyên N từ 1 đến 15.</p><h4>Đầu ra:</h4><p>Giá trị 2^N.</p>",
                                Inputs = new[] { "3", "5", "10" },
                                Outputs = new[] { "8", "32", "1024" }
                            },
                            new {
                                Title = "Đếm Số Lớn Hơn Trung Bình 📈",
                                Diff = "Trung bình", Points = 150,
                                Story = "<h3>Câu chuyện:</h3><p>Cho 3 số nguyên A, B, C. Tính trung bình cộng của 3 số (chia lấy nguyên), sau đó đếm xem có bao nhiêu số lớn hơn hoặc bằng giá trị trung bình cộng đó.</p><h4>Đầu vào:</h4><p>Ba số nguyên A, B, C trên 3 dòng.</p><h4>Đầu ra:</h4><p>Số lượng số lớn hơn hoặc bằng trung bình cộng.</p>",
                                Inputs = new[] { "5 5 8", "10 20 30", "1 9 10" },
                                Outputs = new[] { "1", "2", "2" }
                            },
                            new {
                                Title = "Tính Tổng Dãy Số 🔢",
                                Diff = "Trung bình", Points = 150,
                                Story = "<h3>Câu chuyện:</h3><p>Tính tổng các số tự nhiên liên tiếp từ 1 đến N (Tổng = 1 + 2 + ... + N).</p><h4>Đầu vào:</h4><p>Một số nguyên dương N.</p><h4>Đầu ra:</h4><p>Tổng thu được.</p>",
                                Inputs = new[] { "5", "10", "100" },
                                Outputs = new[] { "15", "55", "5050" }
                            },
                            new {
                                Title = "Mật Mã Đảo Ngược 🔑",
                                Diff = "Khó", Points = 200,
                                Story = "<h3>Câu chuyện:</h3><p>Bé nhận được một mật mã gồm 4 chữ số khác nhau và không chứa số 0. Để mở hòm kho báu, bé phải đảo ngược mật mã đó. Ví dụ 1234 đảo ngược thành 4321.</p><h4>Đầu vào:</h4><p>Một số nguyên gồm 4 chữ số khác nhau.</p><h4>Đầu ra:</h4><p>Mật mã sau khi đảo ngược.</p>",
                                Inputs = new[] { "1234", "5678", "1357" },
                                Outputs = new[] { "4321", "8765", "7531" }
                            }
                        }
                    }
                };

                foreach (var cData in contestsData)
                {
                    var contest = new Contest
                    {
                        Title = cData.Title,
                        Description = cData.Description,
                        StartTime = DateTime.UtcNow.AddHours(-1),
                        EndTime = DateTime.UtcNow.AddDays(7),
                        CurrentSession = initSessionName
                    };

                    context.Contests.Add(contest);
                    context.SaveChanges();

                    foreach (var pData in cData.Problems)
                    {
                        var problem = new Problem
                        {
                            ContestId = contest.Id,
                            Title = pData.Title,
                            Difficulty = pData.Diff,
                            Points = pData.Points,
                            StoryDescription = pData.Story,
                            TimeLimitMs = 2000,
                            MemoryLimitKb = 65536,
                            ImageUrl = ""
                        };

                        context.Problems.Add(problem);
                        context.SaveChanges();

                        for (int i = 0; i < pData.Inputs.Length; i++)
                        {
                            var testcase = new Testcase
                            {
                                ProblemId = problem.Id,
                                InputData = pData.Inputs[i].Replace(" ", "\n"),
                                ExpectedOutput = pData.Outputs[i],
                                IsSample = (i < 2)
                            };
                            context.Testcases.Add(testcase);
                        }
                    }
                }
                context.SaveChanges();
            }
        }
    }
}
