# Bổ sung Import Công ty/Văn phòng V2.4

## Chức năng

- Nút **Import Excel** nằm cạnh **Thêm mới** tại `/owner/companies`.
- Nút **Tải mẫu** tải đúng file `Template_Import_Company_HTX586.xlsx`.
- Chỉ Owner được sử dụng vì trang đã giới hạn role Owner.
- Đọc sheet `IMPORT_COMPANY`, kiểm tra đúng 15 tiêu đề cột.
- Giới hạn file 5 MB và tối đa 1.000 dòng dữ liệu.
- Xem trước dữ liệu trước khi ghi database.
- Kiểm tra trường bắt buộc, độ dài, email, ngày cấp CCCD và `IsActive`.
- Chặn mã số thuế trùng trong file hoặc đã có trong database, kể cả bản ghi xóa mềm.
- Chỉ import các dòng hợp lệ.
- Ghi `CreatedByUserId` theo Owner đang đăng nhập và `CreatedAt` theo UTC.
- Không import chữ ký; chữ ký người đại diện tiếp tục được cập nhật tại trang chi tiết.

## Build lại

```bat
cd /d "D:\HTX 586\htx586v1.3"

dotnet clean
if exist "src\HTX586CONTRACT.Web\bin" rmdir /s /q "src\HTX586CONTRACT.Web\bin"
if exist "src\HTX586CONTRACT.Web\obj" rmdir /s /q "src\HTX586CONTRACT.Web\obj"

dotnet restore
dotnet build
```

Không cần tạo migration vì chức năng này không thay đổi cấu trúc database.
