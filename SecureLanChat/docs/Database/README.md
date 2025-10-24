# Database Setup và Configuration

## Tổng quan

Hệ thống sử dụng Entity Framework Core với SQL Server để quản lý dữ liệu. Database được thiết kế để hỗ trợ chat mã hóa với các bảng chính: Users, Messages, và Sessions.

## Cấu trúc Database

### 1. Users Table
- **Id**: Primary key (GUID)
- **Username**: Tên người dùng (unique, max 50 ký tự)
- **PublicKey**: Khóa công khai RSA (max 2048 ký tự)
- **IsOnline**: Trạng thái online/offline
- **LastSeen**: Thời gian hoạt động cuối cùng
- **CreatedAt**: Thời gian tạo tài khoản

### 2. Messages Table
- **Id**: Primary key (GUID)
- **SenderId**: ID người gửi (Foreign Key)
- **ReceiverId**: ID người nhận (NULL = broadcast)
- **Content**: Nội dung tin nhắn (đã mã hóa)
- **IV**: Initialization Vector cho mã hóa
- **MessageType**: Loại tin nhắn (Private/Broadcast)
- **CreatedAt**: Thời gian gửi

### 3. Sessions Table
- **Id**: Primary key (GUID)
- **UserId**: ID người dùng (Foreign Key)
- **ConnectionId**: ID kết nối SignalR
- **AESKey**: Khóa AES (đã mã hóa)
- **CreatedAt**: Thời gian tạo session
- **ExpiresAt**: Thời gian hết hạn
- **IsActive**: Trạng thái hoạt động

## Migration Commands

### Tạo Migration
```bash
dotnet ef migrations add InitialCreate --project src/Server
```

### Cập nhật Database
```bash
dotnet ef database update --project src/Server
```

### Xóa Migration
```bash
dotnet ef migrations remove --project src/Server
```

## Connection String

Cấu hình trong `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=SecureLanChat;Trusted_Connection=true;MultipleActiveResultSets=true"
  }
}
```

## Seeding Data

Hệ thống tự động seed dữ liệu test khi khởi động:

- **4 test users**: admin, user1, user2, testuser
- **4 test messages**: Mix giữa private và broadcast
- **Test data** được tạo với dữ liệu hợp lệ

## Performance Optimization

### Indexes
- Username (unique index)
- SenderId (foreign key index)
- ReceiverId (foreign key index)
- UserId (foreign key index)

### Connection Pooling
- Sử dụng Entity Framework connection pooling
- Cấu hình MaxPoolSize trong connection string
- Automatic cleanup expired sessions

## Monitoring

### Health Checks
- Database connectivity check
- Query performance monitoring
- Connection count tracking

### Logging
- Structured logging với Serilog
- Database operation logs
- Performance metrics

## Backup và Recovery

### Backup Strategy
- Regular full backups
- Transaction log backups
- Point-in-time recovery

### Recovery Procedures
- Database restore từ backup
- Data migration scripts
- Rollback procedures

## Security

### Data Protection
- Tất cả tin nhắn được mã hóa
- Khóa không lưu dưới dạng plain text
- Session timeout tự động

### Access Control
- Database user permissions
- Connection string security
- Audit logging
