# Thay đổi chính — V2

## Phân quyền

- Chuẩn hóa ba role: Owner, Admin, VehicleOwner.
- Chuyển dữ liệu role Driver cũ sang VehicleOwner.
- VehicleOwner không còn gán trực tiếp Công ty/Văn phòng.
- Sửa guard đăng nhập để chỉ Admin bắt buộc có Công ty/Văn phòng.

## Tài khoản

- Seed Owner mặc định `owner / Htx@586`.
- Reset mật khẩu chung `Htx@586`.
- Trang hồ sơ riêng cho Owner, Admin và VehicleOwner.
- Đăng ký tài khoản rút gọn còn ID, mật khẩu, số điện thoại và họ tên.
- Soft-delete, khóa/mở khóa và ghi nhận người tạo/cập nhật.

## Xe

- Một VehicleOwner có nhiều xe; một xe chỉ thuộc một VehicleOwner.
- Xe xác định ngược Công ty/Văn phòng của hợp đồng.
- Chữ ký tài xế lưu một lần theo từng xe.
- Admin CRUD và xóa mềm xe trong phạm vi đơn vị được gán.
- Chặn chuyển xe trực tiếp nếu xe vẫn đang gán tài khoản khác.

## Khách hàng

- Owner xem toàn hệ thống; Admin xem theo phạm vi đơn vị.
- VehicleOwner chỉ xem khách do chính tài khoản tạo.
- Khách mới trong hợp đồng tự tạo chỉ được ghi chính thức khi hoàn thành.
- Ghi người tạo, thời gian tạo và soft-delete.

## Hợp đồng

- Kích hoạt hợp đồng hành khách và hàng hóa.
- Tách trạng thái Đã tạo, Đã phát, Đã nhận, Đã hoàn thành và Đã hủy.
- Ghi người phát hợp đồng và thông báo cho VehicleOwner.
- Tách thao tác khách ký khỏi thao tác hoàn thành.
- Snapshot cố định toàn bộ dữ liệu nguồn khi hoàn thành.
- Khóa vĩnh viễn hợp đồng hoàn thành/hủy và tự sinh PDF khi hoàn thành.
- PDF dùng đúng chữ ký VehicleOwner theo xe, đếm hành khách thực tế và hỗ trợ các key dữ liệu hàng hóa.

## Dashboard và báo cáo

- Owner thống kê toàn hệ thống và theo Công ty/Văn phòng.
- Admin thống kê theo đơn vị được gán.
- VehicleOwner thống kê xe, biển số, hợp đồng và doanh thu cá nhân.
- Báo cáo Excel hỗ trợ một VehicleOwner có nhiều xe và nhiều đơn vị.

## Hạ tầng

- Nâng toàn bộ project lên .NET 9.
- Cố định SDK bằng `global.json`.
- Sửa đường dẫn Data Protection dùng chung giữa các lần publish.
- Thay overlay “Rejoining the server…” bằng giao diện kết nối lại tiếng Việt, tự reload khi circuit bị từ chối.
- Bổ sung badge thông báo ở thanh navigation mobile.
- Loại bỏ connection string VPS thật khỏi source.
