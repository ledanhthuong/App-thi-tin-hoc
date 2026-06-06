# Script tự động cài đặt và cấu hình Python & C++ (g++) cho CodeKids
# Yêu cầu chạy bằng quyền Administrator

$ErrorActionPreference = "Stop"
Write-Host "=== ĐANG CẤU HÌNH MÔI TRƯỜNG CHẤM BÀI CHO CODEKIDS ===" -ForegroundColor Cyan

# 1. Cài đặt Python 3
Write-Host "`n1. Đang tải và cài đặt Python 3..." -ForegroundColor Yellow
$pythonUrl = "https://www.python.org/ftp/python/3.10.11/python-3.10.11-amd64.exe"
$pythonPath = "$env:TEMP\python-installer.exe"
Write-Host "Đang tải bộ cài từ python.org..." -ForegroundColor Gray
Invoke-WebRequest -Uri $pythonUrl -OutFile $pythonPath
Write-Host "Đang chạy cài đặt Python ngầm (tự động thêm vào PATH)..." -ForegroundColor Gray
Start-Process -FilePath $pythonPath -ArgumentList "/quiet InstallAllUsers=1 PrependPath=1" -Wait
Write-Host "✅ Cài đặt Python thành công!" -ForegroundColor Green

# 2. Cài đặt MinGW (g++)
Write-Host "`n2. Đang tải trình biên dịch C++ (MinGW)..." -ForegroundColor Yellow
$mingwUrl = "https://github.com/brechtsanders/winlibs_mingw/releases/download/13.2.0posix-16.0.6-11.0.1-ucrt-r1/winlibs-x86-64-posix-seh-gcc-13.2.0-llvm-16.0.6-mingw-w64ucrt-11.0.1-r1.zip"
$mingwZip = "$env:TEMP\mingw.zip"
Write-Host "Đang tải file nén compiler g++ (khoảng 100MB)..." -ForegroundColor Gray
Invoke-WebRequest -Uri $mingwUrl -OutFile $mingwZip
Write-Host "Đang giải nén MinGW vào thư mục C:\..." -ForegroundColor Gray
if (-not (Test-Path "C:\mingw64")) {
    Expand-Archive -Path $mingwZip -DestinationPath "C:\" -Force
}
Write-Host "✅ Đã giải nén MinGW thành công!" -ForegroundColor Green

# 3. Cấu hình biến môi trường PATH cho g++
Write-Host "`n3. Cấu hình biến môi trường PATH..." -ForegroundColor Yellow
$userPath = [System.Environment]::GetEnvironmentVariable("Path", "User")
if ($userPath -notlike "*C:\mingw64\bin*") {
    $newPath = $userPath + ";C:\mingw64\bin"
    [System.Environment]::SetEnvironmentVariable("Path", $newPath, "User")
    Write-Host "✅ Đã thêm C:\mingw64\bin vào biến môi trường PATH!" -ForegroundColor Green
} else {
    Write-Host "✅ Đường dẫn PATH g++ đã được cấu hình trước đó." -ForegroundColor Green
}

# 4. Tắt Aliases của Windows App Store cho Python (Sửa lỗi Windows tự mở Microsoft Store khi gõ python)
Write-Host "`n4. Vô hiệu hóa lối tắt App Store của Python..." -ForegroundColor Yellow
$appAppsDir = "$env:USERPROFILE\AppData\Local\Microsoft\WindowsApps"
if (Test-Path "$appAppsDir\python.exe") {
    Remove-Item -Path "$appAppsDir\python.exe" -Force -ErrorAction SilentlyContinue
}
if (Test-Path "$appAppsDir\python3.exe") {
    Remove-Item -Path "$appAppsDir\python3.exe" -Force -ErrorAction SilentlyContinue
}
Write-Host "✅ Đã vô hiệu hóa lối tắt Windows Store thành công!" -ForegroundColor Green

Write-Host "`n=== ĐÃ HOÀN THÀNH! VUI LÒNG MỞ LẠI CỬA SỔ CMD/POWERSHELL MỚI ===" -ForegroundColor Green
