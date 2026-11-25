# Học database: các bảng và các trường

File này giải thích từng bảng trong database `hoctapchotre` (SQL Server): bảng dùng để làm gì và các trường quan trọng có ý nghĩa gì. Đọc kèm `BAO-CAO-SPRINT.md`: file đó chỉ ra code nằm ở đâu, file này chỉ ra dữ liệu được lưu thế nào.

- Schema đầy đủ: `database/01-schema.sql` (56 bảng)
- Dữ liệu gốc (lớp, môn, huy hiệu...): `database/02-reference-data.sql`
- Class C# tương ứng: `Models/Entities/*.cs`, ánh xạ trong `Data/SafeLearnDbContext.cs`
- Cột và bảng thêm sau này: `Services/SchemaUpdater.cs` (tự chạy khi app khởi động)

---

## 1. Khái niệm cần nắm trước

| Khái niệm | Nghĩa | Ví dụ trong project |
|---|---|---|
| **Khóa chính (PK)** | Cột định danh duy nhất cho từng dòng | Hầu hết các bảng có `Id INT IDENTITY`, tức số tự tăng 1, 2, 3... Bảng ghi nhiều dữ liệu (log, phiên học) dùng `BIGINT` cho đủ chỗ. |
| **Khóa ngoại (FK)** | Cột trỏ tới khóa chính của bảng khác, giữ cho dữ liệu liên kết đúng | `Lessons.SubjectId` trỏ tới `Subjects.Id`. Không thể tạo bài học thuộc một môn không tồn tại. |
| **UNIQUE** | Không cho hai dòng trùng giá trị | `UQ_Subjects_Code`: không có hai môn trùng mã. `UQ_ChildSubjects_ChildProfileId_SubjectId`: một bé không thể chọn một môn hai lần. |
| **CHECK** | Điều kiện dữ liệu bắt buộc phải đúng, database tự từ chối nếu sai | `Status` của video chỉ được là `Pending`, `Approved` hoặc `Blocked`. `ProgressPercent` phải nằm trong 0 đến 100. |
| **NULL / NOT NULL** | Cột được để trống hay bắt buộc có giá trị | `Lessons.Title` bắt buộc. `Lessons.PublishedAt` để trống cho tới khi bài được đăng. |
| **DEFAULT** | Giá trị tự điền nếu không truyền vào | `CreatedAt` mặc định là giờ hiện tại, `Status` của video mặc định là `Pending`. |
| **Bảng nối (n-n)** | Bảng chỉ gồm hai khóa ngoại, dùng cho quan hệ nhiều-nhiều | `ChildSubjects` (bé ↔ môn), `LessonTags` (bài ↔ thẻ), `SourceVideoSources` (nguồn ↔ video) |

**Quy ước chung, gặp ở rất nhiều bảng:**

- `...Id NVARCHAR(450)`: khóa ngoại trỏ tới `Users.Id`. Id người dùng của ASP.NET Identity là chuỗi GUID chứ không phải số, nên cột dài 450 ký tự. Ví dụ: `ParentId`, `CreatedById`, `ReviewedById`.
- `Status NVARCHAR(30)`: trạng thái lưu dạng chữ tiếng Anh (`Draft`, `Approved`...), luôn có CHECK giới hạn các giá trị hợp lệ. Nhãn tiếng Việt hiển thị trên giao diện nằm trong code (ví dụ `LessonStatus.Label`).
- `CreatedAt`, `UpdatedAt`, `...At DATETIME2`: thời điểm, lưu theo giờ **UTC**. Giao diện đổi sang giờ Việt Nam khi hiển thị (`ToLocalTime()`).
- `RowVersion ROWVERSION`: SQL Server tự đổi giá trị mỗi khi dòng bị sửa. Dùng để chống **hai người sửa cùng lúc** (optimistic concurrency): nếu admin A mở bài, admin B sửa và lưu trước, thì khi A lưu sẽ bị báo lỗi thay vì ghi đè mất thay đổi của B. Có ở `Lessons`, `ContentSources`, `SourceVideos`, `ContentReports`, `TimeExtensionRequests`.
- `IsActive BIT`: dùng để **ẩn** thay vì xóa (1 = đang dùng, 0 = ẩn). Dữ liệu cũ vẫn còn nên lịch sử học của bé không bị mất.
- `SortOrder INT`: thứ tự hiển thị.
- `IconKey`, `ColorKey`, `AvatarKey`: tên biểu tượng hoặc màu có sẵn trong giao diện (ví dụ `teal`). Không lưu file ảnh.
- `...Hash`: giá trị đã được **băm** (mật khẩu, PIN). Database không lưu PIN hay mật khẩu gốc.

