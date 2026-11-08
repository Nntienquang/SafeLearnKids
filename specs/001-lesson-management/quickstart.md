# Quickstart: kiểm tra tính năng Quản lý bài học

## Điều kiện

- .NET SDK 10, Python 3, `sqlcmd`, SQL Server có database `hoctapchotre` (xem README).
- Dừng mọi phiên `dotnet run` đang khóa `bin/` trước khi build.
- Có tài khoản Admin (README → "Tài khoản Admin đầu tiên").

## Kiểm thử tự động

```powershell
dotnet build SafeLearnKids.Web.csproj
py scripts\smoke-lessons.py
py scripts\smoke-catalog.py
py scripts\smoke-admin-users.py
py scripts\check-auth-regressions.py
```

Kỳ vọng: mỗi script in `RESULT n/n passed`; `smoke-lessons.py` tự dọn bài, nội dung và tài
khoản tạm, không đổi 5 bài mẫu.

## Kiểm tra thủ công

Chạy app (`dotnet run --project SafeLearnKids.Web.csproj --launch-profile http`), đăng nhập Admin.

1. **Tạo bài (US1)**: `/admin-catalog` → chọn Toán → "Bài học mới". Chỉ thấy lớp đã gán cho Toán.
   Lưu với tên trống → báo lỗi tại trường tên. Lưu hợp lệ → mở trang soạn bài, trạng thái "Bản nháp";
   bài xuất hiện trong danh sách bài của Toán.
2. **Chống ghi đè (US1)**: mở cùng bài ở hai tab, lưu tab 1 rồi lưu tab 2 → tab 2 báo bài đã bị thay
   đổi, dữ liệu tab 1 giữ nguyên.
3. **Nội dung (US2)**: thêm Video → danh sách chọn không có video bị chặn. Thêm Bài đọc trống → bị từ
   chối; thêm Bài đọc có `<script>` → hiển thị nguyên văn. Đổi thứ tự bằng "Lên/Xuống", tải lại
   trang → đúng thứ tự. Xóa một phần → có hộp xác nhận. Loại bài tự đổi: chỉ video → "Video",
   thêm bài đọc → "Kết hợp", xóa hết video → "Bài đọc". Thời lượng trong khối Trạng thái tự đổi theo
   tổng thời lượng video và thời gian đọc ước tính (100 chữ/phút); bài mới là 0 phút.
4. **Vòng đời (US3)**: bài không có nội dung → "Xuất bản" bị từ chối kèm lý do. Có nội dung → Xuất
   bản (thấy người xuất bản và thời điểm) → Lưu trữ → Về bản nháp. Từ "Đã xuất bản" bấm
   "Về bản nháp" → có hộp xác nhận báo bé sẽ tạm không thấy bài. Khi không ở Bản nháp,
   form thông tin và nội dung bị khóa.
5. **Xóa bài (US4)**: Xóa bài nháp mới tạo → về danh mục, bài biến
   mất. Thử xóa bài mẫu đã có dữ liệu học tập → bị từ chối, gợi ý lưu trữ.
6. **Nhật ký**: `sqlcmd -S . -d hoctapchotre -E -C -Q "SELECT TOP 10 Action, EntityId, Detail FROM dbo.AuditLogs WHERE EntityType=N'Lesson' ORDER BY Id DESC"` thấy các thao tác vừa làm.
7. **Phân quyền**: đăng nhập Phụ huynh, mở `/admin-lessons/1` → bị chuyển tới trang 403.

Chi tiết route: [contracts/admin-lesson-routes.md](contracts/admin-lesson-routes.md). Quy tắc dữ
liệu và trạng thái: [data-model.md](data-model.md).
