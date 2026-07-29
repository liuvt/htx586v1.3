# HTX586CONTRACT v1.3 – Bản tinh gọn theo Admin/Driver

> **Cập nhật v1.3.4:** dữ liệu runtime nằm tại thư mục `htx586contract_data` cùng cấp với `htx586contract`. Xem `HOTFIX_SIBLING_DATA_V1.3.4.md`.


## 1. Mô hình mới

- Chỉ còn hai vai trò nghiệp vụ: `Admin` và `Driver`.
- Mỗi tài khoản `Admin` đại diện cho một công ty. Thông tin công ty và chữ ký đại diện được lưu trực tiếp trên `AspNetUsers` của Admin.
- Tài xế thuộc trực tiếp một Admin qua `AspNetUsers.AdminId`.
- Xe thuộc trực tiếp một Admin qua `Vehicles.AdminId`; Admin hiện tại có thể chuyển xe sang một Admin khác.
- Không còn màn hình/quy trình quản lý CompanyProfile, Owner hoặc Customer độc lập.
- Các bảng cũ vẫn được giữ để đọc/chuyển đổi dữ liệu lịch sử, nhưng luồng mới không tạo CompanyProfile/Customer cho mỗi hợp đồng.

## 2. Hợp đồng một dòng snapshot

`Contracts.ContractDataJson` là nguồn dữ liệu snapshot đầy đủ và có thẩm quyền của từng hợp đồng, gồm:

- Công ty/Admin và chữ ký công ty tại thời điểm lập.
- Tài xế và chữ ký cố định tại thời điểm lập.
- Xe và toàn bộ thông tin chủ sở hữu nhập tay.
- Khách hàng nhập tay.
- Danh sách hành khách.
- Chữ ký chủ sở hữu xe và chữ ký khách hàng.

Các cột snapshot cũ, `ContractPassengers` và `ContractSignatures` vẫn được đồng bộ/đọc fallback để tương thích PDF và dữ liệu phiên bản cũ. Việc đổi xe, đổi Admin hoặc sửa hồ sơ sau này không làm đổi nội dung hợp đồng đã lưu.

## 3. Luồng hợp đồng

1. Driver tạo hợp đồng.
2. Công ty và tài xế được lấy tự động từ Admin/Driver, không cho nhập lại.
3. Driver nhập tay xe, chủ xe, khách hàng, nội dung hợp đồng và hành khách.
4. Bấm **Lưu tạm** để tạo hợp đồng.
5. Số hợp đồng bằng tổng số hợp đồng lịch sử của Driver cộng 1; khóa duy nhất theo `(DriverId, ContractNumber)`.
6. Chủ xe và khách hàng ký tay trên điện thoại. Có thể ký lại nhiều lần; chữ ký mới ghi đè chữ ký cũ.
7. Ký không khóa hợp đồng. Driver vẫn có thể sửa dữ liệu và ký lại.
8. Chỉ nút **Hoàn thành hợp đồng** mới kiểm tra đủ bốn chữ ký: công ty, tài xế, chủ xe, khách hàng.
9. Khi hoàn thành, hợp đồng bị khóa và PDF chính thức được tạo từ snapshot.

## 4. Đăng ký tài xế

- Driver chọn công ty/Admin khi gửi yêu cầu đăng ký.
- Chữ ký tài xế bắt buộc được tạo ngay trong hồ sơ đăng ký và lưu cố định.
- Chỉ Admin được chọn mới thấy và duyệt yêu cầu.
- Admin chỉ xem, sửa, khóa/mở khóa và reset mật khẩu tài xế thuộc chính `AdminId` của mình.

## 5. Nâng cấp database cũ

Khi ứng dụng khởi động, `DatabaseSeeder` tự kiểm tra và bổ sung các cột/index/FK cần thiết, sau đó:

- Chuyển thông tin CompanyProfile hiện hữu sang tài khoản Admin.
- Gán `AdminId` cho Driver, Vehicle và Contract cũ.
- Cho phép `Contracts.CompanyProfileId` và `Contracts.CustomerId` nullable.
- Chuyển hợp đồng cũ sang `ContractDataJson` nếu chưa có snapshot.
- Gỡ ràng buộc một xe – một tài xế của phiên bản cũ.

Trước lần deploy đầu tiên, bắt buộc sao lưu:

- Database SQL Server.
- Thư mục `HTX586CONTRACT_Data`, đặc biệt là `uploads` và `dataprotection-keys`.

## 6. Build và deploy

Dự án dùng .NET 9:

```bash
dotnet restore
dotnet build HTX586CONTRACT.sln -c Release
dotnet publish src/HTX586CONTRACT.Web/HTX586CONTRACT.Web.csproj -c Release -o publish
```

Sau khi deploy, đăng nhập từng Admin và kiểm tra đầy đủ thông tin công ty/chữ ký công ty trước khi cho tài xế hoàn thành hợp đồng mới.

## 7. Lưu ý tương thích

- Không xóa vật lý các bảng CompanyProfiles/Customers cũ trong lần nâng cấp này để tránh mất dữ liệu lịch sử.
- Migration cũ được giữ nguyên như lịch sử dự án; nâng cấp tương thích được thực hiện trong `DatabaseSeeder`.
- Nên thử nghiệm trên bản sao database production trước khi thay thế bản đang chạy.
