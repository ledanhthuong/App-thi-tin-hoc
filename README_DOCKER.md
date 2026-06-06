# Hướng dẫn sử dụng Docker cho dự án App Thi Tin Học (CodeKids)

Dự án này là ứng dụng ASP.NET Core 10.0 Web MVC hỗ trợ làm bài và chấm bài trực tuyến cho học sinh. Khi chạy trên Docker, hệ thống tự động thiết lập môi trường chấm bài bao gồm trình biên dịch **C++ (g++)** và **Python 3** bên trong container Linux để chấm bài thi.

---

## 📌 Các tính năng chính của cấu hình Docker
1. **Multi-stage Build**: Giúp giảm dung lượng ảnh Docker chạy thực tế (chỉ chứa runtime của .NET 10, g++ và Python 3).
2. **Auto-seeding**: Khi ứng dụng khởi chạy lần đầu trên database mới, hệ thống tự động khởi tạo bảng và seed sẵn dữ liệu (tài khoản `superadmin`, danh sách giáo viên, học sinh, và 5 vòng thi mẫu kèm bộ testcase).
3. **Bảo mật**: Chạy ứng dụng dưới user bảo mật `app` thay vì quyền `root`.
4. **Không cần cài đặt thủ công**: Không cần chạy file `setup_env.ps1` hoặc sao chép thư mục `Compiler` của Windows vào container, Docker sẽ tự động cài đặt `g++` và `python3` từ kho ứng dụng Linux.

---

## 🛠️ Yêu cầu hệ thống
- Đã cài đặt [Docker Desktop](https://www.docker.com/products/docker-desktop/) trên máy tính của bạn.
- Docker Desktop đang hoạt động (Đang chạy ở chế độ Linux Containers - mặc định).

---

## 🚀 Hướng dẫn khởi chạy nhanh

Mở Terminal (CMD / PowerShell / Git Bash) tại thư mục gốc của dự án và chạy lệnh:

```bash
docker compose up --build -d
```

- `-d`: Chạy dưới nền (detached mode).
- `--build`: Tiến hành build lại Docker image từ mã nguồn mới nhất.

Sau khi các container khởi chạy thành công, truy cập ứng dụng tại địa chỉ:
👉 **[http://localhost:8080](http://localhost:8080)**

---

## 🔑 Tài khoản đăng nhập mẫu
Sau khi hệ thống khởi tạo cơ sở dữ liệu, bạn có thể đăng nhập bằng các tài khoản mẫu sau:

*   **Tài khoản Quản trị tối cao (Super Admin)**:
    *   Username: `superadmin`
    *   Password: `admin123`
*   **Tài khoản Giáo viên**:
    *   Username: `giauviem1`
    *   Password: `123456`
*   **Tài khoản Học sinh**:
    *   Username: `hocsinh1` (hoặc `hocsinh2`, `hocsinh3`)
    *   Password: `123456`

---

## 💾 Cấu hình Cơ sở dữ liệu (Database)

Có hai lựa chọn để cấu hình database trong file [docker-compose.yml](file:///d:/App%20thi%20tin%20hoc/docker-compose.yml):

### Lựa chọn 1: Sử dụng SQL Server chạy cục bộ trong Docker (Mặc định)
File `docker-compose.yml` đã được định nghĩa sẵn một container chạy SQL Server (`db`).
- **Connection String mặc định**: Kết nối trực tiếp đến container `db` trong cùng mạng Docker.
- **Dữ liệu được lưu trữ**: Được ánh xạ ra ổ đĩa máy ảo thông qua Docker Volume `mssql-data` để không bị mất khi bạn tắt container.

### Lựa chọn 2: Sử dụng SQL Server Cloud / Bên ngoài (Ví dụ: databaseasp.net)
Nếu bạn muốn tiếp tục sử dụng database từ xa sẵn có của mình:
1. Mở file [docker-compose.yml](file:///d:/App%20thi%20tin%20hoc/docker-compose.yml).
2. Tìm đến phần cấu hình `environment` của service `web`.
3. Bỏ comment dòng cấu hình chuỗi kết nối từ xa và điền thông tin của bạn vào:
   ```yaml
   # Lựa chọn 2:
   - ConnectionStrings__DefaultConnection=Server=db54959.databaseasp.net;Database=db54959;User Id=db54959;Password=thuong0909;TrustServerCertificate=True;Encrypt=False;MultipleActiveResultSets=True
   ```
4. Bạn có thể xóa hoàn toàn service `db` ở phía dưới để giảm tải bộ nhớ nếu chỉ dùng database ngoài.

---

## 🔍 Giải thích cơ chế hoạt động của trình chấm bài trong Docker

Hệ thống chấm bài tự động hoạt động như sau khi chạy trong Docker:
1. **Biên dịch C++**:
   - Khi học sinh gửi bài C++, `JudgingService` sẽ tạo file tạm `.cpp` trong thư mục `/tmp/CodeKidsTempSubmissions`.
   - Hệ thống gọi lệnh `g++` (được cài đặt trong môi trường Linux của Docker) để biên dịch sang file chạy.
   - Chạy file thực thi để kiểm tra kết quả đối chiếu với input/output mẫu của testcase.
2. **Chạy Python**:
   - Khi học sinh gửi bài Python, `JudgingService` lưu file `.py` tạm thời.
   - Hệ thống gọi lệnh `python` (được trỏ liên kết đến `python3` thông qua thư viện `python-is-python3`) để thực thi mã nguồn và so khớp kết quả testcase.

---

## 📋 Các lệnh Docker hữu ích

*   **Xem logs ứng dụng** (Để xem quá trình chạy hoặc gỡ lỗi):
    ```bash
    docker compose logs -f web
    ```
*   **Dừng các container**:
    ```bash
    docker compose down
    ```
*   **Xóa toàn bộ dữ liệu database và cấu hình lại từ đầu (Reset Database)**:
    ```bash
    docker compose down -v
    ```
    *(Lưu ý: Lệnh này sẽ xóa hoàn toàn ổ đĩa ảo `mssql-data` chứa database cục bộ của SQL Server)*.
