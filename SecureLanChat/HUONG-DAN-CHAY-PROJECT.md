# 📖 HƯỚNG DẪN CHI TIẾT CÁCH CHẠY PROJECT

## Mục lục
1. [Yêu cầu hệ thống](#1-yêu-cầu-hệ-thống)
2. [Cài đặt công cụ cần thiết](#2-cài-đặt-công-cụ-cần-thiết)
3. [Kiểm tra môi trường](#3-kiểm-tra-môi-trường)
4. [Cấu hình dự án](#4-cấu-hình-dự án)
5. [Các cách chạy dự án](#5-các-cách-chạy-dự-an)
6. [Truy cập ứng dụng](#6-truy-cập-ứng-dụng)
7. [Sử dụng ứng dụng](#7-sử-dụng-ứng-dụng)
8. [Xử lý lỗi thường gặp](#8-xử-lý-lỗi-thường-gặp)
9. [Cấu trúc thư mục](#9-cấu-trúc-thư-mục)

---

## 1. Yêu cầu hệ thống

### 1.1. Phần mềm bắt buộc
- **Windows 10/11** (hoặc Windows Server)
- **.NET 8.0 SDK** (bắt buộc)
- **SQL Server LocalDB** (hoặc SQL Server Express/Full)
- **PowerShell 5.1** trở lên (thường có sẵn trên Windows)

### 1.2. Phần mềm tùy chọn (khuyến nghị)
- **Visual Studio 2022** hoặc **Visual Studio Code** - để chỉnh sửa code
- **Git** - để quản lý phiên bản code
- **Trình duyệt web hiện đại** (Chrome, Edge, Firefox)

### 1.3. Yêu cầu phần cứng
- **RAM**: Tối thiểu 4GB (khuyến nghị 8GB)
- **Ổ cứng**: Tối thiểu 500MB dung lượng trống
- **Kết nối mạng**: Để test trong LAN

---

## 2. Cài đặt công cụ cần thiết

### 2.1. Cài đặt .NET 8.0 SDK

#### Bước 1: Tải .NET 8.0 SDK
1. Truy cập: https://dotnet.microsoft.com/download/dotnet/8.0
2. Chọn **.NET 8.0 SDK** (không phải Runtime)
3. Chọn phiên bản cho **Windows x64**
4. Tải file `.exe` installer

#### Bước 2: Cài đặt
1. Chạy file installer vừa tải
2. Chọn **Install** và chờ quá trình cài đặt hoàn tất
3. Nhấn **Close** khi cài đặt xong

#### Bước 3: Xác minh cài đặt
Mở **PowerShell** mới và chạy:
```powershell
dotnet --version
```
Kết quả mong đợi: `8.0.x` (ví dụ: `8.0.101`)

**Lưu ý**: 
- Nếu lệnh không hoạt động, khởi động lại PowerShell hoặc máy tính.
- Nếu hiển thị phiên bản thấp hơn 8.0, bạn cần cài đặt .NET 8.0 SDK

---

### 2.2. Cài đặt SQL Server LocalDB

SQL Server LocalDB là phiên bản nhẹ của SQL Server, phù hợp cho development.

#### Cách 1: Cài qua Visual Studio (Khuyến nghị)
1. Tải **Visual Studio Installer**
2. Chọn **Modify** cho Visual Studio của bạn
3. Trong tab **Individual components**, tìm và chọn:
   - **SQL Server Express LocalDB**
4. Nhấn **Modify** để cài đặt

#### Cách 2: Tải trực tiếp
1. Truy cập: https://docs.microsoft.com/en-us/sql/database-engine/configure-windows/sql-server-express-localdb
2. Tải **SQL Server Express LocalDB**
3. Cài đặt theo hướng dẫn

#### Cách 3: Sử dụng SQL Server Express
Nếu bạn đã có SQL Server Express hoặc Full, có thể sử dụng luôn.

**Xác minh cài đặt**:
```powershell
sqllocaldb info
```
Hoặc kiểm tra trong **SQL Server Management Studio (SSMS)**.

---

## 3. Kiểm tra môi trường

Sau khi cài đặt, kiểm tra tất cả công cụ:

```powershell
# Kiểm tra .NET SDK
dotnet --version

# Kiểm tra Entity Framework tools
dotnet ef --version

# Nếu lệnh trên báo lỗi, cài đặt EF tools:
dotnet tool install --global dotnet-ef

# Kiểm tra SQL Server LocalDB (nếu đã cài)
sqllocaldb info
```

---

## 4. Cấu hình dự án

### 4.1. Kiểm tra Connection String

Mở file: `SecureLanChat\src\Server\appsettings.json`

Kiểm tra connection string:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=SecureLanChat;Trusted_Connection=true;MultipleActiveResultSets=true"
  }
}
```

**Giải thích**:
- `(localdb)\\mssqllocaldb`: SQL Server LocalDB instance mặc định
- `SecureLanChat`: Tên database sẽ được tạo tự động
- `Trusted_Connection=true`: Sử dụng Windows Authentication

**Nếu bạn dùng SQL Server khác**, thay đổi connection string:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost\\SQLEXPRESS;Database=SecureLanChat;Trusted_Connection=true;"
  }
}
```

### 4.2. Kiểm tra Port (nếu cần)

Mở file: `SecureLanChat\src\Server\Properties\launchSettings.json`

Các port mặc định:
- **HTTP**: `5000`
- **HTTPS**: `7000` (trong profile https) hoặc `5001` (trong một số cấu hình)

Nếu port bị chiếm, bạn có thể đổi trong file này.

---

## 5. Các cách chạy dự án

### 5.1. Cách 1: Sử dụng PowerShell Script (Khuyến nghị - Dễ nhất) ⭐

Đây là cách dễ nhất và được khuyến nghị nhất.

#### Bước 1: Mở PowerShell
- Nhấn `Win + X`
- Chọn **Windows PowerShell** hoặc **Terminal**
- Hoặc tìm "PowerShell" trong Start Menu

#### Bước 2: Di chuyển đến thư mục dự án
```powershell
cd E:\Documents\Frontend\chat-in-lan\SecureLanChat
```
*(Thay đường dẫn bằng đường dẫn thực tế của bạn)*

#### Bước 3: Chạy script
```powershell
.\run.ps1
```

**Script sẽ tự động**:
1. ✅ Kiểm tra .NET SDK
2. ✅ Restore các package NuGet
3. ✅ Build project
4. ✅ Tạo/cập nhật database
5. ✅ Khởi động server

#### Bước 4: Chờ server khởi động
Bạn sẽ thấy thông báo:
```
Starting Secure LAN Chat Server...
Server will be available at:
  • HTTPS: https://localhost:7000
  • HTTP:  http://localhost:5000
  • Swagger: https://localhost:7000/swagger
```

---

### 5.2. Cách 2: Sử dụng Batch File

Phù hợp nếu bạn muốn chạy từ Command Prompt.

#### Bước 1: Mở Command Prompt
- Nhấn `Win + R`
- Gõ `cmd` và nhấn Enter

#### Bước 2: Di chuyển đến thư mục dự án
```cmd
cd E:\Documents\Frontend\chat-in-lan\SecureLanChat
```

#### Bước 3: Chạy file batch
```cmd
run.bat
```

Hoặc double-click vào file `run.bat` trong File Explorer.

---

### 5.3. Cách 3: Chạy thủ công từng bước (Nâng cao)

Phù hợp nếu bạn muốn hiểu rõ từng bước hoặc gặp lỗi với script.

#### Bước 1: Mở PowerShell
```powershell
cd E:\Documents\Frontend\chat-in-lan\SecureLanChat\src\Server
```

#### Bước 2: Restore packages
```powershell
dotnet restore
```
**Thời gian**: 1-3 phút (lần đầu), các lần sau nhanh hơn.

**Kết quả mong đợi**:
```
  Restore completed in X.XX sec for E:\...\SecureLanChat.csproj.
```

#### Bước 3: Build project
```powershell
dotnet build
```

**Kết quả mong đợi**:
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

#### Bước 4: Tạo database
```powershell
dotnet ef database update
```

**Kết quả mong đợi**:
```
Applying migration 'XXXXXXXX_InitialCreate'.
Done.
```

**Lưu ý**: Nếu lần đầu chạy và chưa có migration, có thể bỏ qua bước này. Database sẽ được tạo tự động khi chạy ứng dụng.

#### Bước 5: Chạy ứng dụng
```powershell
dotnet run
```

**Kết quả mong đợi**:
```
info: SecureLanChat.Program[0]
      Starting Secure LAN Chat System
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:7000
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

**Để dừng server**: Nhấn `Ctrl + C`

---

### 5.4. Cách 4: Chạy từ Visual Studio

1. Mở file `SecureLanChat.sln` trong Visual Studio
2. Nhấn `F5` hoặc chọn **Debug > Start Debugging**
3. Hoặc nhấn `Ctrl + F5` để chạy không debug

Visual Studio sẽ tự động:
- Restore packages
- Build project
- Tạo database (nếu cần)
- Khởi động server
- Mở browser tự động

---

## 6. Truy cập ứng dụng

Sau khi server khởi động thành công, bạn có thể truy cập:

### 6.1. Giao diện Web Chat (Ứng dụng chính)
- **HTTPS**: https://localhost:7000
- **HTTP**: http://localhost:5000

**Lưu ý**: Lần đầu truy cập HTTPS, trình duyệt có thể cảnh báo về certificate không được tin cậy. Đây là bình thường trong môi trường development. Chọn **Advanced** > **Proceed to localhost**.

### 6.2. Swagger API Documentation
- **URL**: https://localhost:7000/swagger
- **Mục đích**: Xem và test các API endpoints
- **Tính năng**: 
  - Xem tất cả API
  - Test API trực tiếp
  - Xem schema của request/response

### 6.3. Health Check Endpoint
- **URL**: https://localhost:7000/health
- **Mục đích**: Kiểm tra trạng thái hệ thống
- **Response**: JSON với thông tin về database, encryption, etc.

### 6.4. SignalR Hub Endpoint
- **URL**: https://localhost:7000/chathub
- **Mục đích**: WebSocket endpoint cho real-time chat
- **Lưu ý**: Không truy cập trực tiếp bằng browser, được sử dụng tự động bởi JavaScript client

---

## 7. Sử dụng ứng dụng

### 7.1. Đăng ký tài khoản mới

1. Mở browser và truy cập: https://localhost:7000
2. Tìm phần **Register** hoặc **Đăng ký**
3. Điền thông tin:
   - Username: (ví dụ: `user1`)
   - Password: (ví dụ: `password123`)
4. Nhấn **Register**
5. Hệ thống sẽ tự động:
   - Tạo tài khoản
   - Tạo RSA key pair
   - Đăng nhập tự động

### 7.2. Đăng nhập

1. Nếu đã có tài khoản, điền thông tin:
   - Username
   - Password
2. Nhấn **Login** hoặc **Đăng nhập**

### 7.3. Sử dụng test accounts

Hệ thống tự động tạo 4 tài khoản test khi khởi động:
- Username: `admin`, Password: `admin123`
- Username: `user1`, Password: `user123`
- Username: `user2`, Password: `user123`
- Username: `testuser`, Password: `test123`

### 7.4. Chat với người dùng khác

1. **Xem danh sách người online**: Trong giao diện chat, sẽ hiển thị danh sách users đang online
2. **Gửi tin nhắn broadcast**: Gửi tin nhắn cho tất cả mọi người
3. **Gửi tin nhắn private**: Chọn một user và gửi tin nhắn riêng
4. **Xem lịch sử tin nhắn**: Tin nhắn cũ sẽ được hiển thị và giải mã tự động

### 7.5. Test với nhiều người dùng

Để test tính năng chat:

1. **Mở 2 tab browser** (hoặc 2 browser khác nhau)
2. **Tab 1**: Đăng nhập với `user1`
3. **Tab 2**: Đăng nhập với `user2`
4. **Gửi tin nhắn** giữa 2 tab
5. Quan sát:
   - Tin nhắn hiển thị real-time
   - Encryption status (🔒 locked icon)
   - Message history

---

## 8. Xử lý lỗi thường gặp

### 8.1. Lỗi: "dotnet not found" hoặc "dotnet : The term 'dotnet' is not recognized"

**Nguyên nhân**: .NET SDK chưa được cài đặt hoặc chưa có trong PATH.

**Giải pháp**:
1. Cài đặt .NET 8.0 SDK (xem mục 2.1)
2. **Khởi động lại PowerShell** (quan trọng!)
3. Chạy lại: `dotnet --version`
4. Nếu vẫn lỗi, thử khởi động lại máy tính

---

### 8.2. Lỗi: "Unable to connect to database" hoặc "A network-related or instance-specific error occurred"

**Nguyên nhân**: 
- SQL Server LocalDB chưa được cài đặt
- SQL Server service chưa chạy
- Connection string sai

**Giải pháp**:

**Bước 1**: Kiểm tra SQL Server LocalDB
```powershell
sqllocaldb info
```

**Bước 2**: Nếu chưa có, cài đặt SQL Server LocalDB (xem mục 2.2)

**Bước 3**: Nếu dùng SQL Server Express/Full, kiểm tra service:
```powershell
# Kiểm tra SQL Server service
Get-Service | Where-Object {$_.Name -like "*SQL*"}

# Khởi động SQL Server service (nếu cần)
Start-Service MSSQLSERVER
```

**Bước 4**: Kiểm tra connection string trong `appsettings.json`

**Bước 5**: Thử đổi connection string sang SQLite (tạm thời):
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=chat.db"
  }
}
```
*(Cần thay đổi trong Program.cs để dùng SQLite thay vì SQL Server)*

---

### 8.3. Lỗi: "Port 5000 is already in use" hoặc "Address already in use"

**Nguyên nhân**: Port 5000 hoặc 7000 đang được sử dụng bởi ứng dụng khác.

**Giải pháp**:

**Cách 1**: Tìm và kill process đang dùng port
```powershell
# Tìm process đang dùng port 5000
netstat -ano | findstr :5000

# Lấy PID (số ở cột cuối) và kill
taskkill /PID <PID> /F
```

**Cách 2**: Đổi port trong `launchSettings.json`
```json
{
  "profiles": {
    "https": {
      "applicationUrl": "https://localhost:8000;http://localhost:6000"
    }
  }
}
```

---

### 8.4. Lỗi: "Unable to create an object of type 'ChatDbContext'"

**Nguyên nhân**: 
- Entity Framework tools chưa được cài đặt
- Migration chưa được tạo

**Giải pháp**:

**Bước 1**: Cài đặt EF tools
```powershell
dotnet tool install --global dotnet-ef
```

**Bước 2**: Tạo migration (nếu chưa có)
```powershell
cd src\Server
dotnet ef migrations add InitialCreate
```

**Bước 3**: Cập nhật database
```powershell
dotnet ef database update
```

**Lưu ý**: Nếu đã có database và muốn tạo lại:
```powershell
dotnet ef database drop --force
dotnet ef database update
```

---

### 8.5. Lỗi: "Failed to restore packages" hoặc "NU1101"

**Nguyên nhân**: 
- Mất kết nối internet
- NuGet source bị lỗi

**Giải pháp**:

**Bước 1**: Kiểm tra kết nối internet

**Bước 2**: Clear NuGet cache
```powershell
dotnet nuget locals all --clear
```

**Bước 3**: Restore lại
```powershell
dotnet restore --force
```

---

### 8.6. Lỗi: "Certificate error" khi truy cập HTTPS

**Nguyên nhân**: Development certificate chưa được tin cậy.

**Giải pháp**:

**Cách 1**: Trust development certificate
```powershell
dotnet dev-certs https --trust
```

**Cách 2**: Trong browser, chọn **Advanced** > **Proceed to localhost** (chỉ cho development)

---

### 8.7. Lỗi: "Build failed" với nhiều warnings

**Nguyên nhân**: 
- Code có warnings (thường không ảnh hưởng chạy)
- Thiếu dependencies

**Giải pháp**:

**Bước 1**: Xem chi tiết lỗi
```powershell
dotnet build --verbosity detailed
```

**Bước 2**: Nếu chỉ là warnings, có thể bỏ qua và chạy tiếp
```powershell
dotnet run
```

**Bước 3**: Nếu là errors, kiểm tra:
- Thiếu package? → `dotnet restore`
- Code syntax error? → Sửa code

---

### 8.8. Lỗi: "Database migration failed"

**Giải pháp**:

**Bước 1**: Xóa database cũ (nếu cần)
```powershell
cd src\Server
dotnet ef database drop --force
```

**Bước 2**: Xóa migrations cũ (nếu cần)
```powershell
# Xóa thư mục Migrations trong src/Server (nếu có)
```

**Bước 3**: Tạo lại migration
```powershell
dotnet ef migrations add InitialCreate
```

**Bước 4**: Cập nhật database
```powershell
dotnet ef database update
```

---

## 9. Cấu trúc thư mục

```
SecureLanChat/
├── src/
│   ├── Server/                 # Backend ASP.NET Core
│   │   ├── Controllers/        # API Controllers (Auth, User, Health)
│   │   ├── Hubs/              # SignalR Hubs (ChatHub)
│   │   ├── Services/          # Business Logic Services
│   │   ├── Data/              # Entity Framework DbContext
│   │   ├── Models/            # Data Models
│   │   ├── Middleware/        # Custom Middleware
│   │   ├── Program.cs         # Entry point
│   │   └── appsettings.json   # Cấu hình
│   ├── Client/                # Frontend
│   │   └── wwwroot/           # Static files (HTML, CSS, JS)
│   └── Shared/                # Shared code
│       ├── Models/            # Shared Models
│       └── Interfaces/        # Interfaces
├── tests/                     # Unit tests và Integration tests
├── docs/                      # Documentation
├── run.bat                    # Batch script để chạy
├── run.ps1                    # PowerShell script để chạy
└── README.md                  # Tài liệu tổng quan
```

---

## 10. Tips và Best Practices

### 10.1. Development
- **Luôn chạy `dotnet restore`** sau khi clone/pull code mới
- **Kiểm tra logs** trong console để debug
- **Sử dụng Swagger** để test API nhanh
- **Xem file logs** trong thư mục `logs/` (nếu có)

### 10.2. Production
- **Đổi connection string** sang production database
- **Bật HTTPS** và sử dụng certificate thật
- **Cấu hình logging** phù hợp
- **Thiết lập firewall** cho port
- **Backup database** định kỳ

### 10.3. Testing
- **Test với nhiều users** để kiểm tra real-time
- **Test encryption** bằng cách xem database (messages phải được mã hóa)
- **Test message history** sau khi logout/login lại

---

## 11. Liên hệ và hỗ trợ

Nếu gặp vấn đề không giải quyết được:

1. **Kiểm tra logs**:
   - Console output khi chạy server
   - File logs trong thư mục `logs/` (nếu có)
   - Browser console (F12)

2. **Kiểm tra các file README khác**:
   - `README.md` - Tổng quan
   - `QUICK-START.md` - Hướng dẫn nhanh
   - `README-SETUP.md` - Hướng dẫn cài đặt
   - `docs/` - Tài liệu chi tiết từng module

3. **Kiểm tra issues** trong repository (nếu có)

---

## 12. Tóm tắt nhanh (Quick Reference)

```powershell
# 1. Kiểm tra .NET
dotnet --version

# 2. Di chuyển đến thư mục dự án
cd E:\Documents\Frontend\chat-in-lan\SecureLanChat

# 3. Chạy script (Cách dễ nhất)
.\run.ps1

# HOẶC chạy thủ công:
cd src\Server
dotnet restore
dotnet build
dotnet ef database update  # (nếu cần)
dotnet run

# 4. Truy cập ứng dụng
# - Web: https://localhost:7000
# - Swagger: https://localhost:7000/swagger
# - Health: https://localhost:7000/health

# 5. Dừng server
# Nhấn Ctrl+C trong terminal
```

---

**Chúc bạn chạy project thành công! 🚀**

Nếu có vấn đề, hãy kiểm tra phần [Xử lý lỗi thường gặp](#8-xử-lý-lỗi-thường-gặp) hoặc các file README khác trong project.

