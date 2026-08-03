# Báo cáo kiểm tra mã nguồn V2

Ngày kiểm tra: 01/08/2026

## Kiểm tra đã thực hiện

- Đọc được toàn bộ JSON và XML project.
- Kiểm tra cân bằng dấu ngoặc cho toàn bộ file C#.
- Kiểm tra khối `@code` của toàn bộ Razor component.
- Kiểm tra route Razor không trùng nhau.
- Kiểm tra ba role chuẩn: `Owner`, `Admin`, `VehicleOwner`.
- Kiểm tra toàn bộ project target `net9.0`.
- Kiểm tra `global.json` khóa SDK `9.0.301`.
- Kiểm tra không còn mật khẩu SQL/VPS thật trong source.
- Kiểm tra các marker nghiệp vụ chính: mật khẩu reset, quan hệ nhiều xe, chữ ký theo xe, trạng thái hợp đồng, Data Protection và reconnect UI.
- Kiểm tra không đóng gói `bin`, `obj`, `.vs` hoặc file `.csproj.user`.

Kết quả kiểm tra tĩnh cuối:

```text
C# files: 87
Razor files: 62
Routes: 55
JSON files: 6
Project files: 4
ERRORS: 0
WARNINGS: 0
```

## Giới hạn môi trường kiểm tra

Môi trường xử lý source hiện không cài .NET SDK/MSBuild và không thể tải SDK qua mạng, vì vậy chưa thể chạy `dotnet restore`, `dotnet build` hoặc migration thực tế tại đây. Trước khi publish VPS, bắt buộc chạy các lệnh trong `DEPLOYMENT_VPS.md` trên máy có .NET SDK 9.0.301 và sao lưu database trước lần khởi động đầu tiên.
