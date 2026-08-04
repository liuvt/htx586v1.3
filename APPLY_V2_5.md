# HTX586CONTRACT V2.5 - MudBlazor Index Company

## Thay đổi

- Bỏ giao diện nút HTML/CSS tự viết ở trang Owner/Companies/Index.
- Dùng `MudFileUpload<IBrowserFile>` của MudBlazor 9.5.0.
- Dùng `MudGrid`, `MudStack`, `MudButton`, `MudPaper` và `Wrap="Wrap.Wrap"` để responsive.
- Cho phép chọn lại đúng cùng một file Excel nhờ `MudFileUpload.ClearAsync()`.
- File `Index.razor.css` được ghi đè thành file rỗng; không còn CSS riêng cho trang.

## Áp dụng

Copy thư mục `src` đè vào thư mục gốc project.

Sau đó chạy:

```bat
dotnet clean
if exist "src\HTX586CONTRACT.Web\bin" rmdir /s /q "src\HTX586CONTRACT.Web\bin"
if exist "src\HTX586CONTRACT.Web\obj" rmdir /s /q "src\HTX586CONTRACT.Web\obj"
dotnet restore
dotnet build
```
