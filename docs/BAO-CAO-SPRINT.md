# Học gì để báo cáo từng sprint

Mỗi sprint liệt kê: user story → file cần đọc (theo thứ tự **Controller → Service → View → Test**) → ý chính phải giải thích được → đường dẫn để demo.

Cách dùng hệ thống, bấm nút nào và kịch bản test từng sprint: xem `docs/HUONG-DAN-TEST-CHUC-NANG.md`.

Mẹo đọc code nhanh:
- Mở controller, tìm `[HttpGet("...")]` để biết trang nằm ở địa chỉ nào.
- Đi theo lời gọi sang Service để hiểu nghiệp vụ.
- Mở View cùng tên để xem giao diện.
- Trong code có chú thích `US-xx`: tìm nhanh bằng Ctrl+Shift+F.

**File chung, sprint nào cũng có thể bị hỏi:**
- `Program.cs`: đăng ký service, middleware bảo mật, giới hạn số lần thử, thứ tự xác thực.
- `Data/SafeLearnDbContext.cs`: ánh xạ 56 bảng SQL (database có trước, code sinh từ DB).
- `Models/Entities/*`: mỗi bảng một class.
- `database/01-schema.sql`: toàn bộ schema. Giải thích từng bảng và trường: `docs/HOC-DATABASE.md`.
- `Services/SchemaUpdater.cs`: tự thêm cột hoặc bảng mới khi app khởi động.
- Layout: `Views/Shared/_ParentLayout.cshtml`, `_AdminLayout.cshtml`, `_KidsLayout.cshtml`.

---

## Sprint 1: Tài khoản và phân quyền (US-01 → US-10)

**US-01→03: đăng ký, đăng nhập/đăng xuất, đổi và khôi phục mật khẩu**
- `Controllers/AccountController.cs`
- `Data/ApplicationUser.cs`, `AppSignInManager.cs`, `AppClaimsFactory.cs`, `EmailConfirmationTokenProvider.cs`
- `Services/AccountEmailService.cs`, `AccountEmailQueue.cs`, `AccountActivityLog.cs`
- `Views/Account/*`
- Ý chính:
  - Dùng ASP.NET Identity; mật khẩu được băm.
  - Phải xác minh email mới đăng nhập được.
  - Sai 5 lần thì khóa 5 phút.
  - Giới hạn số lần thử theo IP.
  - Đổi hoặc đặt lại mật khẩu thì đăng xuất mọi phiên cũ (security stamp).

**US-04: phân quyền**
- `[Authorize(Roles = "Admin" | "Parent")]` trên từng controller.
- `Services/ChildModeFilter.cs`: khi đang ở chế độ trẻ, server khóa toàn bộ trang phụ huynh.

**US-05: admin quản lý tài khoản phụ huynh**
- `Areas/Admin/Controllers/UsersController.cs`
- `Areas/Admin/Views/Users/*`

**US-06, 07: tạo, sửa, xóa hồ sơ bé**
- `Areas/Parent/Controllers/ChildrenController.cs`
- `Services/ChildProfileService.cs`
- `Views/Children/Onboarding.cshtml`, `Index.cshtml`
- `wwwroot/js/parent.js` (gọi `/children`)
- Ý chính: tối đa 4 bé mỗi tài khoản; tên bé không trùng; phải giữ ít nhất 1 hồ sơ.

**US-08: PIN riêng của bé**
- `Services/ChildAccess.cs` (`ChildPins`)
- `Areas/Parent/Controllers/PinController.cs`, `Views/Pin/Index.cshtml`
- `Areas/Kids/Views/Learn/Picker.cshtml`
- Ý chính: PIN được băm; sai 5 lần khóa riêng bé đó 5 phút.

**US-09: chuyển sang không gian học của bé**
- `Services/KidContext.cs`: cookie lưu bé đang chọn, kiểm tra lại mỗi request.
- `LearnController.Picker/Choose`
- Thoát chế độ trẻ cần PIN hoặc mật khẩu: `LearnController.Exit`, `Services/ParentSettings.cs`.

