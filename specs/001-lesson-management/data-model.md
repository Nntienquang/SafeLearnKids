# Data Model: Quản lý bài học

Mọi bảng đã có trong `hoctapchotre` và đã được map trong `Data/SafeLearnDbContext.cs`. Tính năng
**không thay đổi schema**.

## Lesson (`dbo.Lessons`)

| Field | Type | Rules |
|---|---|---|
| Id | int, identity | |
| SubjectId, GradeLevelId | int | Cặp phải tồn tại trong `SubjectGrades` (FK tổ hợp) — FR-003 |
| Title | nvarchar(200) | Bắt buộc, trim, 1–200 ký tự |
| Summary | nvarchar(500) null | Tùy chọn, ≤ 500 ký tự |
| LessonType | nvarchar(30) | `Video` \| `Reading` \| `Mixed` (CK_Lessons_2); không nhập từ form, tự tính sau mỗi thay đổi nội dung (FR-019, research R10) |
| DurationMin | int | ≥ 0 (CK_Lessons_1); không nhập từ form, tự tính sau mỗi thay đổi nội dung (FR-020, research R12); bài mới = 0; chưa có giới hạn trên (để Sprint 5) |
| SortOrder | int | 0–999; mặc định max(cùng môn + lớp) + 1 |
| Status | nvarchar(30) | `Draft` \| `InReview` \| `Published` \| `Archived` (CK_Lessons_3) |
| CreatedById | nvarchar(450) | Admin tạo bài |
| ApprovedById | nvarchar(450) null | Admin xuất bản; xóa khi về Draft |
| CreatedAt, UpdatedAt | datetime2 | UTC; `UpdatedAt` cập nhật ở mọi thay đổi bài, nội dung, trạng thái |
| PublishedAt | datetime2 null | Lần xuất bản gần nhất |
| RowVersion | rowversion | Token chống ghi đè đồng thời (research R1) |

### State transitions (research R2)

```text
Draft ──publish──▶ Published ──archive──▶ Archived
  ▲                    │                      │
  └──── back to draft ─┴──────────────────────┘
InReview (dữ liệu cũ) ──back to draft──▶ Draft
```

- Chỉ `Draft` cho sửa thông tin, nội dung và cho xóa.
- `Draft → Published` yêu cầu ít nhất 1 phần nội dung hợp lệ (research R3). Giao diện không tạo
  trạng thái `InReview`.
- `→ Published`: đặt `ApprovedById`, `PublishedAt`. `→ Draft`: xóa `ApprovedById`.
- `Published → Draft` làm bài ngừng hiển thị với bé tới khi xuất bản lại; không giữ bản song song.
  Form chuyển trạng thái này có hộp xác nhận (FR-012).

## LessonContent (`dbo.LessonContents`)

| Field | Type | Rules |
|---|---|---|
| Id | int | |
| LessonId | int | FK → Lessons, ON DELETE CASCADE |
| ContentKind | nvarchar(30) | Tạo mới: `Video` \| `Reading`. `Document`/`Image` có sẵn chỉ hiển thị/xóa/đổi thứ tự |
| Title | nvarchar(150) null | Tùy chọn khi tạo; mặc định tiêu đề video với Video |
| SourceVideoId | int null | Bắt buộc với Video; video phải `Approved` tại thời điểm thêm (CK_LessonContents_1) |
| Body | nvarchar(max) null | Bắt buộc, không rỗng với Reading; giới hạn form 20.000 ký tự |
| FileUrl, FileName | nvarchar null | Không tạo mới trong đợt này |
| SortOrder | int | 1..n liên tục trong bài sau mỗi lần thêm/đổi thứ tự/xóa (research R4) |

Hợp lệ để xuất bản: Video có SourceVideo `Approved`; Reading có Body không rỗng; Document/Image có FileUrl.

## SourceVideo (`dbo.SourceVideos`) — chỉ đọc

Dùng `Id`, `Title`, `DurationSec`, `ThumbnailUrl`, `Status` (`Pending` \| `Approved` \| `Blocked`).
Danh sách chọn chỉ gồm `Approved`. Việc duyệt video thuộc tính năng "Kênh gợi ý".

## LessonTag (`dbo.LessonTags`) — ngoài phạm vi

Gắn thẻ thuộc card Trello US-14. Tính năng này không đọc/ghi thẻ; khi xóa bài, các dòng
`LessonTags` cũ tự xóa theo (FK ON DELETE CASCADE).

## Subject / GradeLevel / SubjectGrade — chỉ đọc

Lấy tên môn, cờ `IsActive` (cảnh báo môn đang ẩn) và danh sách lớp hợp lệ của môn.

## AuditLog (`dbo.AuditLogs`) — ghi

`EntityType = "Lesson"`, `EntityId` = Id bài, `Action` theo research R6, `Detail` mô tả ngắn
(ví dụ "Draft→Published", tiêu đề phần nội dung).

## Liên kết chặn xóa bài (research R5)

`Assignments`, `ContentReports`, `Favorites`, `LearningPathItems`, `LessonActivities`, `Questions`,
`Quizzes` có `LessonId` = bài → từ chối xóa.
