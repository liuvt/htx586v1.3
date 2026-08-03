# BUILD FIX V2.2

Bản vá này xử lý log build HTX586CONTRACT.Web ngày 03/08/2026.

## 5 lỗi compile đã sửa

1. `Admin/DriverAccounts/Create.razor`: không để trực tiếp `Htx@586` trong attribute Razor; chuyển thành hằng `ResetPassword`.
2. `Admin/Account.razor`: giá trị role `Admin` dùng hằng `RoleName`, tránh Razor hiểu thành namespace.
3. `Driver/Accounts/Index.razor`: giá trị role `VehicleOwner` dùng hằng `RoleName`.
4. `Admin/DriverAccounts/Index.razor`: khai báo `MudTextField T="string"` và callback `ValueChanged` có kiểu rõ ràng.
5. Đồng bộ các vị trí liên quan để tránh lỗi phát sinh sau khi Razor sinh mã C#.

## 17 cảnh báo đã xử lý theo log

- Thay `MudForm.Validate()` bằng `ValidateAsync()`.
- Đổi `Autocomplete` thành `AutoComplete` trên `MudTextField`.
- Thay `Variant` không hợp lệ của `MudPaper` bằng `Outlined="true"`.
- Đổi attribute `Title` của `MudIconButton` thành native attribute `title`.
- Thay `Dense="true"` trên `MudNumericField` bằng `Margin="Margin.Dense"`.
- Xóa các field `_uploadingSignaturePng` và `_creatingNewCustomer` không được đọc.

## SDK

`global.json` dùng SDK `9.0.301` và `rollForward: latestPatch`, khớp SDK đã cài trên máy build.

## Lệnh kiểm tra

```bat
cd /d "D:\HTX 586\htx586v1.3"
dotnet clean
dotnet restore
dotnet build
```

Môi trường đóng gói không có .NET SDK, vì vậy cần chạy `dotnet build` trên máy Windows để xác nhận compile thực tế.