**US-10: đồng ý xử lý dữ liệu trẻ**
- `Services/ChildAccess.cs` (`ChildConsent`)
- `Areas/Parent/Controllers/PrivacyController.cs`, `Views/Privacy/Index.cshtml`
- Ý chính: phải tick đồng ý khi tạo hồ sơ; rút đồng ý được; chưa đồng ý thì bé không vào học.

**Test:** `scripts/smoke-sql-auth.py`, `check-auth-regressions.py`, `smoke-admin-users.py`, `smoke-child-access.py`, `smoke-pin.py`

**Nhắc khi chưa có PIN:** `Parent/HomeController` (mục "Cần ba mẹ xem") và `Kids/Views/Learn/Picker.cshtml` (khung "Dành cho ba mẹ", chỉ hiện khi ba mẹ vừa mở chế độ trẻ, chưa chọn bé).

**Demo:** `/register` → mail xác minh → `/login` → `/onboarding` (tick đồng ý) → `/pin` → `/kids-picker`

**Câu hỏi hay gặp:**
- *"Sao bé không có tài khoản riêng?"*: trẻ tiểu học không tự quản lý được mật khẩu, nên bé dùng hồ sơ có PIN, học trên phiên của ba mẹ, và bị khóa trong chế độ trẻ ở phía server.

---

## Sprint 2: Môn học và bài học (US-11 → US-16)

- **US-11, 12, 13, 14:**
  - `Areas/Admin/Controllers/CatalogController.cs`
  - `Services/SubjectCatalog.cs`, `LessonFileStorage.cs`
  - `Views/Catalog/Index.cshtml` (danh sách, lọc), `SubjectForm.cshtml`, `Lesson.cshtml` (soạn bài, gắn nội dung: đoạn đọc, video, ảnh, PDF)
- **US-15** (phụ huynh chọn môn cho bé): `ChildProfileService.ResolveSubjectsAsync`.
- **US-16** (thư viện cho phụ huynh): `Areas/Parent/Controllers/LibraryController.cs`, `Views/Library/Index.cshtml`, `Details.cshtml`.
- **Bài của gia đình và kho cộng đồng** (mở rộng):
  - `Services/LessonAccess.cs`: 3 lớp hiển thị là bài chuẩn, bài của gia đình, bài cộng đồng đã duyệt.
  - `FamilyLessonsController.cs`, `Admin/CommunityController.cs`
- **Xem PDF trong trang:** `Views/Shared/_PdfViewer.cshtml`, `wwwroot/js/pdf-viewer.js`.
- **Ý chính:**
  - Trạng thái bài: Nháp → Đã đăng → Lưu trữ.
  - File tải lên được kiểm tra loại file và giới hạn 5 MB.
  - File bài học chỉ tải được khi đã đăng nhập.
- **Test:** `smoke-catalog.py`, `smoke-family.py`
- **Demo:** `/admin-catalog` → tạo môn → `/admin-lesson/new` → thêm nội dung → đăng → phụ huynh mở `/library`

---

## Sprint 3: Nguồn nội dung an toàn (US-17 → US-22)

- **US-17→20:**
  - `Areas/Admin/Controllers/CurationController.cs`
  - `Services/SourceSyncService.cs` (đồng bộ YouTube), `SourceUrl.cs` (đọc link kênh, playlist, video)
  - `Views/Curation/Index.cshtml`, `Source.cshtml`, `Videos.cshtml`
- **US-21** (phụ huynh chặn hoặc cho phép nguồn): `Areas/Parent/Controllers/SourcesController.cs`.
- **US-22** (đánh giá nguồn tự động): `Services/SourceAssessor.cs`. Xem mục "Về AI" cuối file.
- **Đồng bộ chạy nền:** `Services/SourceSyncQueue.cs` (hàng đợi + tiến trình nền), cột `SyncRequestedAt`. **Duyệt/chặn hàng loạt:** `CurationController.BulkVideoStatus`, `Views/Curation/Videos.cshtml`, `wwwroot/js/admin.js`.
- **Ý chính:**
  - Video chỉ phát được khi đã duyệt.
  - Phát qua `youtube-nocookie` trong iframe có sandbox.
  - Khóa YouTube API nằm trong user-secrets, không nằm trong Git.
