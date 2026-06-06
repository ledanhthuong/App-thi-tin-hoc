# Stage 1: Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Sao chép file project và restore các dependencies trước
COPY ["App thi tin hoc.csproj", "./"]
RUN dotnet restore "./App thi tin hoc.csproj"

# Sao chép toàn bộ mã nguồn còn lại (loại trừ các file trong .dockerignore) và build
COPY . .
RUN dotnet publish "App thi tin hoc.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Chuyển sang root để cài đặt các trình biên dịch
USER root

# Cài đặt trình biên dịch C++ (g++) và Python 3 để chấm bài trực tuyến
RUN apt-get update && apt-get install -y --no-install-recommends \
    build-essential \
    python3 \
    python-is-python3 \
    && rm -rf /var/lib/apt/lists/*

# Sao chép kết quả build từ Stage 1
COPY --from=build /app/publish .

# Cấp quyền cho thư mục ứng dụng cho user 'app' (user mặc định của .NET 8+)
RUN chown -R app:app /app

# Khởi chạy ứng dụng dưới user bảo mật 'app' thay vì root
USER app

ENTRYPOINT ["dotnet", "App thi tin hoc.dll"]
