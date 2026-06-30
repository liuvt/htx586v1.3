# HTX586CONTRACT

Ứng dụng web quản lý và ký hợp đồng vận chuyển hành khách cho HTX 586, viết bằng ASP.NET Core Blazor Interactive Server trên .NET 9.

## Điểm chính

- Solution và namespace độc lập: `HTX586CONTRACT`.
- Quản lý hồ sơ HTX, tài xế, khách hàng, phương tiện và hợp đồng.
- Ký 4 bên: văn phòng HTX, chủ xe, khách hàng, tài xế.
- Khóa cập nhật/hủy hợp đồng theo trạng thái chữ ký.
- Lưu chữ ký nguyên tử, không phụ thuộc `RowVersion` cũ.
- Sinh PDF 2 trang từ **PDF nền + layout JSON**.
- Runtime không cần Microsoft Word hoặc LibreOffice.
- Hỗ trợ triển khai IIS hoặc Docker.

## Chạy nhanh

```powershell
dotnet user-secrets set `
  "ConnectionStrings:Default" `
  "Server=localhost;Database=HTX586CONTRACT;User Id=sa;Password=YOUR_PASSWORD;TrustServerCertificate=True" `
  --project .\src\HTX586CONTRACT.Web\HTX586CONTRACT.Web.csproj

# Development đã có tài khoản bootstrap mặc định: owner / Owner@123456.
# Khi deploy thật, hãy cấu hình Seed:OwnerPassword riêng bằng user-secrets hoặc biến môi trường.

dotnet restore .\HTX586CONTRACT.slnx
dotnet build .\HTX586CONTRACT.slnx
dotnet run --project .\src\HTX586CONTRACT.Web\HTX586CONTRACT.Web.csproj
```

## Phân quyền

- `Owner`: quản lý tổng, được tạo từ seeding ban đầu. Owner tạo tài khoản `Admin`.
- `Admin`: được tạo kèm `CompanyProfile` và chữ ký cố định người đại diện.
- `Driver`: tài xế được gán vào `CompanyProfile` và có chữ ký cố định tài xế.
- Database seeding không tạo `CompanyProfile` mặc định nữa; CompanyProfile phát sinh khi Owner tạo Admin mới.
- Khi nâng cấp database cũ chưa có `Owner`, seeding sẽ tự gán quyền `Owner` cho tài khoản đã cấu hình bằng `Seed:OwnerUserName`/`Seed:AdminUserName`; nếu không tìm thấy thì tự gán cho Admin hiện hữu đầu tiên.
- Database mới ở môi trường Development tự tạo tài khoản bootstrap `owner / Owner@123456` và bắt buộc đổi mật khẩu sau khi đăng nhập. Production/Staging vẫn nên cấu hình `Seed:OwnerPassword` riêng để bootstrap an toàn.

## Template runtime

```text
src/HTX586CONTRACT.Web/Templates/Contracts/
├── HopDongVanChuyenHanhKhach.template.pdf
└── HopDongVanChuyenHanhKhach.layout.json
```

File thiết kế gốc nằm trong thư mục `design` và không tham gia vào runtime.

## Tài liệu đầy đủ

Xem [HTX586CONTRACT_LATEST.md](HTX586CONTRACT_LATEST.md).
