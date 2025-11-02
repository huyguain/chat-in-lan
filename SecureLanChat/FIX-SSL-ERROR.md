# 🔒 Hướng dẫn khắc phục lỗi ERR_SSL_PROTOCOL_ERROR

## ❌ Lỗi bạn gặp:
```
ERR_SSL_PROTOCOL_ERROR
Trang web này không thể cung cấp kết nối an toàn
Localhost đã gửi ý kiến phản hồi không hợp lệ
```

## ✅ Nguyên nhân:
Lỗi này xảy ra khi:
- Certificate HTTPS bị hỏng hoặc không hợp lệ
- Server không load certificate đúng cách
- TLS/SSL handshake thất bại
- Certificate chưa được trust đúng cách

## 🔧 Giải pháp:

### Bước 1: Làm sạch và tạo lại certificate ⭐ QUAN TRỌNG

**Phải chạy PowerShell với quyền Administrator!**

1. **Mở PowerShell với quyền Administrator**:
   - Nhấn `Win + X`
   - Chọn **"Windows PowerShell (Admin)"** hoặc **"Terminal (Admin)"**

2. **Chạy các lệnh sau**:
   ```powershell
   cd E:\Documents\Frontend\chat-in-lan\SecureLanChat
   
   # Xóa certificate cũ
   dotnet dev-certs https --clean
   
   # Tạo lại certificate mới và trust nó
   dotnet dev-certs https --trust
   ```

3. **Xác nhận** khi Windows hỏi có trust certificate không → Chọn **"Yes"**

### Bước 2: Khởi động lại server (BẮT BUỘC!)

**Sau khi tạo lại certificate, bạn PHẢI khởi động lại server!**

1. **Dừng server hiện tại** (nếu đang chạy):
   - Tìm cửa sổ terminal đang chạy server
   - Nhấn `Ctrl + C` để dừng server
   - Hoặc đóng cửa sổ terminal

2. **Khởi động lại server**:
   ```powershell
   cd E:\Documents\Frontend\chat-in-lan\SecureLanChat
   .\run.ps1
   ```

   Hoặc chạy thủ công:
   ```powershell
   cd E:\Documents\Frontend\chat-in-lan\SecureLanChat\src\Server
   dotnet run --launch-profile https
   ```

3. **Chờ thấy thông báo**:
   ```
   Now listening on: https://localhost:3000
   Now listening on: http://localhost:3001
   Application started. Press Ctrl+C to shut down.
   ```

### Bước 3: Xóa cache trình duyệt

1. **Trong trình duyệt**, nhấn `Ctrl + Shift + Delete`
2. Chọn **"Cached images and files"** hoặc **"Ảnh và tệp đã lưu trong bộ nhớ cache"**
3. Nhấn **"Clear data"** hoặc **"Xóa dữ liệu"**
4. **Đóng hoàn toàn trình duyệt** (tất cả các cửa sổ)
5. **Mở lại trình duyệt**

### Bước 4: Thử truy cập lại

1. Truy cập: `https://localhost:3000/index.html`
2. Hoặc: `https://localhost:3000/`

**Lần đầu truy cập**, trình duyệt có thể hiển thị cảnh báo:
- Chọn **"Advanced"** hoặc **"Nâng cao"**
- Chọn **"Proceed to localhost (unsafe)"** hoặc **"Tiếp tục đến localhost (không an toàn)"**

### Bước 5: Nếu vẫn lỗi - Thử các cách sau

#### Cách 1: Thử trình duyệt khác
- Chrome
- Microsoft Edge
- Firefox

#### Cách 2: Dùng chế độ Incognito/Private
- **Chrome/Edge**: `Ctrl + Shift + N`
- **Firefox**: `Ctrl + Shift + P`

#### Cách 3: Kiểm tra certificate trong Windows

1. Nhấn `Win + R`
2. Gõ `certmgr.msc` và nhấn Enter
3. Vào **Personal** > **Certificates**
4. Tìm certificate có **CN=localhost**
5. Đảm bảo:
   - Certificate còn hiệu lực (chưa hết hạn)
   - Certificate có icon khóa vàng

#### Cách 4: Tạm thời dùng HTTP

Nếu HTTPS vẫn không hoạt động, tạm thời dùng HTTP để test:

```powershell
cd E:\Documents\Frontend\chat-in-lan\SecureLanChat\src\Server
dotnet run --launch-profile http
```

Truy cập: `http://localhost:5000/index.html`

**⚠️ Lưu ý**: HTTP không an toàn, chỉ dùng để test. Trong production phải dùng HTTPS!

## 📋 Checklist

Trước khi báo lỗi, đảm bảo bạn đã:

- [ ] ✅ Đã mở PowerShell với **quyền Administrator**
- [ ] ✅ Đã chạy `dotnet dev-certs https --clean`
- [ ] ✅ Đã chạy `dotnet dev-certs https --trust`
- [ ] ✅ Đã xác nhận trust certificate (chọn Yes khi Windows hỏi)
- [ ] ✅ Đã **KHỞI ĐỘNG LẠI server** (sau khi tạo lại certificate)
- [ ] ✅ Đã xóa cache trình duyệt
- [ ] ✅ Đã đóng và mở lại trình duyệt
- [ ] ✅ Đã thử trình duyệt khác hoặc chế độ Incognito
- [ ] ✅ Đã kiểm tra server đang chạy với profile `https`

## 🚨 Nếu vẫn không được:

1. **Khởi động lại máy tính** (đôi khi cần thiết)
2. **Kiểm tra Windows Firewall**:
   - Tạm thời tắt để test
   - Hoặc thêm exception cho .NET
3. **Kiểm tra Windows Defender** có chặn không
4. **Xem logs server** trong terminal để tìm lỗi chi tiết
5. **Kiểm tra file logs** trong `SecureLanChat/src/Server/logs/`

## 💡 Giải thích kỹ thuật:

- **Development certificate** được .NET tự động tạo để dùng HTTPS trong development
- Certificate này được lưu trong **Windows Certificate Store**
- Khi certificate bị hỏng, server không thể handshake SSL/TLS với browser
- Việc **trust certificate** giúp Windows và browser tin tưởng certificate này

---

**Chúc bạn khắc phục thành công! 🎉**

Nếu vẫn gặp vấn đề, hãy:
1. Kiểm tra logs server trong terminal
2. Kiểm tra file logs trong `logs/` folder
3. Thử các bước trên một lần nữa

