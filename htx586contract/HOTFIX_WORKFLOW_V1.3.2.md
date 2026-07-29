# HTX586CONTRACT v1.3.2 – Luồng lưu, ký và khóa hợp đồng

> **Cập nhật v1.3.4:** dữ liệu runtime nằm tại thư mục `htx586contract_data` cùng cấp với `htx586contract`. Xem `HOTFIX_SIBLING_DATA_V1.3.4.md`.


## Nghiệp vụ đã chỉnh

1. Nút **Lưu hợp đồng** cho phép để trống toàn bộ dữ liệu nhập tay.
2. Sau khi tạo hoặc cập nhật, trạng thái luôn là **Chờ xác nhận từ khách hàng**.
3. Hợp đồng vẫn được chỉnh sửa và hai chữ ký tay vẫn được ký lại khi chưa chốt.
4. Chỉ khóa hợp đồng khi đồng thời đáp ứng:
   - Có chữ ký cố định của công ty.
   - Có chữ ký cố định của tài xế.
   - Có chữ ký tay của chủ sở hữu xe.
   - Có chữ ký tay của khách hàng.
   - File PDF chính thức đã được tạo và lưu metadata thành công.
5. Quy trình nút **Hoàn thành hợp đồng** được đổi thành:
   - Lưu dữ liệu hiện tại.
   - Kiểm tra đủ 4 chữ ký.
   - Tạo PDF chính thức.
   - Chỉ sau khi PDF thành công mới đổi trạng thái sang `Completed` và khóa chỉnh sửa.
6. Nếu tạo PDF thất bại, hợp đồng vẫn ở trạng thái chờ xác nhận và vẫn chỉnh sửa/ký lại được.
7. Các hợp đồng cũ có trạng thái `Completed` nhưng chưa có PDF/hash/thời điểm tạo PDF sẽ tự hiển thị lại là **Chờ xác nhận từ khách hàng** và cho phép tiếp tục xử lý.

## Database

Không cần thêm migration và không cần xóa dữ liệu. Bản này chỉ thay đổi logic nghiệp vụ và giao diện.

## Build

```bash
dotnet restore
dotnet build HTX586CONTRACT.slnx -c Release
dotnet publish src/HTX586CONTRACT.Web/HTX586CONTRACT.Web.csproj -c Release -o publish
```

## Deploy VPS

```bash
sudo systemctl stop htx586contract
# Sao lưu source đang chạy và database trước khi thay file.
# Chép nội dung thư mục publish mới, không xóa HTX586CONTRACT_Data.
sudo systemctl start htx586contract
sudo systemctl status htx586contract --no-pager -l
sudo journalctl -u htx586contract -f
```

## Kiểm thử đề nghị

1. Tạo hợp đồng không nhập trường nào và bấm **Lưu hợp đồng**.
2. Xác nhận trạng thái là **Chờ xác nhận từ khách hàng**.
3. Nhập bổ sung dữ liệu rồi lưu nhiều lần.
4. Ký chủ xe, ký khách hàng và thử ký lại.
5. Khi thiếu chữ ký, nút hoàn thành không thể chốt.
6. Khi đủ 4 chữ ký, bấm hoàn thành; kiểm tra PDF được tạo rồi hợp đồng mới khóa.
7. Tạm làm hỏng đường dẫn/template PDF để kiểm tra: tạo PDF lỗi thì hợp đồng không bị khóa.
