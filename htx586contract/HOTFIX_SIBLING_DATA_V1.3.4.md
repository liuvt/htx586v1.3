# HTX586CONTRACT v1.3.4 – dữ liệu nằm cùng cấp với source

## Cấu trúc bắt buộc

```text
/var/www/
├── htx586contract/
│   ├── src/
│   ├── deploy/
│   └── ...
└── htx586contract_data/
    ├── upload/
    └── dataprotection-keys/
```

- `htx586contract`: chỉ chứa source hoặc file publish của ứng dụng.
- `htx586contract_data/upload`: lưu chữ ký, PDF và file runtime.
- `htx586contract_data/dataprotection-keys`: lưu khóa cookie/antiforgery qua các lần deploy.
- URL public vẫn dùng `/uploads/...`; tên thư mục vật lý là `upload` để không phải cập nhật URL đã lưu trong database.

## Cấu hình

Khi chạy source từ `src/HTX586CONTRACT.Web`, `appsettings.json` và `appsettings.Development.json` dùng:

```json
"DataStorage": {
  "RootPath": "../../../htx586contract_data"
},
"FileStorage": {
  "UploadRootPath": "upload",
  "PublicRequestPath": "/uploads"
}
```

Khi deploy ứng dụng trực tiếp tại `/var/www/htx586contract`, `appsettings.Production.json` dùng:

```json
"DataStorage": {
  "RootPath": "../htx586contract_data"
}
```

Có thể khóa đường dẫn bằng systemd override:

```ini
[Service]
WorkingDirectory=/var/www/htx586contract
Environment="DataStorage__RootPath=../htx586contract_data"
Environment="FileStorage__UploadRootPath=upload"
Environment="FileStorage__PublicRequestPath=/uploads"
```

## Chuyển dữ liệu cũ

```bash
sudo systemctl stop htx586contract

sudo /var/www/htx586contract/deploy/move-data-outside-source.sh \
  /var/www/htx586contract/HTX586CONTRACT_Data \
  /var/www/htx586contract_data \
  www-data \
  www-data
```

Script tự chuyển thư mục cũ `uploads` sang thư mục mới `upload`, giữ nguyên `dataprotection-keys`, và không xóa dữ liệu cũ.

Sau đó:

```bash
sudo systemctl daemon-reload
sudo systemctl start htx586contract
sudo systemctl status htx586contract --no-pager -l
sudo journalctl -u htx586contract -n 100 --no-pager | grep DataRoot
```

Log đúng:

```text
HTX586CONTRACT DataRoot đang sử dụng: /var/www/htx586contract_data
```

## Cấp quyền thủ công

```bash
sudo mkdir -p /var/www/htx586contract_data/upload
sudo mkdir -p /var/www/htx586contract_data/dataprotection-keys
sudo chown -R www-data:www-data /var/www/htx586contract_data
sudo chmod -R u+rwX,go-rwx /var/www/htx586contract_data
```

Không đặt `htx586contract_data` bên trong `htx586contract` vì dữ liệu có thể bị ghi đè hoặc xóa khi cập nhật source/publish.