---

## 2. Sơ đồ quan hệ chính

```
Users (phụ huynh / admin)
 ├── ChildProfiles (hồ sơ bé) ──┬── ChildSubjects ── Subjects ── SubjectGrades ── GradeLevels
 │                              ├── LessonActivities, Favorites, Assignments ── Lessons
 │                              ├── LearningSessions, DailyUsages, TimeLimitPolicies
 │                              └── QuizAttempts, ChildBadges, Streaks, WeeklyReports
 ├── ParentSources ── ContentSources ── SourceVideoSources ── SourceVideos
 └── UserConsents, UserSettings, Notifications, DataRequests

Lessons ──┬── LessonContents (đoạn đọc / video / ảnh / PDF) ── SourceVideos
          ├── Questions ── QuestionOptions
          └── Quizzes ── QuizQuestions ── Questions
```

---

## 3. Tài khoản và phân quyền (Sprint 1)

### Users: tài khoản đăng nhập (phụ huynh và admin)
Bảng của ASP.NET Identity (tên gốc `AspNetUsers`, được đổi thành `Users`), có thêm vài cột riêng của project.

| Trường | Ý nghĩa |
|---|---|
| `Id` | Chuỗi GUID, định danh người dùng |
| `UserName`, `Email` | Tên đăng nhập và email (project dùng email làm tên đăng nhập) |
| `NormalizedUserName`, `NormalizedEmail` | Bản viết HOA, dùng để tìm kiếm không phân biệt hoa thường và chặn đăng ký trùng |
| `EmailConfirmed` | Đã bấm link xác minh email chưa. Chưa xác minh thì không đăng nhập được. |
| `PasswordHash` | Mật khẩu đã băm |
| `SecurityStamp` | Đổi giá trị khi đổi mật khẩu, nhờ đó mọi phiên đăng nhập cũ bị đăng xuất |
| `ConcurrencyStamp` | Chống sửa trùng, cùng ý tưởng với RowVersion |
| `LockoutEnd`, `LockoutEnabled`, `AccessFailedCount` | Khóa tài khoản: đếm số lần nhập sai, sai 5 lần thì khóa tới thời điểm `LockoutEnd` |
| `PhoneNumber...`, `TwoFactorEnabled` | Cột có sẵn của Identity, project chưa dùng |
| `FullName`, `AvatarUrl` | Họ tên và ảnh đại diện (thêm riêng) |
| `IsActive` | Admin khóa hoặc mở tài khoản (US-05) |
| `LastLoginAt` | Lần đăng nhập gần nhất |

### Roles, UserRoles: vai trò
- `Roles`: danh sách vai trò, gồm `Admin` và `Parent`.
- `UserRoles`: bảng nối người dùng ↔ vai trò. Code dùng `[Authorize(Roles = "Admin")]` để kiểm tra vai trò.
- `UserClaims`, `RoleClaims`, `UserLogins`, `UserTokens`: các bảng chuẩn của Identity (thông tin bổ sung, đăng nhập bằng Google/Facebook, token xác thực hai bước). Project chưa dùng tới nên các bảng này gần như để trống. Link xác minh email và khôi phục mật khẩu được tạo dạng có chữ ký, không cần lưu vào bảng.

### LoginHistories: lịch sử đăng nhập
| Trường | Ý nghĩa |
|---|---|
| `UserId` | Ai đăng nhập. NULL nếu email nhập vào không tồn tại. |
| `UserNameTried` | Email đã nhập, giữ lại cả khi đăng nhập sai |
| `Succeeded`, `FailReason` | Thành công hay thất bại, kèm lý do (sai mật khẩu, bị khóa, chưa xác minh...) |
| `IpAddress`, `UserAgent` | Máy và trình duyệt, dùng để phát hiện đăng nhập lạ |

### ChildProfiles: hồ sơ của bé
Bé không có tài khoản riêng. Bé là một hồ sơ nằm dưới tài khoản phụ huynh và học qua **chế độ trẻ**.

