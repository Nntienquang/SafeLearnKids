# Contract: Admin lesson routes

Tất cả route yêu cầu đăng nhập với vai trò `Admin` (không phải Admin → chuyển tới `/error/403`,
chưa đăng nhập → `/login`). Mọi POST là form submit có antiforgery token (filter toàn cục);
thiếu token → 400. Id không tồn tại → 404. Khi chạy InMemory, các trang hiển thị thông báo
"chỉ có khi chạy với SQL Server" và POST chuyển hướng về `/admin-catalog`.

Kết quả thành công của POST: 302 về trang soạn bài (hoặc danh mục sau khi xóa) với
`TempData["Info"]`. Lỗi nghiệp vụ ở các form có dữ liệu người dùng gõ vào (thông tin bài, thêm/sửa
nội dung) trả lại 200 với trang tương ứng, thông báo lỗi và đúng dữ liệu đã nhập (FR-018). Lỗi ở các
thao tác chỉ có nút bấm (đổi thứ tự, xóa, trạng thái) trả 302 kèm `TempData["Error"]`.

| Method | Route | Form fields | Kết quả |
|---|---|---|---|
| GET | `/admin-lessons/new?subject={id}` | — | Form tạo bài; chọn lớp trong các lớp của môn. Môn không tồn tại → 404 |
| POST | `/admin-lessons/new` | `SubjectId`, `GradeLevelId`, `Title`, `Summary`, `SortOrder` | Tạo bài `Draft` → 302 `/admin-lessons/{id}`; lỗi → 200 form |
| GET | `/admin-lessons/{id}` | — | Trang soạn bài: thông tin, nội dung, trạng thái, nút thao tác theo trạng thái |
| POST | `/admin-lessons/{id}` | `GradeLevelId`, `Title`, `Summary`, `SortOrder`, `RowVersion` | Lưu thông tin (chỉ Draft); xung đột RowVersion → 200 form + thông báo |
| POST | `/admin-lessons/{id}/contents` | `RowVersion`, `ContentKind` (`Video`\|`Reading`), `Title`, `SourceVideoId` hoặc `Body` | Thêm phần nội dung cuối danh sách |
| POST | `/admin-lessons/{id}/contents/{contentId}` | `RowVersion`, `Title`, `Body` (Reading) hoặc `SourceVideoId` (Video) | Sửa phần nội dung |
| POST | `/admin-lessons/{id}/contents/{contentId}/move` | `RowVersion`, `direction` (`up`\|`down`) | Đổi thứ tự |
| POST | `/admin-lessons/{id}/contents/{contentId}/delete` | `RowVersion` | Xóa phần nội dung (form có `data-confirm`) |
| POST | `/admin-lessons/{id}/status` | `RowVersion`, `target` (`Published`\|`Archived`\|`Draft`) | Chuyển trạng thái theo bảng ở data-model; không hợp lệ/thiếu điều kiện → Error |
| POST | `/admin-lessons/{id}/delete` | `RowVersion` | Xóa bài Draft không có dữ liệu liên quan → 302 `/admin-catalog?subject={subjectId}` |
| GET | `/admin-lesson` | — | 302 → `/admin-catalog` (giữ link sidebar cũ) |

`LessonType` và `DurationMin` không nhận từ form; mọi POST thay đổi nội dung tự tính lại loại bài
(research R10) và thời lượng (research R12).
Form `status` với `target=Draft` từ bài Đã xuất bản có `data-confirm` báo bé sẽ tạm không thấy bài.

## Thay đổi trên `/admin-catalog`

- Nút "Bài học mới" → `/admin-lessons/new?subject={môn đang chọn}`.
- Nút "Sửa" của từng bài → `/admin-lessons/{lessonId}`.

## Nhật ký

Mỗi POST thành công ghi một dòng `dbo.AuditLogs` với `EntityType = "Lesson"`, `EntityId` = Id bài
và `Action` như trong research R6.
