# HTX586CONTRACT V2 — .NET 9

Bản viết lại từ mã nguồn HTX586CONTRACT, chuyển hệ thống sang ba vai trò cố định:

- **Owner**: điều hành toàn hệ thống.
- **Admin**: quản trị đúng Công ty/Văn phòng được gán.
- **VehicleOwner**: không gán trực tiếp Công ty/Văn phòng; phạm vi được suy ra theo từng xe được cấp.

> Các tên lớp/thuộc tính cũ như `DriverAccountService`, `DriverId`, `AssignedDriverId` được giữ để tương thích database và giảm rủi ro khi nâng cấp. Nghiệp vụ thực tế của các thành phần này đã chuyển thành **VehicleOwner**.

## Tài khoản seed

| Trường | Giá trị mặc định |
|---|---|
| ID đăng nhập | `owner` |
| Mật khẩu | `Htx@586` |
| Role | `Owner` |
| Số điện thoại | `0900000586` |

Mật khẩu reset dùng chung cho Owner, Admin và VehicleOwner là **`Htx@586`**. Sau khi reset, tài khoản được yêu cầu đổi mật khẩu.

Có thể thay thông tin seed bằng `Seed:*` trong `appsettings.Production.json` hoặc biến môi trường.

## Công nghệ

- Target framework: `net9.0`
- SDK khóa bằng `global.json`: `9.0.301`
- ASP.NET Core Identity / EF Core SQL Server: `9.0.18`
- Blazor Interactive Server
- MudBlazor, ClosedXML, PDFsharp, SkiaSharp

## Luồng dữ liệu chính

```text
Owner
  └─ Công ty/Văn phòng
       ├─ Admin
       └─ Xe và chủ sở hữu
            └─ VehicleOwner được cấp xe

VehicleOwner
  └─ Có thể được cấp nhiều xe
       └─ Mỗi xe thuộc một Công ty/Văn phòng
```

- Một VehicleOwner có thể có nhiều xe thuộc nhiều Công ty/Văn phòng khác nhau.
- Một xe chỉ được cấp cho tối đa một VehicleOwner tại một thời điểm.
- Muốn chuyển xe sang tài khoản khác phải bỏ gán và lưu trước, sau đó mới gán lại.
- Chữ ký tài xế được xác nhận một lần **theo từng xe**, không còn dùng chữ ký chung ở cấp tài khoản.

## Hợp đồng

Hai loại hợp đồng được seed và kích hoạt:

1. Hợp đồng vận chuyển hành khách.
2. Hợp đồng vận chuyển hàng hóa bằng xe ô tô.

### Hợp đồng Owner/Admin phát xuống

1. Owner/Admin chọn khách hàng đã có trong danh mục.
2. Chọn xe đã được cấp cho VehicleOwner.
3. Hệ thống phát thông báo cho VehicleOwner.
4. VehicleOwner phải có chữ ký theo xe và bấm **Nhận hợp đồng**.
5. VehicleOwner cập nhật người lái thực tế, chuyến đi và hành khách.
6. Khách hàng ký trực tiếp.
7. VehicleOwner bấm **Hoàn thành hợp đồng**.
8. Hệ thống chụp snapshot bất biến, khóa hợp đồng và sinh PDF.

### Hợp đồng VehicleOwner tự tạo

- Chỉ được tạo hợp đồng vận chuyển hành khách.
- Bắt buộc chọn một xe đang được cấp cho tài khoản.
- Công ty/Văn phòng và chủ xe được suy ra từ xe.
- Khách hàng được đối chiếu theo số điện thoại trong danh bạ riêng của VehicleOwner.
- Khách mới chỉ trở thành khách hàng chính thức khi hợp đồng hoàn thành.
- Hợp đồng hoàn thành hoặc đã hủy không thể sửa lại.

### Biến dữ liệu PDF