| Trường | Ý nghĩa |
|---|---|
| `ParentId` | Phụ huynh sở hữu hồ sơ. Mọi truy vấn đều lọc theo cột này để gia đình này không xem được bé nhà khác. |
| `UserId` | Dự phòng nếu sau này bé có tài khoản riêng. Hiện để trống. |
| `DisplayName` | Tên hiển thị. UNIQUE theo từng phụ huynh: một nhà không có hai bé trùng tên. |
| `GradeLevelId` | Bé học lớp mấy. Quyết định bé thấy những bài nào. |
| `BirthYear` | Năm sinh, CHECK trong khoảng 1900 đến 2100 |
| `AvatarKey` | Hình đại diện chọn từ bộ có sẵn |
| `PinHash` | Mã PIN riêng của bé, đã băm (US-08) |
| `PinFailedAttempts`, `PinLockedUntil` | Đếm số lần nhập PIN sai; sai nhiều lần thì khóa 5 phút |
| `PinChangedAt` | Lần đổi PIN gần nhất |
| `AllowSearch` | Ba mẹ cho bé dùng ô tìm kiếm không (mặc định 1). Thêm sau bằng `SchemaUpdater`; server kiểm tra khi bé tìm. |
| `IsActive` | 0 khi phụ huynh xóa hồ sơ |

### UserConsents: sự đồng ý của phụ huynh (US-10)
Lưu bằng chứng phụ huynh đã đồng ý điều khoản và chính sách dữ liệu của trẻ.

| Trường | Ý nghĩa |
|---|---|
| `UserId` | Phụ huynh |
| `ChildProfileId` | Đồng ý cho bé nào. NULL nếu là đồng ý điều khoản chung. |
| `ConsentType` | `Terms` (điều khoản sử dụng) hoặc `ChildDataPolicy` (chính sách dữ liệu trẻ em) |
| `Version` | Phiên bản văn bản đã đồng ý, ví dụ `v1.0`. Khi văn bản đổi, có thể hỏi lại phụ huynh. |
| `Decision` | `Accepted`, `Declined` hoặc `Revoked` (đã rút lại) |
| `AcceptedAt`, `RevokedAt`, `DecidedAt` | Các mốc thời gian. Có CHECK bắt buộc khớp với `Decision`, ví dụ `Revoked` thì phải có `RevokedAt`. |
| `IpAddress` | Bằng chứng đồng ý từ đâu |

### UserSettings: cài đặt của phụ huynh
| Trường | Ý nghĩa |
|---|---|
| `Theme` | Giao diện `Light`, `Dark` hoặc `Auto` |
| `Language` | Ngôn ngữ |
| `ParentPinHash` | PIN của phụ huynh (đã băm), dùng để thoát chế độ trẻ thay cho mật khẩu |
| `WeeklyEmail` | Có nhận email báo cáo tuần không |
| `NotifyTimeRequest`, `NotifyContentReport` | Có nhận thông báo khi bé xin thêm giờ hoặc báo cáo nội dung không |
| `PinFailedAttempts`, `PinLockedUntil` | Số lần nhập sai PIN/mật khẩu liên tiếp ở màn hình thoát chế độ trẻ; sai 5 lần thì khóa tới `PinLockedUntil`. Lưu trong SQL (không phải bộ nhớ) nên khởi động lại app không mở khóa. Thêm sau bằng `SchemaUpdater`. |

### AuditLogs: nhật ký thao tác
Mỗi thao tác quan trọng (tạo bài, duyệt nguồn, xóa video, khóa tài khoản...) ghi một dòng. Dùng để biết **ai làm gì, lúc nào**.

| Trường | Ý nghĩa |
|---|---|
| `ActorId` | Người thực hiện |
| `Action` | Hành động, ví dụ `CreateSource`, `DeleteVideo` |
| `EntityType`, `EntityId` | Đối tượng bị tác động, ví dụ `ContentSource` số `12` |
| `Detail` | Mô tả thêm |
| `IpAddress` | Địa chỉ IP |

---

## 4. Môn học và bài học (Sprint 2)

### GradeLevels: các lớp
`Level` (1 đến 5) và `Name` ("Lớp 1"...). Đây là dữ liệu gốc, nằm trong `02-reference-data.sql`.

### Subjects: môn học
| Trường | Ý nghĩa |
|---|---|
| `Code` | Mã môn viết HOA, ví dụ `TOAN`. UNIQUE. Code dùng mã này thay cho Id. |
| `Name`, `Description` | Tên và mô tả |
| `IconKey`, `ColorKey` | Biểu tượng và màu của ô môn trên giao diện của bé |
| `IsActive` | Ẩn môn. Môn đã có bài thì không xóa được, chỉ ẩn được. |

