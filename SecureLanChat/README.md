# Secure LAN Chat System

Hệ thống chat mã hóa end-to-end cho mạng LAN với bảo mật cao và hiệu suất tối ưu.

## Tính năng chính

- **Mã hóa End-to-End**: AES-128 + RSA 2048-bit
- **Real-time Communication**: SignalR WebSocket
- **User Management**: Đăng nhập, danh sách online
- **Message History**: Lưu trữ và tìm kiếm tin nhắn
- **Web Interface**: Responsive design với dark/light theme
- **High Performance**: Hỗ trợ 100+ người dùng đồng thời

## Công nghệ sử dụng

- **Backend**: ASP.NET Core 6.0, SignalR, Entity Framework Core
- **Frontend**: HTML5, CSS3, JavaScript ES6+
- **Database**: SQL Server / SQLite
- **Mã hóa**: System.Security.Cryptography
- **Logging**: Serilog

## Cấu trúc project

```
SecureLanChat/
├── src/
│   ├── Server/          # ASP.NET Core API
│   ├── Client/          # Web frontend
│   └── Shared/          # Shared models và interfaces
├── tests/               # Unit tests và integration tests
└── docs/                # Documentation
```

## Cài đặt và chạy

1. Cài đặt .NET 6.0 SDK
2. Clone repository
3. Cấu hình connection string trong appsettings.json
4. Chạy migrations: `dotnet ef database update`
5. Chạy ứng dụng: `dotnet run`

## API Documentation

Swagger UI có sẵn tại: `https://localhost:7000/swagger`

## Health Check

Health check endpoint: `https://localhost:7000/health`
