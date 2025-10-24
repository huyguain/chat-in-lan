# Secure LAN Chat System - Hướng dẫn cài đặt và chạy

## Yêu cầu hệ thống

### 1. .NET 6.0 SDK
- Tải về: https://dotnet.microsoft.com/download/dotnet/6.0
- Chọn **.NET 6.0 SDK** cho Windows
- Cài đặt và khởi động lại PowerShell

### 2. SQL Server LocalDB (Khuyến nghị)
- Cài đặt qua Visual Studio Installer
- Hoặc tải về: https://docs.microsoft.com/en-us/sql/database-engine/configure-windows/sql-server-express-localdb

### 3. Visual Studio Code (Tùy chọn)
- Để chỉnh sửa code: https://code.visualstudio.com/

## Cách chạy dự án

### Phương pháp 1: Sử dụng script tự động (Khuyến nghị)

1. **Mở PowerShell với quyền Administrator**
2. **Di chuyển đến thư mục dự án:**
   ```powershell
   cd C:\Users\admin\Documents\chat-in-lan\SecureLanChat
   ```

3. **Chạy script:**
   ```powershell
   .\run.bat
   ```

### Phương pháp 2: Chạy thủ công

1. **Mở PowerShell**
2. **Di chuyển đến thư mục Server:**
   ```powershell
   cd C:\Users\admin\Documents\chat-in-lan\SecureLanChat\src\Server
   ```

3. **Restore packages:**
   ```powershell
   dotnet restore
   ```

4. **Build project:**
   ```powershell
   dotnet build
   ```

5. **Tạo database:**
   ```powershell
   dotnet ef database update
   ```

6. **Chạy ứng dụng:**
   ```powershell
   dotnet run
   ```

## Truy cập ứng dụng

Sau khi chạy thành công, bạn có thể truy cập:

- **HTTPS:** https://localhost:5001
- **HTTP:** http://localhost:5000
- **Swagger API:** https://localhost:5001/swagger

## Tính năng chính

### 🔐 Bảo mật
- **End-to-end encryption** với RSA 2048-bit + AES 128-bit
- **Forward secrecy** - mỗi session có key riêng
- **Secure key exchange** giữa client và server
- **Session management** với timeout

### 💬 Chat Real-time
- **SignalR WebSocket** cho real-time messaging
- **Broadcast messages** cho tất cả users
- **Private messages** giữa 2 users
- **Online users tracking**
- **Message history** với encryption

### 🎨 Giao diện Web
- **Responsive design** cho mobile và desktop
- **Modern UI** với animations
- **Real-time notifications**
- **Encryption status indicators**

## Cấu trúc dự án

```
SecureLanChat/
├── src/
│   ├── Server/                 # ASP.NET Core Backend
│   │   ├── Controllers/        # API Controllers
│   │   ├── Hubs/              # SignalR Hubs
│   │   ├── Services/          # Business Logic
│   │   ├── Data/              # Entity Framework
│   │   └── Middleware/         # Custom Middleware
│   ├── Client/                # Frontend
│   │   └── wwwroot/           # Static Files
│   └── Shared/                # Shared Models
├── tests/                     # Unit Tests
└── docs/                      # Documentation
```

## API Endpoints

### Authentication
- `POST /api/auth/login` - Đăng nhập
- `POST /api/auth/register` - Đăng ký
- `POST /api/auth/logout` - Đăng xuất

### Users
- `GET /api/user/online` - Danh sách users online
- `GET /api/user/{id}` - Thông tin user
- `PUT /api/user/{id}/status` - Cập nhật trạng thái

### Health
- `GET /api/health` - Trạng thái hệ thống
- `GET /api/health/database` - Trạng thái database
- `GET /api/health/encryption` - Trạng thái encryption

## SignalR Hubs

### ChatHub
- `JoinChat(userId)` - Tham gia chat
- `LeaveChat(userId)` - Rời chat
- `SendMessage(senderId, receiverId, message)` - Gửi tin nhắn
- `ExchangeKeys(userId, publicKey)` - Trao đổi keys
- `GetMessageHistory(userId, otherUserId)` - Lịch sử tin nhắn

## Troubleshooting

### Lỗi thường gặp

1. **"dotnet not found"**
   - Cài đặt .NET 6.0 SDK
   - Khởi động lại PowerShell

2. **"Database connection failed"**
   - Cài đặt SQL Server LocalDB
   - Kiểm tra connection string trong appsettings.json

3. **"Port already in use"**
   - Thay đổi port trong launchSettings.json
   - Hoặc kill process đang sử dụng port

4. **"Migration failed"**
   - Xóa database cũ: `dotnet ef database drop`
   - Tạo lại: `dotnet ef database update`

### Logs
- Console logs: Hiển thị trong terminal
- File logs: `logs/log-YYYY-MM-DD.txt`
- Log levels: Information, Warning, Error

## Phát triển thêm

### Thêm tính năng mới
1. Tạo model trong `src/Shared/Models/`
2. Tạo service trong `src/Server/Services/`
3. Tạo controller trong `src/Server/Controllers/`
4. Viết tests trong `tests/UnitTests/`

### Database changes
1. Thay đổi model
2. Tạo migration: `dotnet ef migrations add MigrationName`
3. Cập nhật database: `dotnet ef database update`

## Hỗ trợ

Nếu gặp vấn đề, hãy kiểm tra:
1. .NET SDK đã cài đặt chưa
2. SQL Server LocalDB đã cài đặt chưa
3. Port 5000/5001 có bị chiếm không
4. Logs trong console và file

Chúc bạn sử dụng thành công! 🚀