- **Test:** `smoke-sources.py`
- **Demo:** `/admin-curate` → thêm nguồn → Đồng bộ → xem điểm đánh giá → duyệt → `/admin-videos` duyệt từng video

---

## Sprint 4: Hoạt động học của trẻ (US-23 → US-28)

- **US-23, 25, 26, 27, 28:**
  - `Areas/Kids/Controllers/LearnController.cs`: Home, Lesson, Next, Complete, Search, Favorite.
  - `Services/KidContext.cs`: `Openable` = những bài bé được mở.
  - `Areas/Kids/Views/Learn/*`, `wwwroot/js/kids.js`
- **US-24** (giao bài): `LibraryController.Assign`.
- **Ý chính:**
  - Bé chỉ thấy bài đã đăng, đúng lớp và đúng môn, hoặc bài được giao riêng.
  - Học xong phần trước mới mở phần sau.
  - Tiến độ lưu trong `LessonActivities`.
- **Test:** `smoke-learning.py`
- **Demo:** phụ huynh giao bài → `/kids-picker` → `/kids-home` → học từng phần → hoàn thành → tìm kiếm → yêu thích

---

## Sprint 5: Thời gian học và giám sát (US-29 → US-34)

- **US-29** (lịch sử học): `HistoryController.cs`, `Views/History/Index.cshtml`.
- **US-30, 31, 32:**
  - `TimeLimitsController.cs`
  - `Services/StudyTimeService.cs`: server đếm giây qua nhịp heartbeat, theo khung giờ và giới hạn trong ngày.
  - `LearnController` (`TimeUp`, xin thêm giờ)
- **US-33, 34** (báo cáo nội dung): `LearnController.Report`, `Admin/ReportsController.cs`.
- **Ý chính:**
  - Thời gian được đếm **trên server**, không tin số giây do trình duyệt gửi.
  - Hết giờ thì mọi trang học chuyển sang `/kids-time-up`.
- **Test:** `smoke-time.py`
- **Demo:** `/time` đặt 1 phút → bé học → hết giờ → bé xin thêm → phụ huynh duyệt

---

## Sprint 6: Câu hỏi và bài tập (US-35 → US-40)

- **US-35, 36, 40:** `Admin/QuestionsController.cs`, `Views/Questions/*`.
- **US-38, 39:**
  - `Services/QuizService.cs`: chấm điểm tự động.
  - `LearnController` (Quiz, Submit, Result), `Parent/HistoryController` (`/quiz-result/{id}`).
- **US-37** (giao bài tập có hạn nộp): `LibraryController.Assign`.
- **US-40** (AI gợi ý câu hỏi): `Services/QuestionSuggester.cs`. Xem mục "Về AI".
- **Ý chính:**
  - Câu hỏi do AI gợi ý luôn ở trạng thái "Chờ duyệt"; admin duyệt rồi mới dùng được.
  - Chấm điểm trên server; nộp trễ có ghi nhận.
- **Không tự chuyển câu, chấm tròn, đọc to:** `Areas/Kids/Views/Learn/Quiz.cshtml`, `wwwroot/js/kids.js` (khối quiz và khối "read aloud").
- **Giải thích cách khác:** `Services/KidExplainer.cs`, `QuestionsController.DraftKidExplanation`, cột `Questions.KidExplanation`; bé thấy ở `Result.cshtml`, `Review.cshtml`.
- **Test:** `smoke-quiz.py`, `smoke-ai.py`

---

## Sprint 7: Tiến độ (US-41 → US-46)

- **US-41→46:** `Services/ProgressService.cs` gồm tiến độ theo môn, lộ trình, huy hiệu, chuỗi ngày học, mục tiêu tuần và ôn câu sai.
- Phía phụ huynh: `Parent/ProgressController.cs`, `Views/Progress/Index.cshtml`.
- Phía admin: `Admin/AchievementsController.cs`, `Views/Achievements/Paths.cshtml`, `Badges.cshtml`.
- Phía bé: `LearnController` (`/kids-badges`, `/kids-review`).
- **Ý chính:** huy hiệu được trao tự động theo 4 loại tiêu chí: số bài, chuỗi ngày, bài đạt 100%, tổng số phút.
- **Học thích ứng:** `ProgressService.ReviewLessonsAsync` gom câu sai theo bài → "Con nên học lại bài nào?" (`/kids-review`) và "Nên học lại" (`/kids-home`).
- **Test:** `smoke-progress.py`

