# Implementation Plan: Quản lý bài học

**Branch**: `001-lesson-management` | **Date**: 2026-11-03 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/001-lesson-management/spec.md`

## Summary

Thay trang "Soạn bài học" tĩnh (`/admin-lesson`) bằng chức năng thật trên SQL: Admin tạo bài nháp
từ môn đang chọn ở `/admin-catalog`, sửa thông tin bài (có chống ghi đè đồng thời bằng
`Lessons.RowVersion`), thêm/sửa/đổi thứ tự/xóa nội dung Video (chỉ video đã duyệt) và Bài đọc, gắn
chuyển trạng thái Bản nháp → Đã xuất bản → Lưu trữ (về Bản nháp từ mọi trạng
thái khác), và xóa bài nháp chưa có dữ liệu liên quan. Cách làm theo đúng pattern của tính năng danh
mục môn học: service lấy `SafeLearnDbContext` qua DI, controller mỏng trong Area Admin, Razor
server-rendered, `TempData` cho thông báo, `AccountActivityLog` cho nhật ký, smoke test Python.

## Technical Context

**Language/Version**: C# trên .NET 10 (`net10.0`); Python 3 cho smoke test

**Primary Dependencies**: ASP.NET Core MVC + Razor, EF Core (SQL Server provider), ASP.NET Core
Identity (đã có); Bootstrap 5 + CSS/JS sẵn có của dự án (không thêm thư viện)

**Storage**: SQL Server, database `hoctapchotre`; các bảng đã có: `Lessons`, `LessonContents`,
`SourceVideos`, `SubjectGrades`, `AuditLogs` — không cần thay đổi schema

**Testing**: smoke test Python `scripts/smoke-lessons.py` dựa trên harness `scripts/smoke-sql-auth.py`;
chạy lại `smoke-catalog.py`, `smoke-admin-users.py`, `check-auth-regressions.py`

**Target Platform**: Web server Windows (Kestrel/IIS Express khi dev), trình duyệt desktop và mobile

**Project Type**: Web application (ASP.NET Core MVC, một project)

**Performance Goals**: Trang soạn bài và danh sách bài hiển thị < 2 giây với môn có 200 bài (SC-006)

**Constraints**: Không truy vấn trong vòng lặp; truy vấn đọc dùng `AsNoTracking` + projection; mọi
POST có antiforgery (filter toàn cục đã có); chế độ InMemory phải hiện thông báo thay vì lỗi

**Scale/Scope**: Vài Admin; hàng trăm bài; 1 controller mới, 2 service mới, 2 view mới

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Nguyên tắc | Đánh giá | Ghi chú |
|---|---|---|
| I. KISS · DRY · YAGNI | PASS | Không thêm thư viện, không interface thừa, không làm tải tệp/quiz (ngoài phạm vi). Gom phần ghi nhật ký trùng lặp trong `AccountActivityLog` (xem research R6). |
| II. SOLID | PASS | `LessonService` (thông tin bài, trạng thái, xóa) và `LessonContentService` (nội dung) tách trách nhiệm; quy tắc chuyển trạng thái là class tĩnh thuần `LessonWorkflow`. Controller chỉ điều phối. Không tạo interface vì chỉ có một implementation. |
| III. Mã dễ đọc | PASS | Trạng thái, loại nội dung, giới hạn độ dài là hằng số có tên; code/comment tiếng Anh, UI tiếng Việt. |
| IV. Lỗi & log | PASS | Bắt `DbUpdateConcurrencyException`, lỗi FK/unique (547/2601/2627) → thông báo tiếng Việt; lỗi khác đi exception handler toàn cục; log qua `ILogger`. |
| V. Bí mật | PASS | Không thêm cấu hình bí mật; smoke test dùng tài khoản tạm sinh ngẫu nhiên. |
| VI. Kiểm thử | PASS | `scripts/smoke-lessons.py` phủ mọi FR; chạy lại các bộ test cũ. Chưa có unit test project (TODO trong constitution). |
| Quy trình: không tự tạo file | CHỜ DUYỆT | Danh sách file mới ở mục Project Structure; chỉ tạo khi bạn duyệt plan này. |
| Quy trình: không xóa code tính năng khác | PASS | Chỉ thay trang mẫu `/admin-lesson` và đoạn `admin-lesson` trong `admin.js` — đó là placeholder của chính tính năng này. |

**Re-check sau Phase 1**: PASS — thiết kế dữ liệu và route không phát sinh vi phạm mới.

## Project Structure

### Documentation (this feature)

```text
specs/001-lesson-management/
├── plan.md              # This file
├── research.md          # Phase 0
├── data-model.md        # Phase 1
├── quickstart.md        # Phase 1
├── contracts/
│   └── admin-lesson-routes.md
└── tasks.md             # Phase 2 (/speckit-tasks, chưa tạo)
```

### Source Code (repository root)

```text
Areas/Admin/
├── Controllers/
│   ├── LessonsController.cs        # MỚI: routes admin-lessons/*
│   └── CatalogController.cs        # SỬA: admin-lesson chuyển hướng về admin-catalog
└── Views/
    ├── Lessons/
    │   ├── Create.cshtml           # MỚI: form tạo bài nháp
    │   └── Edit.cshtml             # MỚI: trang soạn bài (thông tin, nội dung, trạng thái)
    └── Catalog/
        ├── Index.cshtml            # SỬA: link "Bài học mới" và "Sửa" tới trang thật
        └── Lesson.cshtml           # XÓA: trang mẫu tĩnh được thay thế

Models/
└── LessonViewModels.cs             # MỚI: view model + hằng số trạng thái/loại

Services/
├── LessonWorkflow.cs               # MỚI: quy tắc chuyển trạng thái (static, thuần)
├── LessonService.cs                # MỚI: tạo/sửa/xóa bài, chuyển trạng thái
├── LessonContentService.cs         # MỚI: thêm/sửa/đổi thứ tự/xóa nội dung
└── AccountActivityLog.cs           # SỬA: gom ghi nhật ký theo loại đối tượng

Program.cs                          # SỬA: đăng ký 2 service mới
wwwroot/js/admin.js                 # SỬA: bỏ khối admin-lesson dùng localStorage
scripts/smoke-lessons.py            # MỚI: smoke test cho tính năng
```

**Structure Decision**: Giữ một project ASP.NET Core MVC như hiện tại; thêm controller riêng
`LessonsController` thay vì nhồi vào `CatalogController` (SRP), đặt nghiệp vụ trong `Services/`
theo cùng pattern `CatalogService`/`ChildProfileService`.

## Complexity Tracking

Không có vi phạm cần biện minh.
