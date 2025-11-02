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

📖 **Xem hướng dẫn chi tiết**: [HUONG-DAN-CHAY-PROJECT.md](./HUONG-DAN-CHAY-PROJECT.md)

### Cách nhanh nhất:

1. Cài đặt .NET 6.0 SDK từ https://dotnet.microsoft.com/download/dotnet/6.0
2. Mở PowerShell và chạy:
   ```powershell
   cd SecureLanChat
   .\run.ps1
   ```
3. Truy cập: https://localhost:7000

### Các tài liệu khác:
- [QUICK-START.md](./QUICK-START.md) - Hướng dẫn nhanh
- [README-SETUP.md](./README-SETUP.md) - Hướng dẫn cài đặt chi tiết
- [HUONG-DAN-CHAY-PROJECT.md](./HUONG-DAN-CHAY-PROJECT.md) - **Hướng dẫn đầy đủ nhất** ⭐

## API Documentation

Swagger UI có sẵn tại: `https://localhost:7000/swagger`

## Health Check

Health check endpoint: `https://localhost:7000/health`
