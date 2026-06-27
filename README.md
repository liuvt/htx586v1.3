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

dotnet user-secrets set `
  "Seed:AdminPassword" `
  "YOUR_INITIAL_ADMIN_PASSWORD" `
  --project .\src\HTX586CONTRACT.Web\HTX586CONTRACT.Web.csproj

dotnet restore .\HTX586CONTRACT.slnx
dotnet build .\HTX586CONTRACT.slnx
dotnet run --project .\src\HTX586CONTRACT.Web\HTX586CONTRACT.Web.csproj
```

## Template runtime

```text
src/HTX586CONTRACT.Web/Templates/Contracts/
├── HopDongVanChuyenHanhKhach.template.pdf
└── HopDongVanChuyenHanhKhach.layout.json
```

File thiết kế gốc nằm trong thư mục `design` và không tham gia vào runtime.

## Tài liệu đầy đủ

Xem [HTX586CONTRACT_LATEST.md](HTX586CONTRACT_LATEST.md).