---

## Sprint 8: Thống kê, thông báo, dữ liệu (US-47 → US-52)

- **US-47** (báo cáo tuần): `Services/WeeklyReportService.cs`, `DailyJobs.cs` (chạy nền mỗi giờ), `Parent/WeeklyController.cs`.
- **US-48, 49:** `Admin/DashboardController.cs` (`/admin-home`), `StatsController.cs` (`/admin-stats`, có xuất CSV).
- **US-50:** `NotificationsController.cs`, `Services/NotificationWriter.cs`, `SettingsController.cs`, `ParentSettings.cs`.
- **US-51** (nhật ký): `Admin/AuditController.cs` (bảng `AuditLogs`, `LoginHistories`).
- **US-52** (xuất và xóa dữ liệu trẻ): `PrivacyController.cs`, `Services/ChildDataExport.cs`, `Admin/DataRequestsController.cs`.
- **Trang tổng quan phụ huynh:** `Parent/HomeController.cs`, `Views/Home/Index.cshtml`.
- **Test:** `smoke-reports.py`, `smoke-home.py`

---

## Triển khai và kiểm thử (nếu bị hỏi)

- **Deploy:** `DEPLOY.md`, `Dockerfile`, `docker-compose.yml`, `deploy/Caddyfile`, `database/*`.
- **Quét toàn bộ giao diện:** `scripts/crawl-ui.py`.
- **Kiểm tra sức khỏe app:** `GET /healthz`.

---

## Về AI trong dự án (US-22 và US-40)

| | US-22: đánh giá nguồn | US-40: gợi ý câu hỏi |
|---|---|---|
| File | `Services/SourceAssessor.cs` | `Services/QuestionSuggester.cs` |
| Cách làm hiện tại | **Bộ quy tắc** (`rule-v1`) luôn chạy: bắt đầu 60 điểm, cộng/trừ theo YouTube hay website, từ khóa bị chặn (−40), từ ngữ không hợp với trẻ (−25), dấu hiệu nội dung học tập (+5 mỗi dấu hiệu). **Có `Ai:ApiKey`: thêm Claude** đọc tên, mô tả, 50 tiêu đề và kết quả quy tắc, trả JSON (điểm, kết luận, vấn đề, lý do). Từ khóa bị chặn giới hạn điểm AI. Từ 75 gợi ý duyệt; dưới 50 gợi ý từ chối. | **AI thật: Claude** của Anthropic, gọi qua thư viện NuGet `Anthropic`, model đặt ở `Ai:Model` (`claude-opus-5-5`). Không có `Ai:ApiKey` thì dùng bộ sinh câu hỏi offline (`offline-rule-v1`). |
| Ai quyết định | Admin. Gợi ý chỉ để tham khảo; mọi lý do cộng hoặc trừ điểm được hiển thị. | Admin. Câu hỏi AI tạo ra luôn chờ duyệt. |
| Lưu ở đâu | Bảng `CurationSuggestions` (`Score`, `SafetyFlags`, `Summary`, `ModelName` = `rule-v1` hoặc tên model Claude) | Bảng `Questions` (trạng thái `InReview`) |

Khi báo cáo US-22, nói đúng: "có key thì Claude chấm cùng bộ quy tắc minh bạch; không key thì chỉ bộ quy tắc; admin luôn quyết định". Thêm: "giải thích cách khác" (`Services/KidExplainer.cs`) cũng do Claude soạn **nháp**, admin lưu thì bé mới thấy, nên không có chữ AI nào tới thẳng tay trẻ. Kiểm thử phần AI không cần key thật: `scripts/smoke-ai.py` dựng server Claude giả.
