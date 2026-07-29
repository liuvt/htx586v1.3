# HTX586CONTRACT v1.3.1 - Hotfix lưu hợp đồng

> **Cập nhật v1.3.4:** dữ liệu runtime nằm tại thư mục `htx586contract_data` cùng cấp với `htx586contract`. Xem `HOTFIX_SIBLING_DATA_V1.3.4.md`.


## Lỗi đã xử lý

Thông báo:

`The database operation was expected to affect 1 row(s), but actually affected 0 row(s)`

Đây là `DbUpdateConcurrencyException`, thường xuất hiện khi:

- hai thao tác lưu/ký chạy gần như đồng thời trên điện thoại;
- bảng SQL Server còn trigger từ phiên bản cũ;
- hợp đồng và danh sách hành khách bị cập nhật qua nhiều bảng có `RowVersion`.

## Thay đổi

1. Tắt SQL Server `OUTPUT` cho `Contracts`, `ContractPassengers`, `ContractSignatures` để tương thích trigger cũ.
2. Không dùng `RowVersion` làm concurrency token cho ba bảng thuộc luồng hợp đồng.
3. Danh sách hành khách của luồng mới chỉ đọc/ghi trong `Contracts.ContractDataJson`.
4. Không còn xóa rồi chèn lại toàn bộ `ContractPassengers` mỗi lần bấm lưu.
5. Chặn hai lần bấm lưu chạy đồng thời trên giao diện Driver.
6. Hợp đồng cũ vẫn đọc được từ bảng `ContractPassengers` khi chưa có snapshot JSON hợp lệ.

## Database

Hotfix không yêu cầu xóa database và không yêu cầu xóa cột `RowVersion` hiện có.

## Deploy

```bash
dotnet restore
dotnet publish src/HTX586CONTRACT.Web/HTX586CONTRACT.Web.csproj -c Release -o publish
sudo systemctl stop htx586contract
# sao lưu và thay nội dung publish
sudo systemctl start htx586contract
sudo journalctl -u htx586contract -n 100 --no-pager
```