### SubjectGrades: môn dạy cho những lớp nào
Bảng nối môn ↔ lớp. Ví dụ môn Tiếng Anh chỉ dạy lớp 3 đến 5. Khi tạo bài, hệ thống kiểm tra cặp môn + lớp có trong bảng này không.

### ChildSubjects: bé được học những môn nào (US-15)
Bảng nối bé ↔ môn, do phụ huynh bật hoặc tắt. Bé chỉ thấy bài của các môn có trong bảng này. Việc kiểm tra nằm ở `KidContext.Openable`.

### Lessons: bài học
| Trường | Ý nghĩa |
|---|---|
| `SubjectId`, `GradeLevelId` | Bài thuộc môn nào, lớp nào |
| `Title` | Tên bài |
| `Summary` | Mục tiêu hoặc tóm tắt bài, hiển thị là "Mục tiêu bài học" |
| `LessonType` | `Reading` (đọc), `Video` hoặc `Mixed` (kết hợp) |
| `DurationMin` | Thời lượng ước tính (phút) |
| `Status` | `Draft` (nháp) → `InReview` (chờ duyệt) → `Published` (đã đăng) → `Archived` (lưu trữ). **Bé chỉ thấy bài `Published`.** |
| `CreatedById`, `ApprovedById` | Ai soạn, ai duyệt |
| `PublishedAt` | Thời điểm đăng |
| `OwnerParentId` | NULL nghĩa là bài chuẩn của hệ thống. Có giá trị nghĩa là bài do một phụ huynh tự soạn cho con mình. |
| `ShareStatus`, `ShareNote`, `SharedAt`, `ShareReviewedAt` | Phụ huynh chia sẻ bài của mình lên kho cộng đồng: `Pending` → admin duyệt `Approved` hoặc `Rejected`, kèm ghi chú. CHECK: chỉ bài của phụ huynh mới có `ShareStatus`. |
| `RowVersion` | Chống hai admin sửa đè lên nhau |

### LessonContents: các phần nội dung trong bài
Một bài gồm nhiều phần, sắp xếp theo `SortOrder`.

| Trường | Ý nghĩa |
|---|---|
| `ContentKind` | `Reading` (đoạn đọc), `Video`, `Image` hoặc `Document` (PDF) |
| `Body` | Nội dung chữ, dùng cho `Reading` |
| `SourceVideoId` | Video đã duyệt, dùng cho `Video` |
| `FileUrl`, `FileName` | File tải lên, dùng cho `Image` và `Document` |
| `Title`, `SortOrder` | Tiêu đề phần và thứ tự |

CHECK quan trọng: loại nào thì phải có dữ liệu tương ứng. `Video` phải có `SourceVideoId`, `Reading` phải có `Body`, `Image` và `Document` phải có `FileUrl`.

### Tags, LessonTags: thẻ
`Tags.Name` là tên thẻ (UNIQUE). `LessonTags` nối bài ↔ thẻ, dùng để lọc và tìm kiếm trong thư viện.

### LessonRequests, LessonRequestVotes: phụ huynh đề xuất bài học
| Trường | Ý nghĩa |
|---|---|
| `ParentId`, `SubjectId`, `GradeLevelId`, `Topic`, `Note` | Ai đề xuất, cho môn và lớp nào, chủ đề gì |
| `Status` | `Open` → `Planned` (đã lên kế hoạch) → `Done` (đã có bài) hoặc `Declined` (từ chối) |
| `Reply`, `HandledById`, `HandledAt` | Admin trả lời |
| `LessonId` | Bài học được làm ra từ đề xuất này |

`LessonRequestVotes`: phụ huynh khác bấm "cũng muốn có bài này", mỗi người một phiếu.

---

## 5. Nguồn nội dung an toàn (Sprint 3)

