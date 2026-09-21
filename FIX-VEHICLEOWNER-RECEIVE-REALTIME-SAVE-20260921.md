# FIX VehicleOwner: Nhận HĐ -> chỉnh sửa -> lưu cập nhật

Ngày: 2026-09-21

## Triệu chứng

Sau khi Owner/Admin vừa phát hoặc cập nhật HĐ, VehicleOwner bấm Nhận HĐ rồi chỉnh sửa và Lưu cập nhật có thể nhận thông báo xung đột phiên / nhiều phiên cập nhật đồng thời.

## Nguyên nhân

1. UI `Driver/Contracts/Create.razor` sau `ReceiveAsync` chỉ đổi trạng thái local sang `Received`, không tải lại toàn bộ ContractDetail mới nhất từ DB. Vì vậy Blazor circuit có thể tiếp tục chỉnh trên model cũ nếu Owner/Admin vừa cập nhật.
2. Luồng VehicleOwner trước đây load và update từng `ContractPassenger` / `ContractCargoHandlingEvent` cũ. Các bảng con có `RowVersion`, nên việc merge lại các row cũ có thể phát sinh optimistic concurrency hoặc lỗi unique SortOrder, sau đó bị hiển thị chung thành lỗi nhiều phiên.
3. Các request Update trên cùng HĐ chưa được tuần tự hóa trong cùng instance ứng dụng.

## Thay đổi

- Thêm per-contract write gate cho `UpdateAsync`, `ReceiveAsync`, `CompleteAsync` để các thao tác ghi cùng một HĐ xếp hàng ngắn trong cùng instance.
- VehicleOwner update mở transaction trong SQL execution strategy, lấy `UPDLOCK + HOLDLOCK + ROWLOCK` trên dòng Contract trước khi load entity, đảm bảo đọc bản mới nhất sau lần lưu của Owner/Admin.
- Không load `Passengers` / `CargoHandlingEvents` cũ vào tracker trong luồng VehicleOwner.
- Khi VehicleOwner lưu, các collection editable được soft-delete bằng `ExecuteUpdateAsync` và insert lại theo snapshot hiện tại trên UI. Không còn phụ thuộc RowVersion của child row cũ.
- Sau khi Nhận HĐ thành công, UI gọi `GetByIdAsync` và nạp lại toàn bộ model mới nhất rồi mới bật chế độ edit.
- Sau khi Lưu cập nhật thành công, UI cũng reload toàn bộ detail/model thay vì chỉ đổi Status/Signatures.
- Không còn trả thông báo sai rằng chắc chắn có nhiều phiên đồng thời sau khi retry thất bại; log `ContractService` sẽ giữ lỗi DB cụ thể.

## Rule nghiệp vụ giữ nguyên

- Owner/Admin/VehicleOwner đều được cập nhật HĐ trước khi Completed/Cancelled.
- VehicleOwner sau khi nhận được chỉnh sửa và lưu nhiều lần.
- Chữ ký không khóa edit; nếu nội dung thay đổi thì chữ ký cũ bị vô hiệu và phải ký lại.
- Chỉ khi VehicleOwner hoàn thành HĐ thì mới khóa edit.
- Không thêm migration database.
