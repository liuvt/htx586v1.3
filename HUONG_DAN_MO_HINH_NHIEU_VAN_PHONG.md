# Mô hình tài khoản – xe – Công ty/Văn phòng

## Quan hệ dữ liệu

- `ApplicationUser` dùng chung cho `Owner`, `Admin`, `VehicleOwner`.
- `Admin` quản lý nhiều Công ty/Văn phòng qua bảng `AdminOffices`.
- `VehicleOwner` sở hữu nhiều xe qua `Vehicles.AssignedDriverId`.
- Một xe hoạt động tại nhiều Công ty/Văn phòng qua bảng `OfficeVehicles`.
- Hợp đồng lưu `CompanyProfileId` của Công ty/Văn phòng được chọn tại thời điểm lập, đồng thời lưu snapshot thông tin để dữ liệu cũ không đổi theo hồ sơ hiện tại.

## Quy tắc vai trò

- Mỗi tài khoản nghiệp vụ chỉ được chọn đúng một vai trò: `Admin` hoặc `VehicleOwner`.
- Admin phải được gán ít nhất một Công ty/Văn phòng đang hoạt động.
- VehicleOwner không được gán văn phòng trực tiếp; phạm vi văn phòng được suy ra từ các xe sở hữu.
- VehicleOwner đang sở hữu xe không được đổi thành Admin cho đến khi toàn bộ xe được chuyển sang VehicleOwner khác.

## Quyền trên hợp đồng

- Owner được sử dụng toàn bộ Công ty/Văn phòng và xe đang hoạt động.
- Admin chỉ thấy Công ty/Văn phòng thuộc `AdminOffices` và chỉ thấy xe có liên kết `OfficeVehicles` trong phạm vi đó.
- VehicleOwner chỉ thấy xe được gán cho chính tài khoản và các văn phòng đang hoạt động của xe.
- Service kiểm tra lại quyền ở backend; không chỉ dựa vào danh sách hiển thị trên giao diện.

## Database mới

Dự án chỉ giữ một migration khởi tạo mới: `20260803032950_Init`.

Vì hệ thống chưa có dữ liệu, hãy dùng database mới hoặc xóa database thử nghiệm cũ trước khi chạy. Khi ứng dụng khởi động, `DatabaseSeeder` chạy `Database.MigrateAsync()` và tạo:

- Role `Owner`, `Admin`, `VehicleOwner`.
- Tài khoản Owner theo cấu hình `Seed:*`.
- Hai loại hợp đồng mặc định.

Không còn script chuyển đổi schema cũ hoặc dữ liệu demo.
