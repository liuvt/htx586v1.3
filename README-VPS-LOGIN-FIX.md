# HTX586 VPS Login Fix

Sửa lỗi Production chạy sau Nginx/aaPanel nhưng ASP.NET Core không nhận biết request HTTPS.

## Thay đổi source
- Production luôn bật Forwarded Headers.
- Nhận X-Forwarded-For, X-Forwarded-Proto và X-Forwarded-Host.
- UseForwardedHeaders vẫn chạy trước HSTS / HTTPS redirection / Authentication.

## Nginx bắt buộc
Reverse proxy tới port 4122 cần có tối thiểu:

    proxy_set_header Host $host;
    proxy_set_header X-Real-IP $remote_addr;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
    proxy_set_header X-Forwarded-Host $host;
    proxy_http_version 1.1;
    proxy_set_header Upgrade $http_upgrade;
    proxy_set_header Connection "upgrade";

## Sau deploy
- Giữ nguyên appsettings.Production.json hiện có trên VPS nếu file đó chứa connection string.
- Giữ quyền DataProtection keys cho user chạy service.
- Restart service và kiểm tra journalctl.