### ContentSources: nguồn nội dung (kênh hoặc playlist YouTube)
| Trường | Ý nghĩa |
|---|---|
| `Platform` | `YouTubeChannel`, `YouTubePlaylist` hoặc `Website` |
| `ExternalId` | Mã kênh hoặc playlist trên YouTube, ví dụ `UCHmlTe5HJsDtH2huJo56LZw`. UNIQUE cùng `Platform`, nên không thêm trùng một kênh hai lần. |
| `Name`, `Url`, `Description` | Tên, link, mô tả |
| `Status` | `Suggested` (đề xuất, chờ duyệt) → `Approved` (đã duyệt) / `Rejected` (từ chối) / `Suspended` (tạm ngưng) |
| `SuggestedById` | Ai đề xuất (admin hoặc phụ huynh) |
| `ReviewedById`, `ReviewedAt`, `ReviewNote` | Ai duyệt, lúc nào, lý do |
| `LastSyncedAt`, `LastSyncError` | Lần đồng bộ video gần nhất và lỗi nếu có |
| `SyncRequestedAt` | Admin đã bấm đồng bộ, đang chờ tiến trình nền chạy (NULL khi xong). Lệnh còn dở được chạy tiếp khi app khởi động. Thêm sau bằng `SchemaUpdater`. |
| `LastSyncResult` | Kết quả lần đồng bộ gần nhất bằng chữ, ví dụ "12 video, 3 video mới chờ duyệt" |

### SourceVideos: từng video
| Trường | Ý nghĩa |
|---|---|
| `Platform`, `VideoId` | Mã video YouTube (11 ký tự, ví dụ `dQw4w9WgXcQ`). UNIQUE: một video chỉ lưu một lần dù xuất hiện ở nhiều nguồn. |
| `Title`, `ThumbnailUrl`, `DurationSec` | Tiêu đề, ảnh nhỏ, thời lượng (giây) |
| `PublishedAt` | Ngày đăng trên YouTube |
| `CheckedAt` | Lần cuối hệ thống thấy video này khi đồng bộ |
| `Status` | `Pending` (chờ duyệt) / `Approved` (đã duyệt) / `Blocked` (đã chặn). **Bé chỉ xem được video `Approved` nằm trong nguồn `Approved`.** |
| `ReviewedById`, `ReviewedAt`, `ReviewNote` | Ai duyệt hoặc chặn, lý do. Video tự bị chặn có ghi chú như "Tự chặn khi đồng bộ: ..." |

### SourceVideoSources: video thuộc nguồn nào
Bảng nối nguồn ↔ video. Một video có thể vừa nằm trong kênh vừa nằm trong playlist. `DiscoveredAt` là lần đầu tìm thấy video trong nguồn đó.

### ParentSources: lựa chọn của từng gia đình (US-21)
| Trường | Ý nghĩa |
|---|---|
| `ParentId`, `SourceId` | Gia đình nào, nguồn nào (UNIQUE theo cặp) |
| `Status` | `Blocked` (gia đình chặn nguồn này) hoặc `Allowed` |

Phụ huynh chỉ **thu hẹp** được những gì admin đã duyệt, không mở được nguồn admin chưa duyệt.

### CurationSuggestions: đánh giá nguồn tự động (US-22)
| Trường | Ý nghĩa |
|---|---|
| `SourceId` | Nguồn được chấm |
| `Score` | Điểm 0 đến 100 (CHECK) |
| `SafetyFlags` | Các dấu hiệu rủi ro tìm thấy: của bộ quy tắc (`blocked-keyword`, `risky-topic`, `no-videos`) và của AI (`violence`, `scary`, `adult`, `ads`...) |
| `Summary` | Giải thích từng điểm cộng và trừ |
| `ModelName` | Cách chấm: `rule-v1` (bộ quy tắc) khi không có key hoặc gọi AI lỗi; tên model Claude đã trả lời (ví dụ `claude-opus-5-5`) khi chấm bằng AI |
| `Status` | `Pending` / `Accepted` (admin đồng ý) / `Dismissed` (admin quyết định khác) |
| `DecidedById`, `DecidedAt` | Admin nào quyết định |

### BlockedKeywords: từ khóa bị chặn
| Trường | Ý nghĩa |
|---|---|
| `Keyword` | Từ bị chặn |
| `ParentId` | NULL nghĩa là từ chặn chung cho cả hệ thống (do admin đặt). Có giá trị nghĩa là từ chặn riêng của một gia đình. |

Từ khóa được dùng ở hai chỗ: tự chặn video khi đồng bộ hoặc thêm video, và chặn tìm kiếm của bé.

---

## 6. Hoạt động học của bé (Sprint 4)