Renderer PDF hỗ trợ biến chung `CONTRACT_TITLE`, `CONTRACT_BUSINESS_TYPE` và các biến hàng hóa `CARGO_NAME`, `CARGO_WEIGHT`, `CARGO_UNIT`, `CARGO_WEIGHT_UNIT`. Metadata PDF tự đổi theo loại hợp đồng. Chữ ký VehicleOwner lấy từ chữ ký đã xác nhận của chính chiếc xe trong snapshot hợp đồng.

> PDF nền hiện có trong source là mẫu hành khách. Khi đưa mẫu hàng hóa riêng vào vận hành, bổ sung các key trên vào file layout tương ứng để hiển thị đúng vị trí.

## Phạm vi dữ liệu

- **Owner**: toàn hệ thống.
- **Admin**: xe, hợp đồng, doanh thu và khách hàng thuộc Công ty/Văn phòng được gán.
- **VehicleOwner**: xe được cấp, hợp đồng của tài khoản và khách hàng do chính tài khoản tạo.

## Database

Ứng dụng chạy bộ nâng cấp schema idempotent tại thời điểm khởi động để hỗ trợ database cũ:

- Chuyển role `Driver` sang `VehicleOwner`.
- Bỏ gán `CompanyProfileId` trực tiếp khỏi VehicleOwner.
- Bỏ unique index cũ giới hạn một tài khoản chỉ có một xe.
- Thêm chữ ký theo xe, người phát hợp đồng, thời điểm nhận/khóa và các trường snapshot mới.
- Giữ nguyên dữ liệu lịch sử và dùng soft-delete.

**Luôn sao lưu database trước lần chạy đầu tiên của bản V2.**

## Cấu hình bắt buộc

Không lưu mật khẩu SQL thật trong source. Trên VPS nên dùng biến môi trường:

```bash
export ConnectionStrings__Default='Server=SQL_HOST,1433;Database=HTX586CONTRACT;User Id=DB_USER;Password=DB_PASSWORD;TrustServerCertificate=True;'
export ASPNETCORE_ENVIRONMENT=Production
```

Khi chạy sau Nginx, tạo `appsettings.Production.json` từ file mẫu và bật:

```json
{
  "ForwardedHeaders": { "Enabled": true },
  "Authentication": { "CookieSecurePolicy": "Always" }
}
```

Thư mục sau phải tồn tại và user chạy dịch vụ phải có quyền ghi:

```text
../HTX586CONTRACT_Data/uploads
../HTX586CONTRACT_Data/dataprotection-keys
```

Giữ nguyên thư mục `dataprotection-keys` qua các lần publish để cookie/antiforgery không mất khả năng giải mã sau khi restart.

## Build và publish

```bash
dotnet --version
dotnet restore HTX586CONTRACT.slnx
dotnet build HTX586CONTRACT.slnx -c Release
dotnet publish src/HTX586CONTRACT.Web/HTX586CONTRACT.Web.csproj \
  -c Release \
  -o ./publish
```

Chi tiết triển khai VPS xem [DEPLOYMENT_VPS.md](DEPLOYMENT_VPS.md).

## Sửa lỗi lưu chân ký tài xế với SQL Server retry strategy (26/08/2026)

- Sửa lỗi `SqlServerRetryingExecutionStrategy does not support user-initiated transactions` khi tài khoản **Chủ xe** bấm **Xác nhận chữ ký người lái**.
- `ContractDocumentService.SaveSignatureAsync` giờ mở transaction `Serializable` bên trong `Database.CreateExecutionStrategy().ExecuteAsync(...)`, đúng với cấu hình `EnableRetryOnFailure` của SQL Server.
- Giữ cơ chế khóa `UPDLOCK/HOLDLOCK` để không cho hai thao tác ký cùng hợp đồng chạy chồng nhau.
- Bổ sung xử lý retry idempotent theo URL file chữ ký duy nhất để tránh báo nhầm "đã ký trước đó" nếu SQL đã commit nhưng kết nối bị ngắt đúng lúc trả kết quả.
- File chữ ký được dọn tự động nếu giao dịch SQL thất bại.
