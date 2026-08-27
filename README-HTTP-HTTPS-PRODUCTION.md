# HTX586 - Production cho phép đồng thời HTTP và HTTPS

Thay đổi:

- Cookie đăng nhập dùng `CookieSecurePolicy.SameAsRequest`.
  - HTTP: cookie không bắt buộc Secure, nên đăng nhập bằng IP/HTTP vẫn hoạt động.
  - HTTPS: cookie tự có Secure khi request được nhận là HTTPS.
- Không ép HTTP chuyển sang HTTPS mặc định (`Https:RedirectToHttps = false`).
- Không bật HSTS mặc định (`Https:HstsEnabled = false`) để trình duyệt không bị ép dùng HTTPS.
- Forwarded Headers hỗ trợ X-Forwarded-For, X-Forwarded-Proto và X-Forwarded-Host khi sau này chạy sau Nginx/HTTPS.

Khi đã có domain + SSL và muốn chuyển sang HTTPS-only:

```json
"Authentication": {
  "CookieSecurePolicy": "Always"
},
"Https": {
  "RedirectToHttps": true,
  "HstsEnabled": true
}
```

Nếu đang dùng `appsettings.Production.json` riêng trên VPS, cần đảm bảo file đó không ghi đè lại `Authentication:CookieSecurePolicy` thành `Always` hoặc `Https:RedirectToHttps` thành `true`.