### LessonActivities: tiến độ của bé trong từng bài
| Trường | Ý nghĩa |
|---|---|
| `ChildProfileId`, `LessonId` | Bé nào, bài nào. UNIQUE: một bé có một dòng cho mỗi bài. |
| `SessionId` | Học trong phiên nào |
| `ProgressPercent` | % hoàn thành (0 đến 100) |
| `LastPositionSec` | Vị trí xem video lần cuối, để học tiếp từ chỗ dừng |
| `WatchSeconds` | Tổng số giây đã xem |
| `Status` | `Started` hoặc `Completed` |

### Favorites: bài yêu thích
Bé ↔ bài, kèm `FavoritedAt`.

### Assignments: phụ huynh giao bài cho bé
| Trường | Ý nghĩa |
|---|---|
| `LessonId` hoặc `QuizId` | Giao một bài học **hoặc** một bài kiểm tra. CHECK: phải có đúng một trong hai. |
| `AssignedById` | Phụ huynh giao |
| `DueDate`, `Note` | Hạn hoàn thành và lời nhắn |
| `Status` | `Assigned` → `InProgress` → `Completed`, hoặc `Cancelled` |

Bài được giao có thể thuộc lớp khác lớp của bé. Đây là cách phụ huynh mở thêm bài cho con.

### SearchLogs: lịch sử tìm kiếm của bé
`Query` (bé gõ gì), `ResultCount` (bao nhiêu kết quả), `WasBlocked` (có chứa từ khóa bị chặn không). Phụ huynh xem được trong trang lịch sử.

### ContentReports: báo cáo nội dung không phù hợp
| Trường | Ý nghĩa |
|---|---|
| `ChildProfileId` / `ReportedById` | Bé báo hoặc phụ huynh báo (phải có ít nhất một) |
| `LessonId` / `SourceVideoId` | Báo bài hay báo video (phải có ít nhất một) |
| `Reason` | `Inappropriate` (không phù hợp), `Scary` (đáng sợ), `Wrong` (sai kiến thức), `Ads` (quảng cáo), `Other` |
| `Comment` | Mô tả thêm |
| `Status` | `Open` → `InReview` → `Resolved` (đã xử lý) / `Dismissed` (bỏ qua) |
| `HandledById`, `Resolution`, `ResolvedAt` | Admin xử lý thế nào |

---

## 7. Thời gian học và giám sát (Sprint 5)

### TimeLimitPolicies: giới hạn thời gian của từng bé
| Trường | Ý nghĩa |
|---|---|
| `ChildProfileId` | UNIQUE: mỗi bé một chính sách |
| `DailyMinutes` | Số phút được học mỗi ngày (0 đến 1440) |
| `StudyStart`, `StudyEnd` | Khung giờ được học, ví dụ 07:00 đến 20:00. CHECK: phải có cả hai hoặc không có cả hai. |
| `BreakEveryMinutes` | Nhắc nghỉ sau mỗi bao nhiêu phút |

### TimeLimitOverrides: giới hạn riêng theo thứ trong tuần
`Weekday` (1 đến 7) và `Minutes`. Ví dụ: cuối tuần được học 90 phút thay vì 60.

### LearningSessions: phiên học
| Trường | Ý nghĩa |
|---|---|
| `StartedAt`, `EndedAt` | Bắt đầu và kết thúc phiên |
| `LastHeartbeatAt` | Trình duyệt của bé gửi tín hiệu "vẫn đang học" định kỳ. Mất tín hiệu thì coi như phiên đã dừng. |
| `ActiveSeconds` | Số giây học thật sự |
| `EndReason` | `Exit` (bé thoát), `TimeUp` (hết giờ), `Timeout` (mất tín hiệu), `ParentPin` (phụ huynh kết thúc) |

### DailyUsages: tổng thời gian học mỗi ngày
`UsageDate` và `Seconds`. UNIQUE theo bé + ngày. Dùng để so với `DailyMinutes` và quyết định khi nào hết giờ.

### TimeExtensionRequests: bé xin thêm giờ
| Trường | Ý nghĩa |
|---|---|
| `ForDate`, `RequestedMinutes`, `Reason` | Xin cho ngày nào, bao nhiêu phút, vì sao |
| `Status` | `Pending` → `Approved` / `Denied`, hoặc `Expired` (hết ngày mà chưa ai trả lời) |
| `GrantedMinutes` | Số phút phụ huynh cho. CHECK: không vượt quá số phút bé xin. |
| `RowVersion` | Chống hai phụ huynh (bố và mẹ) cùng duyệt một lúc |

