# Research: Quản lý bài học

Không còn mục NEEDS CLARIFICATION trong Technical Context. Các quyết định dưới đây giải quyết những
điểm thiết kế mà spec để mở.

## R1. Chống ghi đè khi hai Admin cùng sửa (FR-005)

- **Decision**: Dùng cột `Lessons.RowVersion` (đã map `IsRowVersion`). Form sửa gửi kèm RowVersion
  dạng base64 trong hidden field; service gán vào `OriginalValue` rồi `SaveChanges`. Bắt
  `DbUpdateConcurrencyException` → "Bài đã được người khác thay đổi. Hãy tải lại trang." và giữ dữ
  liệu đã nhập trên form. Mọi thay đổi nội dung, trạng thái cũng cập nhật `Lessons.UpdatedAt`
  để RowVersion của bài đổi theo và thao tác trạng thái cũng kiểm tra RowVersion.
- **Rationale**: Cột đã có sẵn trong schema; không cần khóa bản ghi.
- **Alternatives**: So sánh `UpdatedAt` (kém chính xác); khóa bi quan (phức tạp, không cần).

## R2. Quy tắc chuyển trạng thái (FR-009, FR-011, FR-012)

- **Decision**: Class tĩnh `LessonWorkflow` chứa bảng chuyển hợp lệ:
  Draft→Published, Published→Archived, và {Published, Archived}→Draft; bài `InReview` có sẵn trong
  dữ liệu cũ chỉ đi được về Draft (không còn bước Chờ duyệt, clarification 2026-11-03).
  Chỉ trạng thái Draft cho phép sửa thông tin, nội dung. Khi xuất bản: ghi `ApprovedById` =
  Admin thực hiện, `PublishedAt` = thời điểm hiện tại (UTC). Khi về Draft: xóa `ApprovedById`, giữ
  `PublishedAt` làm mốc lần xuất bản gần nhất.
- **Rationale**: Bảng chuyển nhỏ, thuần, dễ đọc và dễ kiểm thử; tách khỏi truy cập dữ liệu (SRP).
- **Alternatives**: Thư viện state machine (thừa với 4 trạng thái); cho phép sửa bài đã xuất bản
  (trái FR-012, rủi ro an toàn cho trẻ).

## R3. Điều kiện xuất bản (FR-010)

- **Decision**: Bài cần ít nhất 1 phần nội dung hợp lệ: Video tham chiếu `SourceVideos.Status =
  'Approved'`, hoặc Reading có `Body` không rỗng sau khi trim. Phần Document/Image có sẵn trong dữ
  liệu mẫu được tính là hợp lệ nếu có `FileUrl`. Kiểm tra ở bước Draft→Published. Nếu bài
  có video không còn được duyệt thì chặn xuất bản và nêu tên phần nội dung đó.
- **Rationale**: Đúng lựa chọn Q2 = A; video bị chặn sau khi thêm vào bài sẽ chặn lần xuất bản kế tiếp.
- **Alternatives**: Chỉ kiểm tra lúc thêm video (bỏ sót video bị chặn sau đó).

## R4. Đổi thứ tự nội dung (US2)

- **Decision**: Nút "Lên"/"Xuống" trên từng phần (POST, không cần JavaScript). Service đánh lại
  `SortOrder` 1..n cho toàn bộ phần của bài trong một lần `SaveChanges`.
- **Rationale**: Đơn giản, hoạt động không cần JS, chuẩn hóa thứ tự kể cả khi dữ liệu cũ có số trùng.
- **Alternatives**: Kéo-thả bằng JS (phức tạp hơn, cần API JSON; để sau nếu cần).

## R5. Xóa bài (FR-013)

- **Decision**: Chỉ xóa được bài ở trạng thái Draft và không có bản ghi trong `Assignments`,
  `ContentReports`, `Favorites`, `LearningPathItems`, `LessonActivities`, `Questions`, `Quizzes`.
  `LessonContents` và `LessonTags` tự xóa theo (FK ON DELETE CASCADE đã có). Bắt lỗi FK 547 nếu dữ
  liệu xuất hiện giữa lúc kiểm tra và lúc xóa.
- **Rationale**: Giống cách xóa môn học; không mất dữ liệu học tập của trẻ.
- **Alternatives**: Xóa mềm bằng cờ (schema không có cột; Lưu trữ đã đóng vai trò này).

## R6. Ghi nhật ký quản trị (FR-015)

