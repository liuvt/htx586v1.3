# Triển khai HTX586CONTRACT V2 lên VPS Ubuntu

## 1. Sao lưu trước khi nâng cấp

```bash
# Sao lưu thư mục ứng dụng và data
sudo cp -a /www/wwwroot/htx586contract /www/wwwroot/htx586contract.backup
sudo cp -a /www/wwwroot/HTX586CONTRACT_Data /www/wwwroot/HTX586CONTRACT_Data.backup
```

Sao lưu SQL Server bằng công cụ quản trị hoặc lệnh `BACKUP DATABASE` trước khi chạy V2 lần đầu.

## 2. Chuẩn bị cấu hình Production

Tại thư mục publish, tạo `appsettings.Production.json` từ `appsettings.Production.example.json` và thay connection string.

Khuyến nghị không ghi mật khẩu SQL vào file. Dùng systemd environment:

```ini
[Service]
WorkingDirectory=/www/wwwroot/htx586contract
ExecStart=/usr/bin/dotnet /www/wwwroot/htx586contract/HTX586CONTRACT.Web.dll
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://127.0.0.1:4122
Environment="ConnectionStrings__Default=Server=127.0.0.1,1433;Database=HTX586CONTRACT;User Id=DB_USER;Password=DB_PASSWORD;TrustServerCertificate=True;"
User=www-data
Restart=always
RestartSec=5
```

## 3. Tạo thư mục dữ liệu dùng chung

Giả sử ứng dụng đặt tại `/www/wwwroot/htx586contract`:

```bash
sudo mkdir -p /www/wwwroot/HTX586CONTRACT_Data/uploads
sudo mkdir -p /www/wwwroot/HTX586CONTRACT_Data/dataprotection-keys
sudo chown -R www-data:www-data /www/wwwroot/HTX586CONTRACT_Data
sudo chmod -R 750 /www/wwwroot/HTX586CONTRACT_Data
```

Không xóa `dataprotection-keys` khi publish bản mới.

## 4. Nginx reverse proxy

```nginx
location / {
    proxy_pass http://127.0.0.1:4122;
    proxy_http_version 1.1;
    proxy_set_header Host $host;
    proxy_set_header X-Real-IP $remote_addr;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;

    proxy_set_header Upgrade $http_upgrade;
    proxy_set_header Connection "upgrade";
    proxy_read_timeout 3600s;
    proxy_send_timeout 3600s;
}
```

Trong Production phải bật `ForwardedHeaders:Enabled=true`.

## 5. Khởi động

```bash
sudo systemctl daemon-reload
sudo systemctl restart htx586contract
sudo systemctl status htx586contract --no-pager -l
sudo journalctl -u htx586contract -n 200 --no-pager
```

Lần khởi động đầu tiên sẽ:

- Tạo/nâng cấp các cột và index cần thiết.
- Chuyển role Driver cũ sang VehicleOwner.
- Seed Owner mặc định nếu chưa tồn tại.
- Seed hai loại hợp đồng.

## 6. Đăng nhập kiểm tra

```text
ID: owner
Password: Htx@586
```

Sau khi đăng nhập, kiểm tra lần lượt:

1. Dashboard Owner.
2. Tạo Công ty/Văn phòng.
3. Tạo Admin và gán Công ty/Văn phòng.
4. Tạo/duyệt VehicleOwner.
5. Tạo xe, chữ ký chủ xe và cấp xe cho VehicleOwner.
6. VehicleOwner đăng nhập và ký một lần theo xe.
7. Phát hợp đồng, nhận hợp đồng, ký khách hàng, hoàn thành và mở PDF.

## 7. Xử lý lỗi cookie/antiforgery cũ

Khi chuyển từ bản cũ sang bản mới, trình duyệt có thể còn cookie được mã hóa bằng key cũ. Đăng xuất, xóa cookie của domain và đăng nhập lại. Sau đó, việc giữ nguyên thư mục `dataprotection-keys` sẽ tránh lỗi lặp lại sau mỗi lần deploy.
