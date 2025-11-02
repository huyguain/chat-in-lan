# 🚀 HƯỚNG DẪN NHANH - Cách chạy project

## 📋 Yêu cầu trước khi chạy

1. **Cài .NET 8.0 SDK** (hoặc cao hơn - project đã được upgrade)
   - Tải: https://dotnet.microsoft.com/download/dotnet/8.0
   - Chọn **.NET 8.0 SDK** cho Windows
   - Hoặc nếu đã có .NET 9.0 thì cũng được (tương thích ngược)
   - Cài đặt và khởi động lại PowerShell

2. **Kiểm tra .NET đã cài:**
   ```powershell
   dotnet --version
   ```
   Kết quả phải là: `8.0.x` hoặc `9.0.x` (project yêu cầu .NET 8.0+)

3. **Cài SQL Server LocalDB** (nếu chưa có)
   - Thường có sẵn khi cài Visual Studio
   - Hoặc tải: https://go.microsoft.com/fwlink/?LinkID=799012

---

## 🎯 CÁCH CHẠY PROJECT (3 phương pháp)

### ⭐ Cách 1: Dùng Script (NHANH NHẤT - Khuyến nghị)

**Bước 1:** Mở PowerShell trong thư mục project:
```powershell
cd E:\Documents\Frontend\chat-in-lan\SecureLanChat
```

**Bước 2:** Chạy một trong hai script sau:

**Option A - PowerShell:**
```powershell
.\run.ps1
```

**Option B - Batch File:**
```cmd
.\run.bat
```

Script sẽ tự động:
- ✅ Kiểm tra .NET SDK
- ✅ Restore packages
- ✅ Build project
- ✅ Tạo database
- ✅ Chạy server

---

### 📝 Cách 2: Chạy thủ công (step-by-step)

**Bước 1:** Mở PowerShell, di chuyển đến thư mục Server:
```powershell
cd E:\Documents\Frontend\chat-in-lan\SecureLanChat\src\Server
```

**Bước 2:** Restore packages:
```powershell
dotnet restore
```

**Bước 3:** Build project:
```powershell
dotnet build
```

**Bước 4:** Tạo database (nếu chưa có):
```powershell
dotnet ef database update
```

**Lưu ý:** Nếu lỗi "dotnet-ef not found", cài EF tools:
```powershell
dotnet tool install --global dotnet-ef
```

**Bước 5:** Chạy server:
```powershell
dotnet run
```

---

### 🔧 Cách 3: Dùng Visual Studio / VS Code

**Visual Studio:**
1. Mở file `SecureLanChat.sln`
2. Nhấn `F5` hoặc click "Run"

**VS Code:**
1. Mở thư mục `SecureLanChat`
2. Nhấn `F5` hoặc chạy terminal: `dotnet run --project src/Server`

---

## 🌐 Truy cập ứng dụng sau khi chạy

Khi server đã chạy thành công, mở trình duyệt và truy cập:

| Địa chỉ | Mô tả |
|--------|-------|
| **https://localhost:7000** | Giao diện web chính (HTTPS) |
| **http://localhost:5000** | Giao diện web chính (HTTP) |
| **https://localhost:7000/swagger** | API Documentation |
| **https://localhost:7000/api/health** | Health Check |

---

## ✅ Kiểm tra project đã chạy đúng

1. **Kiểm tra Console:**
   - Phải thấy: `Starting Secure LAN Chat System`
   - Phải thấy: `Now listening on: https://localhost:7000`

2. **Kiểm tra trình duyệt:**
   - Mở https://localhost:7000/swagger
   - Phải thấy Swagger UI

3. **Kiểm tra database:**
   - Database sẽ tự động được tạo khi chạy lần đầu
   - Tên database: `SecureLanChat`

---

## 🔍 Xử lý lỗi thường gặp

### ❌ Lỗi: "dotnet not found"
**Giải pháp:**
- Cài .NET 6.0 SDK
- Khởi động lại PowerShell
- Kiểm tra: `dotnet --version`

### ❌ Lỗi: "Database connection failed"
**Giải pháp 1:** Kiểm tra SQL Server LocalDB:
```powershell
sqllocaldb info
sqllocaldb start mssqllocaldb
```

**Giải pháp 2:** Kiểm tra connection string trong `src/Server/appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=SecureLanChat;Trusted_Connection=true;MultipleActiveResultSets=true"
  }
}
```

### ❌ Lỗi: "Port 5000/7000 already in use"
**Giải pháp 1:** Tìm và kill process:
```powershell
netstat -ano | findstr :5000
taskkill /PID <PID> /F
```

**Giải pháp 2:** Đổi port trong `src/Server/Properties/launchSettings.json`

### ❌ Lỗi: "dotnet-ef not found"
**Giải pháp:**
```powershell
dotnet tool install --global dotnet-ef
dotnet tool update --global dotnet-ef
```

### ❌ Lỗi: "Migration failed"
**Giải pháp:**
```powershell
cd src\Server
dotnet ef database drop
dotnet ef database update
```

---

## 📱 Sử dụng ứng dụng

1. **Mở trình duyệt:** https://localhost:7000
2. **Đăng ký tài khoản mới** hoặc **đăng nhập**
3. **Chờ kết nối** và trao đổi encryption keys tự động
4. **Gửi tin nhắn** với users khác
5. **Xem message history**

**Để test với nhiều users:**
- Mở 2-3 tab trình duyệt khác nhau
- Đăng ký các tài khoản khác nhau
- Gửi tin nhắn giữa các tài khoản

---

## 📊 Các tính năng chính

- ✅ **End-to-end Encryption** (RSA + AES)
- ✅ **Real-time Chat** (SignalR WebSocket)
- ✅ **Online Users Tracking**
- ✅ **Message History**
- ✅ **Secure Key Exchange**

---

## 🆘 Cần hỗ trợ?

1. Kiểm tra logs trong console
2. Kiểm tra file logs: `logs/log-YYYY-MM-DD.txt`
3. Kiểm tra Swagger: https://localhost:7000/swagger
4. Kiểm tra Health Check: https://localhost:7000/api/health

---

**Chúc bạn chạy project thành công! 🎉**