---

## 8. Câu hỏi và bài tập (Sprint 6)

### Questions: ngân hàng câu hỏi
| Trường | Ý nghĩa |
|---|---|
| `LessonId` | Câu hỏi thuộc bài nào |
| `Text`, `Explanation` | Nội dung câu hỏi và lời giải thích hiện sau khi bé trả lời |
| `QuestionType` | `SingleChoice` (một đáp án), `MultiChoice` (nhiều đáp án), `TrueFalse` (đúng/sai) |
| `Difficulty` | Độ khó 1 đến 3 |
| `Origin` | `Manual` (admin tự viết) hoặc `AiSuggested` (Claude gợi ý, US-40) |
| `AiModelName` | Model AI đã gợi ý (nếu có) |
| `SourceVideoId` | Câu hỏi về video nào (nếu có) |
| `Status` | `Draft` / `InReview` / `Published` / `Archived`. Câu do AI gợi ý phải được admin duyệt mới dùng được. |
| `KidExplanation` | "Giải thích cách khác" cho bé khi làm sai (tối đa 1000 ký tự). Admin viết hoặc AI soạn nháp rồi admin lưu. Thêm sau bằng `SchemaUpdater`. |
| `KidExplanationBy` | `admin` nếu tự viết, hoặc model đã soạn (`claude-...`, `offline-template-v1`) |

### QuestionOptions: các đáp án
`Text`, `IsCorrect` (đáp án đúng hay không), `SortOrder`.

### Quizzes: bài kiểm tra
| Trường | Ý nghĩa |
|---|---|
| `LessonId`, `Title` | Bài kiểm tra của bài học nào |
| `PassPercent` | Đạt từ bao nhiêu % trở lên |
| `TimeLimitSec` | Giới hạn thời gian làm bài (NULL là không giới hạn) |
| `ShuffleQuestions` | Có xáo trộn thứ tự câu hỏi không |
| `Status` | Giống trạng thái của bài học |

### QuizQuestions: câu hỏi nằm trong bài kiểm tra nào
Bảng nối quiz ↔ câu hỏi, kèm `SortOrder` và `Points` (số điểm của câu, phải lớn hơn 0).

### QuizAttempts: một lần bé làm bài
| Trường | Ý nghĩa |
|---|---|
| `QuizId`, `ChildProfileId`, `AssignmentId` | Bài nào, bé nào, có phải bài được giao không |
| `StartedAt`, `SubmittedAt` | Bắt đầu và nộp bài. `SubmittedAt` NULL nghĩa là đang làm. |
| `ScorePercent`, `CorrectCount`, `TotalCount` | Kết quả. CHECK: số câu đúng không vượt tổng số câu. |
| `Passed` | Đạt hay không (so với `PassPercent`) |

### QuizAttemptAnswers: từng câu bé trả lời
`QuestionId`, `SelectedOptionId` (đáp án bé chọn), `IsCorrect`. UNIQUE theo lần làm + câu hỏi: mỗi câu chỉ trả lời một lần.

---

## 9. Tiến độ, lộ trình, huy hiệu (Sprint 7)

### LearningPaths, LearningPathItems: lộ trình học
- `LearningPaths`: lộ trình cho một môn và một lớp (`Name`, `SubjectId`, `GradeLevelId`, `Description`, `IsActive`).
- `LearningPathItems`: các bài trong lộ trình theo thứ tự (`SortOrder`). `IsRequired` cho biết bài đó bắt buộc hay tự chọn.

### ChildPaths: bé đang đi lộ trình nào
`Status` (`NotStarted` / `InProgress` / `Completed`) và `CurrentItemId` (bé đang ở bài nào của lộ trình).

### SubjectProgresses: tiến độ từng môn
`CompletedLessons` / `TotalLessons` (đã xong mấy bài trên tổng số bài) và `AvgQuizPercent` (điểm kiểm tra trung bình). Đây là bảng tổng hợp sẵn để trang tiến độ tải nhanh, không phải tính lại mỗi lần.