- **Decision**: Refactor `AccountActivityLog.CatalogActionAsync` thành
  `EntityActionAsync(actorId, action, entityType, entityId, detail)` dùng chung, với hằng số
  `SubjectEntity = "Subject"`, `LessonEntity = "Lesson"`; cập nhật `CatalogController` gọi qua hàm
  mới. Action cho bài: `CreateLesson`, `UpdateLesson`, `DeleteLesson`, `AddLessonContent`,
  `UpdateLessonContent`, `MoveLessonContent`, `DeleteLessonContent`,
  `ChangeLessonStatus` (detail ghi "Draft→Published").
- **Rationale**: Tránh nhân bản một phương thức gần giống cho mỗi loại đối tượng (DRY).
- **Alternatives**: Thêm `LessonActionAsync` riêng (lặp code).

## R7. Hiển thị bài đọc an toàn (FR-017)

- **Decision**: Hiển thị `Body` bằng Razor encode mặc định, giữ xuống dòng bằng CSS
  `white-space: pre-wrap`; không hỗ trợ định dạng HTML/Markdown trong đợt này.
- **Rationale**: Không thể chèn script; đủ cho bài đọc văn bản.
- **Alternatives**: Trình soạn rich text (thêm thư viện và cần lọc HTML — để sau).

## R8. Điểm vào từ danh mục và route cũ (FR-016)

- **Decision**: Trang danh mục: "Bài học mới" → `/admin-lessons/new?subject={id}`; "Sửa" từng bài →
  `/admin-lessons/{id}`. Route cũ `/admin-lesson` (link "Soạn bài học" ở sidebar) chuyển hướng về
  `/admin-catalog`, nơi chọn bài để soạn. Xóa view mẫu `Catalog/Lesson.cshtml` và khối `admin-lesson`
  trong `admin.js`.
- **Rationale**: Giữ link sidebar hoạt động mà không duy trì hai trang soạn bài.
- **Alternatives**: Bỏ mục sidebar (đổi điều hướng, cần thống nhất với nhóm).

## R9. Thứ tự mặc định và lớp hợp lệ (FR-002, FR-003)

- **Decision**: Bài mới có `SortOrder` = lớn nhất trong cùng môn + lớp cộng 1 (Admin sửa được, 0–999).
  Danh sách lớp lấy từ `SubjectGrades` của môn; service kiểm tra lại phía server.
  `CreatedById` = Admin đang đăng nhập.
- `DurationMin` không nhập tay nữa (xem R12); giới hạn trên để Sprint 5 (clarification 2026-11-03).
- **Rationale**: Bài mới nằm cuối danh sách mà không phải nhập tay; FK tổ hợp (SubjectId,
  GradeLevelId) với `SubjectGrades` đã bảo đảm ở mức database.

## R10. Loại bài tự xác định theo nội dung (FR-019)

- **Decision**: `LessonContentService` tính lại `Lessons.LessonType` trong cùng lần `SaveChanges`
  sau mỗi lần thêm, sửa hoặc xóa nội dung: chỉ Video → `Video`; không có Video → `Reading`; có Video
  và loại khác → `Mixed`; không có nội dung → `Video` (mặc định của bảng). Hàm tính là hàm tĩnh thuần
  trong `LessonWorkflow` để dễ kiểm thử.
- **Rationale**: Loại bài luôn khớp nội dung; bỏ một trường khỏi form (clarification 2026-11-03).
- **Alternatives**: Admin tự chọn (dễ lệch nội dung); tính khi hiển thị (phải lặp ở mọi nơi đọc bài).

## R11. Sửa bài đã xuất bản

- **Decision**: Không lưu phiên bản song song. Muốn sửa phải đưa bài về Bản nháp; nút này trên bài
  Đã xuất bản có hộp xác nhận báo bé sẽ tạm không thấy bài (clarification 2026-11-03).
- **Rationale**: Schema không có chỗ lưu bản nháp song song; trường hợp sửa bài đang dùng ít gặp.
- **Alternatives**: Bảng phiên bản bài học (đổi schema, nhân đôi logic hiển thị).

## R12. Thời lượng bài tự tính theo nội dung (FR-020)

- **Decision**: `LessonWorkflow.DurationMinutesFor` cộng `SourceVideos.DurationSec` của các video và thời
  gian đọc ước tính của bài đọc (`ReadingWordsPerMinute = 100`), làm tròn lên theo phút;
  `LessonContentService` gọi hàm này cùng lúc tính loại bài. Bài mới có `DurationMin = 0`. Mapping EF
  khai báo `HasSentinel(-1)` cho `DurationMin` để giá trị 0 không bị thay bằng default 10 của cột.
- **Rationale**: Video đã có thời lượng thật; nhập tay dễ lệch nội dung (clarification 2026-11-03).
- **Alternatives**: Admin nhập tay (lệch nội dung); tự gợi ý nhưng cho sửa (thêm trường, thêm quy tắc).
