# 🚀 QUICK START - Secure LAN Chat System

## Bước 1: Cài đặt .NET 6.0 SDK

1. **Tải về .NET 6.0 SDK:**
   - Truy cập: https://dotnet.microsoft.com/download/dotnet/6.0
   - Tải về **.NET 6.0 SDK** cho Windows
   - Chạy file installer

2. **Khởi động lại PowerShell**

## Bước 2: Chạy dự án

### Cách 1: Sử dụng PowerShell Script (Khuyến nghị)

```powershell
# Mở PowerShell với quyền Administrator
cd C:\Users\admin\Documents\chat-in-lan\SecureLanChat

# Chạy script
.\run.ps1
```

### Cách 2: Sử dụng Batch File

```cmd
# Mở Command Prompt
cd C:\Users\admin\Documents\chat-in-lan\SecureLanChat

# Chạy script
run.bat
```

### Cách 3: Chạy thủ công

```powershell
# Di chuyển đến thư mục Server
cd src\Server

# Restore packages
dotnet restore

# Build project
dotnet build

# Tạo database
dotnet ef database update

# Chạy ứng dụng
dotnet run
```

## Bước 3: Truy cập ứng dụng

Sau khi chạy thành công, mở trình duyệt và truy cập:

- **🌐 Web App:** https://localhost:5001
- **📚 API Docs:** https://localhost:5001/swagger
- **🔧 Health Check:** https://localhost:5001/api/health

## Bước 4: Sử dụng

1. **Đăng ký tài khoản mới** hoặc **đăng nhập**
2. **Chờ kết nối** và **trao đổi encryption keys**
3. **Bắt đầu chat** với các users khác
4. **Tất cả tin nhắn đều được mã hóa end-to-end**

## 🔧 Troubleshooting

### Lỗi "dotnet not found"
- Cài đặt .NET 6.0 SDK
- Khởi động lại PowerShell

### Lỗi "Database connection failed"
- Cài đặt SQL Server LocalDB
- Hoặc thay đổi connection string trong `appsettings.json`

### Lỗi "Port already in use"
- Thay đổi port trong `launchSettings.json`
- Hoặc kill process: `netstat -ano | findstr :5000`

## 📱 Tính năng

- ✅ **End-to-end encryption** (RSA + AES)
- ✅ **Real-time messaging** (SignalR)
- ✅ **Online users tracking**
- ✅ **Message history**
- ✅ **Responsive web interface**
- ✅ **Secure key exchange**

## 🎯 Demo

1. Mở 2 tab trình duyệt
2. Đăng ký 2 tài khoản khác nhau
3. Gửi tin nhắn giữa 2 tài khoản
4. Quan sát encryption status
5. Kiểm tra message history

**Chúc bạn sử dụng thành công! 🎉**