### Badges, ChildBadges: huy hiệu
- `Badges`: định nghĩa huy hiệu. `CriteriaType` là điều kiện nhận: `LessonsCompleted` (số bài đã học), `QuizPerfect` (số bài kiểm tra đạt 100%), `StreakDays` (số ngày học liên tiếp), `MinutesTotal` (tổng số phút). `Threshold` là mốc cần đạt, ví dụ 10 bài.
- `ChildBadges`: bé nào nhận huy hiệu nào, lúc nào (`AwardedAt`). UNIQUE: mỗi huy hiệu chỉ nhận một lần.

### Streaks: chuỗi ngày học liên tiếp
`CurrentDays` (chuỗi hiện tại), `LongestDays` (chuỗi dài nhất từng có), `LastActiveDate` (ngày học gần nhất). CHECK: `LongestDays >= CurrentDays`.

### LearningGoals: mục tiêu tuần
`WeekStart` (thứ Hai đầu tuần), `TargetLessons` (số bài) và/hoặc `TargetMinutes` (số phút). Phải đặt ít nhất một trong hai.

---

## 10. Thống kê, thông báo, dữ liệu cá nhân (Sprint 8)

### WeeklyReports: báo cáo tuần gửi phụ huynh
`TotalMinutes`, `LessonsCompleted`, `QuizAttempts`, `AvgQuizPercent`, `StrongSubject` (môn học tốt), `NeedsAttentionSubject` (môn cần chú ý), `Summary`, `GeneratedAt` (lúc tạo), `EmailedAt` (lúc gửi email).

### SystemMetricsDaily: số liệu toàn hệ thống mỗi ngày (US-48)
`ActiveParents`, `ActiveChildren`, `TotalMinutes`, `LessonsCompleted`, `OpenReports`, `NewSources`. Job chạy hằng ngày (`Services/DailyJobs.cs`) ghi một dòng cho mỗi ngày đã qua, để trang thống kê của admin không phải tính lại từ đầu.

### Notifications: thông báo
| Trường | Ý nghĩa |
|---|---|
| `UserId` | Gửi cho ai |
| `NotificationType` | `TimeRequest`, `WeeklyReport`, `ContentReport`, `Assignment`, `Badge`, `Security`, `System` |
| `Title`, `Body`, `LinkUrl` | Nội dung và link mở khi bấm vào |
| `IsRead` | Đã đọc chưa (dùng cho chấm đỏ trên chuông) |

### DataRequests: yêu cầu về dữ liệu cá nhân
| Trường | Ý nghĩa |
|---|---|
| `ParentId`, `ChildProfileId` | Ai yêu cầu, cho bé nào |
| `RequestType` | `Export` (tải về dữ liệu của bé) hoặc `Delete` (xóa dữ liệu của bé) |
| `Status` | `Pending` → `Processing` → `Done` / `Rejected` |
| `ChildName` | Giữ lại tên bé, vì sau khi xóa thì hồ sơ không còn để tra |
| `ProcessedById`, `DecisionNote`, `CompletedAt` | Admin xử lý thế nào |

---

## 11. Câu hỏi hay gặp khi báo cáo

**Vì sao `Status` lưu dạng chữ mà không dùng số?**
Đọc thẳng trong database là hiểu ngay (`Approved` dễ hiểu hơn `2`), và CHECK vẫn giới hạn được các giá trị hợp lệ.

**Vì sao nhiều chỗ ẩn (`IsActive = 0`) thay vì xóa?**
Để giữ lịch sử. Ví dụ môn đã có bé học thì xóa đi sẽ làm mất tiến độ của bé.

**Vì sao cần cả UNIQUE và kiểm tra trong code?**
Code kiểm tra để báo lỗi thân thiện cho người dùng. UNIQUE trong database là lớp chặn cuối cùng, phòng khi hai yêu cầu đến cùng lúc mà cả hai đều qua được bước kiểm tra trong code.

**Làm sao phân biệt bài chuẩn và bài của phụ huynh?**
Dựa vào `Lessons.OwnerParentId`: NULL là bài chuẩn, có giá trị là bài của gia đình đó.

**Bé chỉ xem được bài nào?**
Bài phải `Published`, môn phải đang dùng (`Subjects.IsActive`), và bài phải đúng lớp của bé và thuộc môn phụ huynh đã chọn (`ChildSubjects`), **hoặc** là bài được giao riêng (`Assignments`). Điều kiện này nằm trong `KidContext.Openable`.

**Bé chỉ xem được video nào?**
Video phải `Approved`. Nếu video thuộc nguồn nào đó thì nguồn phải `Approved`, và gia đình không chặn nguồn đó trong `ParentSources`.
