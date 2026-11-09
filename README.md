# SafeLearn Kids — ASP.NET Core MVC

Website học tập an toàn cho trẻ em. Source hiện gồm Razor/CSS/JavaScript, controller khung, ASP.NET Core Identity và model/mapping SQL. Nhiều luồng nghiệp vụ vẫn là UI demo, không phải backend hoàn chỉnh.

Stack trong project: C# · ASP.NET Core MVC `net10.0` · EF Core · SQL Server · Razor · Bootstrap 5 · JavaScript.

## Chạy với SQL Server

Chạy tại **repo root**, thư mục chứa `SafeLearnKids.Web.csproj`:

```powershell
dotnet tool install -g Microsoft.Web.LibraryManager.Cli --version 3.0.114   # nếu máy chưa có LibMan CLI
libman restore
# Thay <SQL_SERVER> bằng SQL instance của máy bạn; giá trị chỉ áp dụng cho terminal hiện tại:
$env:ConnectionStrings__DefaultConnection = "Server=<SQL_SERVER>;Database=hoctapchotre;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
# Chỉ thêm các bảng phụ Identity còn thiếu vào DB hiện có (chạy một lần):
sqlcmd -S "<SQL_SERVER>" -d hoctapchotre -E -C -I -b -i scripts\ensure-identity-schema.sql
dotnet run --project SafeLearnKids.Web.csproj --launch-profile http
```

Cần .NET SDK 10 và SQL Server có schema `hoctapchotre` hiện hành của dự án.

Cấu hình kết nối qua `ConnectionStrings:DefaultConnection` hoặc biến môi trường `ConnectionStrings__DefaultConnection`. `Demo:UseInMemoryDatabase=false` và `Demo:SeedDemoAccounts=false`. Mỗi máy dùng SQL instance riêng; không đưa cấu hình máy cá nhân hoặc mật khẩu SQL vào Git.

Đăng ký ở `/register` ghi vào `dbo.Users` và gán vai trò ở `dbo.UserRoles`; tài khoản vẫn còn sau khi khởi động lại. Console ghi SQL server/database đang sử dụng. Tài khoản từng tạo trong InMemory không tự chuyển sang SQL, cần đăng ký lại.

Đăng ký mới chuyển tới `/check-email`, chưa tự đăng nhập. Xác minh email trước khi đăng nhập; tài khoản tạo từ lượt SQL trước mà chưa xác minh cũng dùng form gửi lại ở trang này. `/recover` gửi hướng dẫn đặt lại; `/change-password` yêu cầu đăng nhập và mật khẩu hiện tại. Đăng xuất, đổi hoặc đặt lại mật khẩu thu hồi các phiên cũ ở máy chủ.

## Tài khoản Admin đầu tiên

Đăng ký ở `/register` chỉ tạo Parent, nên Admin đầu tiên được tạo từ cấu hình riêng của từng máy. Đặt mật khẩu của bạn (đạt chính sách mật khẩu) rồi khởi động lại app:

```powershell
dotnet user-secrets set "Bootstrap:AdminEmail" "<email-quan-tri>"
dotnet user-secrets set "Bootstrap:AdminPassword" "<mat-khau-rieng>"
```

Môi trường khác dùng `Bootstrap__AdminEmail`, `Bootstrap__AdminPassword`. Bước này chỉ chạy khi chưa có Admin nào đăng nhập được, nên hạ quyền về sau không bị đảo ngược. Tài khoản có sẵn cùng email sẽ bị thay mật khẩu. Không đưa giá trị này vào `appsettings*.json`/Git; xóa cấu hình sau khi đăng nhập được.

## Quản lý người dùng

`/admin-users` đọc trực tiếp từ SQL: tìm kiếm, lọc theo trạng thái/vai trò, phân trang, xuất CSV. `/admin-users/{id}` có khóa/mở khóa, đổi vai trò Parent/Admin, gửi lại email xác minh, gửi đặt lại mật khẩu, lịch sử đăng nhập và nhật ký quản trị. Khóa hoặc đổi vai trò kết thúc ngay mọi phiên của tài khoản đó. Admin không thể tự khóa hay tự đổi vai trò, nên hệ thống luôn còn ít nhất một Admin. Vai trò Child, Moderator, Teacher chỉ xem được tại đây.

Admin thêm người dùng ở `/admin-users/new`: tài khoản chưa có mật khẩu, người dùng nhận email mời, xác minh rồi tự đặt mật khẩu. `/admin-users/{id}/edit` sửa họ tên và email; đổi email kết thúc mọi phiên và yêu cầu xác minh lại địa chỉ mới, Admin không tự đổi email của mình ở đây. Xóa là vĩnh viễn và không thể tự xóa; tài khoản có hồ sơ bé hoặc được dữ liệu dùng chung tham chiếu sẽ bị từ chối, khi đó hãy khóa tài khoản.

Ảnh đại diện nhận JPG, PNG, WebP tối đa 2 MB, xác định loại theo nội dung file, lưu trong `wwwroot/uploads/avatars` với tên ngẫu nhiên (không commit). Parent đổi ảnh ở `/settings`, Admin đổi cho mọi tài khoản ở trang chi tiết. Môi trường triển khai cần giữ thư mục này qua các lần deploy. Sau khi pull, chạy lại `scripts\ensure-identity-schema.sql` để thêm cột `Users.AvatarUrl`; thiếu cột này app không đọc được tài khoản.

Thao tác quản trị ghi vào `dbo.AuditLogs`, đăng nhập ghi vào `dbo.LoginHistories`, đồng ý điều khoản khi đăng ký ghi vào `dbo.UserConsents`. Kiểm thử: `py scripts\smoke-admin-users.py` sau khi build.

## Môn học, bài học và thư viện (Sprint 2)

`/admin-catalog` quản lý môn học (tên, mô tả, màu, biểu tượng, thứ tự, ẩn/hiện) và các lớp áp dụng trong `dbo.SubjectGrades`; mã môn không đổi được sau khi tạo vì hồ sơ bé dùng nó làm khóa. Bài học chỉ tạo được ở lớp mà môn áp dụng. `/admin-lesson/{id}` sửa bài, gắn nhãn (tạo nhãn mới tự động), thêm nội dung theo thứ tự: bài đọc, video đã duyệt trong `dbo.SourceVideos`, tài liệu PDF hoặc ảnh JPG/PNG/WebP tối đa 5 MB (kiểm tra theo nội dung tệp, lưu trong `wwwroot/uploads/lessons`). Trạng thái: Bản nháp → Chờ duyệt → Đã xuất bản → Lưu trữ; xuất bản cần ít nhất 1 nội dung, môn đang dùng và mọi video còn được duyệt. Hai người sửa cùng bài thì bản lưu sau bị từ chối (RowVersion). Chỉ xóa được bài nháp hoặc đã lưu trữ chưa được dùng ở nơi khác; môn đã có bài hoặc bé đang học thì chỉ ẩn được. Mọi thao tác ghi `dbo.AuditLogs`.

`/library` của phụ huynh chỉ hiện bài đã xuất bản thuộc môn đang dùng, lọc theo môn, lớp, loại, nhãn và tìm kiếm; `/lesson-detail/{id}` hiện bài đọc, tệp và video YouTube đã duyệt qua trình phát `youtube-nocookie`. Danh sách môn chọn cho bé lấy từ `dbo.Subjects`: môn đang ẩn không thêm mới được nhưng bé đã có vẫn giữ. Giao bài cho bé và báo cáo nội dung thuộc Sprint 4–5. Kiểm thử: `py scripts\smoke-catalog.py` sau khi build.

## Nguồn nội dung an toàn (Sprint 3)

`/admin-curate` quản lý nguồn: dán liên kết danh sách phát (`list=...`), kênh dạng `youtube.com/channel/UC...` hoặc website (chỉ ghi nhận, không tải). Liên kết dạng `@tên-kênh` chưa hỗ trợ. Nguồn mới ở trạng thái chờ duyệt; Admin duyệt, từ chối (cần lý do) hoặc tạm ngưng (cần lý do). Đồng bộ lấy danh sách video mới vào `dbo.SourceVideos` ở trạng thái chờ duyệt; tiêu đề chứa từ khóa trong `dbo.BlockedKeywords` (từ khóa chung) bị chặn tự động. `/admin-videos` là hàng chờ duyệt/chặn từng video, và có ô **Thêm video lẻ** để thêm một video mà không cần thêm cả kênh. Ô này nhận link `watch?v=`, `youtu.be/`, `shorts/`, `embed/`, `live/` hoặc mã video. Có key thì lấy tiêu đề, kênh và thời lượng từ Data API; không có key thì lấy tiêu đề và kênh qua oEmbed công khai của YouTube. Video riêng tư, đã xóa hoặc không cho nhúng bị từ chối; tiêu đề chứa từ khóa bị chặn thì tự chặn. Video mới thêm ở trạng thái chờ duyệt, hoặc được duyệt ngay nếu Admin chọn "Duyệt luôn". Video lẻ không thuộc nguồn nào nên chỉ cần chính video được duyệt. Một video chỉ phát cho bé khi video đã duyệt và ít nhất một nguồn chứa nó đang được duyệt; tạm ngưng nguồn sẽ gỡ video khỏi thư viện và chặn xuất bản bài dùng video đó.

"Đánh giá tự động" ghi vào `dbo.CurationSuggestions` với `ModelName = rule-v1`: bộ quy tắc minh bạch dựa trên loại nguồn, từ khóa bị chặn, từ ngữ không phù hợp và tiêu đề video, giải thích từng điểm cộng/trừ. Đây không phải mô hình AI; quản trị viên quyết định, và quyết định đó đóng gợi ý là đồng ý hoặc không.

Phụ huynh ở `/sources` chặn hoặc cho phép lại từng nguồn đã duyệt cho riêng gia đình mình (`dbo.ParentSources`) và đề xuất nguồn mới (tối đa 5 đề xuất chờ duyệt mỗi gia đình), xem lý do nếu bị từ chối.

**YouTube API key.** Để trống thì đồng bộ dùng RSS công khai của YouTube (15 video mới nhất, không có thời lượng). Có key thì dùng YouTube Data API v3: đọc toàn bộ danh sách tải lên (tối đa 200 video mỗi lần), lấy thời lượng và tự chặn video YouTube không cho nhúng. Bật **YouTube Data API v3** trong Google Cloud, tạo API key, rồi đặt riêng trên máy (không ghi vào `appsettings.json`/Git):

```powershell
dotnet user-secrets set "YouTube:ApiKey" "<api-key>"
```

Môi trường khác dùng biến `YouTube__ApiKey`. Key không được ghi vào log. Kiểm thử: `py scripts\smoke-sources.py` sau khi build (dùng YouTube giả cục bộ, không cần mạng hay key thật).

## Trang học của bé (Sprint 4)

Bé không có tài khoản riêng: phụ huynh đăng nhập, bấm **Chế độ trẻ**, chọn bé. Lựa chọn lưu trong cookie `.SafeLearnKids.Kid` và mỗi request đều kiểm tra bé thuộc phụ huynh đang đăng nhập. Đổi bé ở màn hình chọn bé vẫn giữ chế độ trẻ; chỉ màn hình PIN (`/kids-exit`) mới kết thúc chế độ trẻ (xem Sprint 8). Bé chỉ mở được bài đã xuất bản thuộc môn đang dùng, và (a) đúng lớp và môn ba mẹ chọn cho bé, hoặc (b) được giao riêng. Video theo cùng quy tắc với thư viện (video và nguồn được duyệt, gia đình không chặn nguồn).

- Phụ huynh giao bài ở `/lesson-detail/{id}`: chọn bé, hạn (từ hôm nay đến 1 năm), lời nhắn; mỗi bé chỉ có một bài giao đang mở cho mỗi bài học; hủy được (`dbo.Assignments`).
- `/kids-home`: bài ba mẹ giao, "Học tiếp", các môn của bé kèm số bài đã xong, bài yêu thích.
- `/kids-lesson?id=` chia bài thành từng phần nội dung theo thứ tự; "Con xong phần này rồi" lưu tiến độ vào `dbo.LessonActivities`, lần sau học tiếp từ phần đang dở. Server tự đếm số phần, chỉ cho hoàn thành khi đã học hết; hoàn thành cũng đóng bài được giao. Thời gian học cộng mỗi 30 giây khi trang đang mở (mỗi lần tối đa 60 giây).
- Yêu thích lưu ở `dbo.Favorites`. `/kids-search` chỉ tìm trong bài bé được mở, ghi `dbo.SearchLogs`; từ khóa bị chặn (chung hoặc của gia đình) không trả kết quả.

Công tắc "Cho phép tìm kiếm" vẫn lưu ở trình duyệt (chưa có cột trong SQL). Giờ học ở Sprint 5, bài tập ở Sprint 6, huy hiệu ở Sprint 7. Kiểm thử: `py scripts\smoke-learning.py` sau khi build.

## Thời gian học và giám sát (Sprint 5)

`/time` đặt cho từng bé: số phút mỗi ngày (mặc định 60 khi chưa đặt), số phút riêng từng thứ, khung giờ được học, nhắc nghỉ sau mỗi N phút (`dbo.TimeLimitPolicies`, `dbo.TimeLimitOverrides`). Trang bài học của bé báo thời gian mỗi 30 giây (mỗi lần tối đa 60 giây) vào `dbo.DailyUsages` và `dbo.LearningSessions`; khi hết phút hoặc ngoài khung giờ, server chặn mọi trang học của bé và chuyển tới `/kids-time-up`. Ngày và khung giờ tính theo giờ máy chủ. Chỉ thời gian trên trang bài học được tính.

Bé xin thêm 5/10/15/30 phút ở trang hết giờ (một yêu cầu chờ tại một thời điểm, tối đa 3 lần/ngày, không xin được khi đang ngoài khung giờ); phụ huynh cho thêm (không quá số bé xin) hoặc từ chối ở `/time`. Yêu cầu từ ngày trước tự hết hạn. Thông báo cho phụ huynh ghi vào `dbo.Notifications` (trang xem thông báo thuộc Sprint 8).

`/history` theo từng bé: bài đã học (tiến độ, thời gian xem), số phút 14 ngày, tìm kiếm (kể cả từ khóa bị chặn), báo cáo của bé; xuất CSV. Bé báo cáo bài ở trang học, phụ huynh báo cáo ở thư viện (`dbo.ContentReports`). `/admin-reports`: nhận xem, giữ nội dung hoặc gỡ (chặn video, hoặc lưu trữ bài nếu báo cáo về bài), bắt buộc ghi kết quả; gia đình nhận thông báo và thấy kết quả trong lịch sử. Kiểm thử: `py scripts\smoke-time.py` sau khi build.

## Câu hỏi và bài tập (Sprint 6)

- **Ngân hàng câu hỏi (US-35)**, `/admin-questions`: chọn bài học, soạn câu trắc nghiệm (2–4 đáp án, đúng 1 đáp án đúng) hoặc Đúng/Sai, độ khó 1–3, giải thích đáp án (`dbo.Questions`, `dbo.QuestionOptions`). Trạng thái: Nháp, Chờ duyệt, Đã duyệt, Lưu trữ.
  - Câu đã có bé trả lời thì khóa loại câu và đáp án để kết quả cũ vẫn đúng; vẫn sửa được nội dung, giải thích và trạng thái.
  - Câu đang nằm trong bài tập hoặc đã có người trả lời thì không xóa được, chỉ lưu trữ.
  - Loại "MultiChoice" trong bảng không được dùng, vì `dbo.QuizAttemptAnswers` chỉ lưu một đáp án mỗi câu.
- **Bài tập (US-36)**, `/admin-quizzes/new?lesson=`: chọn câu đã duyệt của bài, điểm đạt (%), thời gian làm bài (1–60 phút hoặc không giới hạn), xáo trộn câu (`dbo.Quizzes`, `dbo.QuizQuestions`). Bài tập chỉ mở cho bé khi ở trạng thái "Đang mở" và có ít nhất 1 câu đã duyệt. Khi đã có bé làm, danh sách câu được giữ nguyên và bài tập không xóa được.
- **Giao bài tập (US-37)**, thẻ "Bài tập kiểm tra" ở `/lesson-detail/{id}`: chọn bài tập, chọn bé, **bắt buộc hạn nộp** (từ hôm nay đến 1 năm), lời nhắn. Mỗi bé chỉ có một bài tập đang mở cho mỗi bài tập; kiểm tra và ghi nằm trong một transaction serializable. Bài tập được giao mở được cho bé kể cả khi khác lớp.
- **Làm bài và chấm điểm (US-38)**, `/kids-quiz?id=`:
  - Bé làm từng câu một, có thanh tiến độ và đồng hồ đếm ngược. Hết giờ thì tự nộp; nộp khi còn câu bỏ trống thì hỏi lại trước.
  - Tải lại trang sẽ tiếp tục lượt đang làm, không tính lại giờ. Lượt quá giờ (thời gian làm bài cộng 60 giây dự phòng) bị đóng và câu trả lời gửi muộn không được tính.
  - Server chấm theo điểm từng câu trong `dbo.QuizQuestions`. Đáp án không thuộc câu hỏi bị coi là chưa trả lời. Kết quả lưu trong `dbo.QuizAttempts` và `dbo.QuizAttemptAnswers`.
  - Lần nộp đầu tiên của bài tập được giao sẽ hoàn thành bài giao và báo cho phụ huynh. Các lần làm lại sau đó là luyện tập.
  - Thời gian trên trang làm bài cũng được tính vào giờ học. Hết giờ học thì bài làm vẫn được nộp, không bị mất.
- **Kết quả và giải thích (US-39)**:
  - Bé xem điểm, số sao, đáp án đã chọn, đáp án đúng và giải thích ở `/kids-result?id=`.
  - Phụ huynh xem danh sách lượt làm ở `/history` (mục "Bài tập đã làm") và chi tiết từng câu ở `/quiz-result/{id}`.
  - Bài nộp sau hạn được đánh dấu "nộp muộn".
- **Gợi ý câu hỏi (US-40)**: nút "Gợi ý câu hỏi" (1–5 câu) lưu câu gợi ý ở trạng thái **Chờ duyệt** với `Origin = AiSuggested` và `AiModelName`. Câu gợi ý không dùng được trong bài tập cho tới khi Admin duyệt; Admin có thể sửa, duyệt hoặc từ chối. Danh sách chờ duyệt của mọi bài ở nút "Chờ duyệt".
  - **Không có key**: dùng bộ gợi ý cục bộ `offline-rule-v1`. Bộ này tạo câu điền từ/điền số và câu Đúng/Sai từ nội dung chữ của bài, không phải AI.
  - **Có key**: gọi Claude (`claude-opus-5-5`) qua SDK `Anthropic`, nhận JSON theo schema rồi kiểm tra lại từng câu như câu soạn tay. Đặt key riêng trên máy, không ghi vào `appsettings.json` hay Git:

```powershell
dotnet user-secrets set "Ai:ApiKey" "<anthropic-api-key>"
```

Môi trường khác dùng biến `Ai__ApiKey`; đổi model bằng `Ai:Model`. Kiểm thử: `py scripts\smoke-quiz.py` sau khi build (chạy không cần key).

## Tiến độ, lộ trình và thành tích (Sprint 7)

Mọi số liệu được tính lại trên server mỗi khi bé học: mỗi 30 giây trên trang bài học hoặc trang làm bài, khi hoàn thành một bài, và khi nộp bài tập (`Services/ProgressService.cs`).

- **Tiến độ theo môn (US-41)**: `dbo.SubjectProgresses` lưu số bài đã xong trên tổng số bài đã xuất bản của môn ở lớp của bé, cùng điểm bài tập trung bình (lấy điểm cao nhất của mỗi bài tập). Phụ huynh xem ở `/progress`.
- **Lộ trình học (US-42)**:
  - Admin quản lý ở `/admin-paths`. Mỗi lộ trình là chuỗi bài đã xuất bản của một môn và một lớp, có thứ tự và đánh dấu bài bắt buộc (`dbo.LearningPaths`, `dbo.LearningPathItems`). Môn và lớp cố định sau khi tạo. Lộ trình đã có bé theo thì không xóa được, chỉ tắt "Đang dùng".
  - Phụ huynh chọn hoặc bỏ lộ trình cho bé ở `/progress`. Chỉ chọn được lộ trình đúng lớp và đúng môn của bé (`dbo.ChildPaths`).
  - Bé thấy bài tiếp theo ở trang học. Lộ trình hoàn thành khi bé học xong mọi bài bắt buộc.
- **Huy hiệu (US-43)**:
  - Admin quản lý ở `/admin-badges`. Tiêu chí: số bài hoàn thành, số ngày học liên tiếp (lấy kỷ lục), số bài tập đạt 100%, tổng số phút học.
  - Bé tự nhận huy hiệu khi đạt mốc (`dbo.ChildBadges`, mỗi huy hiệu một lần). Bé thấy thông báo ngay; phụ huynh nhận thông báo loại `Badge`.
  - `/kids-badges` hiện huy hiệu đã đạt, tiến độ tới từng huy hiệu chưa đạt, và số sao (0–5 sao theo điểm cao nhất mỗi bài tập).
  - Huy hiệu đã có bé nhận thì không xóa được; đổi tiêu chí không thu hồi huy hiệu đã trao.
- **Chuỗi ngày học (US-44)**, `dbo.Streaks`: mỗi ngày có học tính một lần. Học ngày liền sau thì chuỗi tăng 1; bỏ một ngày thì chuỗi bắt đầu lại từ 1, kỷ lục vẫn giữ. Phụ huynh chọn lộ trình không được tính là một ngày học.
- **Mục tiêu tuần (US-45)**, `dbo.LearningGoals`: phụ huynh đặt số bài và/hoặc số phút cho tuần hiện tại (thứ Hai đến Chủ nhật). Trang gợi ý mục tiêu tuần trước kèm kết quả. Bé và phụ huynh thấy tiến độ tuần.
- **Ôn câu làm sai (US-46)**, `/kids-review`:
  - Liệt kê các câu mà lần trả lời gần nhất của bé sai hoặc bỏ trống. Bé chọn lại đáp án để kiểm tra; trả lời đúng thì thấy giải thích.
  - Phần luyện này không lưu điểm. Câu chỉ rời danh sách khi bé làm lại bài tập và trả lời đúng.
  - Phụ huynh thấy số câu cần ôn, cùng các bài tập chưa đạt, ở mục "Cần chú ý".

Kiểm thử: `py scripts\smoke-progress.py` sau khi build.

## Báo cáo, thống kê, thông báo và dữ liệu (Sprint 8)

- **Báo cáo tuần (US-47)**, `/weekly`: theo từng bé và từng tuần (thứ Hai đến Chủ nhật, 12 tuần gần nhất).
  - Gồm số phút mỗi ngày, số bài hoàn thành, số lượt và điểm bài tập, chia theo môn, môn làm tốt nhất, môn cần ôn, huy hiệu mới, mục tiêu tuần.
  - Tuần đã qua được lưu vào `dbo.WeeklyReports`; tuần hiện tại tính trực tiếp.
  - Có nút In / lưu PDF (dùng hộp thoại in của trình duyệt) và xuất CSV các tuần.
- **Job chạy nền** (`Services/DailyJobs.cs`, cấu hình mục `Jobs`; mặc định chạy mỗi 60 phút, lượt đầu sau 20 giây):
  - Tạo báo cáo tuần trước cho mọi bé chưa có báo cáo, gửi thông báo `WeeklyReport` cho phụ huynh.
  - Gửi email báo cáo nếu phụ huynh bật "Gửi báo cáo tuần qua email" và email đã xác minh.
  - Chụp số liệu từng ngày (30 ngày gần nhất) vào `dbo.SystemMetricsDaily`.
  - Mỗi bước chỉ điền phần còn thiếu, nên chạy lại hoặc chạy nhiều bản app cùng lúc đều an toàn.
  - Lần đầu chạy app bản mới, job sẽ tự điền báo cáo tuần trước và số liệu 30 ngày cho dữ liệu đang có.
- **Tổng quan Admin (US-48)**, `/admin-home`:
  - Số phụ huynh, số bé, số bé học trong 7 ngày, số phút và số bài hôm nay, số bài và video đang có.
  - Biểu đồ 14 ngày: ngày cũ lấy từ `SystemMetricsDaily`, hôm nay tính trực tiếp.
  - "Việc cần làm": báo cáo nội dung, nguồn và video chờ duyệt, câu hỏi gợi ý, yêu cầu xóa dữ liệu, đăng nhập thất bại trong 24 giờ.
- **Thống kê nội dung (US-49)**, `/admin-stats` (7, 30 hoặc 90 ngày):
  - Theo môn; bài học nhiều bé học (tỉ lệ hoàn thành, phút xem, yêu thích, báo cáo).
  - Bài tập (lượt làm, điểm trung bình, tỉ lệ đạt); câu hỏi hay bị làm sai.
  - Từ khóa bé tìm, kể cả từ khóa bị chặn và từ khóa không có kết quả.
  - Chỉ hiện số liệu tổng, không hiện bé cụ thể. Xuất CSV bài học.
- **Thông báo và cài đặt (US-50)**:
  - `/notifications`: lọc chưa đọc, mở thông báo (đánh dấu đã đọc rồi đi tới link; chỉ đi tới link trong trang), đánh dấu đọc tất cả, xóa. Số chưa đọc trên sidebar và chuông lấy từ server.
  - `/settings`: giao diện (sáng, tối, theo máy), bật/tắt thông báo xin thêm giờ và báo cáo nội dung, email báo cáo tuần (`dbo.UserSettings`), lịch sử đăng nhập của chính mình.
  - **PIN và khóa chế độ trẻ**:
    - `/pin` đặt hoặc đổi PIN 4 số, cần mật khẩu tài khoản. PIN lưu dạng băm trong `UserSettings.ParentPinHash` (PasswordHasher của Identity), không còn lưu trong trình duyệt.
    - Khi đang ở chế độ trẻ, server chặn mọi trang của khu phụ huynh: GET quay về `/kids-home`, POST bị từ chối 403. Gõ địa chỉ trực tiếp cũng không vào được.
    - Thoát ở `/kids-exit` bằng PIN, hoặc bằng mật khẩu tài khoản khi chưa đặt PIN hay quên PIN. Sai 5 lần thì khóa 5 phút.
- **Nhật ký (US-51)**, `/admin-audit`:
  - Tab Thao tác (`dbo.AuditLogs`): lọc theo người thực hiện, loại thao tác, khoảng ngày.
  - Tab Đăng nhập (`dbo.LoginHistories`): lọc theo email/IP, thành công/thất bại.
  - Phân trang, xuất CSV (tối đa 5000 dòng).
- **Dữ liệu của bé (US-52)**:
  - `/privacy`: tải toàn bộ dữ liệu của bé dạng JSON (hồ sơ, bài học, bài tập và câu trả lời, giờ học, tìm kiếm, báo cáo, huy hiệu, lộ trình, mục tiêu, báo cáo tuần). Mỗi lần tải được ghi vào `dbo.DataRequests`.
  - Phụ huynh gửi yêu cầu xóa (gõ tên bé để xác nhận) và có thể hủy khi chưa được xử lý.
  - `/admin-data-requests`: Admin xóa hồ sơ (SQL xóa theo dây chuyền toàn bộ dữ liệu học của bé), hoặc từ chối kèm lý do. Phụ huynh nhận thông báo; thao tác được ghi nhật ký.

Kiểm thử: `py scripts\smoke-reports.py` sau khi build. Test này bật job chạy mỗi phút và chạy khoảng 3–4 phút.

Các test khác tắt job (`Jobs__Enabled=false` trong `smoke-sql-auth.py`). Vì server khóa trang phụ huynh khi đang ở chế độ trẻ, các test dùng `smoke.Devices`: đường dẫn `/kids-…` gửi từ "máy của bé", các trang khác gửi từ "máy của ba mẹ".

## Bài của gia đình, đề xuất bài học và kho cộng đồng (mở rộng)

Nội dung có ba lớp, cùng một quy tắc hiển thị (`Services/LessonAccess.cs`):

| Lớp | Ai tạo | Ai thấy |
|---|---|---|
| Thư viện chuẩn | Admin | Mọi gia đình |
| Bài của gia đình | Phụ huynh | Chỉ gia đình đó |
| Kho cộng đồng | Bài gia đình được chia sẻ, Admin đã duyệt | Mọi gia đình, ghi tên người đóng góp |

- **A. Bài của gia đình** (`/family-lessons`):
  - Phụ huynh soạn bài cho con: bài đọc, video (chỉ chọn trong danh sách video Admin đã duyệt, trừ nguồn gia đình đã chặn), ảnh, PDF tối đa 5 MB, câu hỏi trắc nghiệm hoặc Đúng/Sai.
  - Câu hỏi tự thành bài tập của bài: bé làm, hệ thống chấm điểm như bài tập thường.
  - Trạng thái: Nháp hoặc "Bé học được". Bài chỉ hiện cho bé khi đúng môn và đúng lớp, hoặc khi được giao.
  - Từ khóa bị chặn (của hệ thống và của gia đình) được kiểm tra trong tiêu đề, bài đọc và câu hỏi.
  - Xóa: bài bé đã dùng thì chỉ ẩn, để giữ tiến độ và điểm. Tối đa 100 bài mỗi gia đình, 30 câu hỏi mỗi bài.
  - Bài riêng không hiện cho gia đình khác, cũng không hiện trong danh mục bài, thống kê hay hàng báo cáo của Admin. Báo cáo của bé về bài riêng chỉ đến phụ huynh.
- **B. Đề xuất bài học** (`/lesson-requests`):
  - Phụ huynh gửi chủ đề con cần học (tối đa 10 đề xuất đang chờ); gia đình khác bấm "Tôi cũng cần". Không hiện tên người gửi.
  - Admin xem ở `/admin-lesson-requests`, xếp theo số gia đình cùng cần. Admin chuyển trạng thái "Đang soạn", "Đã có bài" (gắn mã một bài công khai) hoặc "Chưa thực hiện" (bắt buộc ghi lý do).
  - Người gửi và mọi gia đình đã bấm "Tôi cũng cần" đều nhận thông báo.
- **C. Kho cộng đồng:**
  - Phụ huynh bấm "Gửi chia sẻ" (phải đồng ý hiện tên mình) cho bài đang ở trạng thái "Bé học được".
  - Admin xem trước và duyệt hoặc từ chối (kèm lý do) ở `/admin-community`; bài đã duyệt có thể bị gỡ (kèm lý do).
  - Sửa bài đã duyệt thì bài tự quay về chờ duyệt, và gia đình khác không thấy cho tới khi duyệt lại.
  - Khi Admin gỡ một bài cộng đồng bị báo cáo, bài chỉ rời kho cộng đồng; gia đình tác giả vẫn giữ bài.

**Database:** khi khởi động ở chế độ SQL, `Services/SchemaUpdater.cs` tự thêm những gì còn thiếu, không xóa hay sửa dữ liệu cũ, chạy lại nhiều lần vẫn an toàn:
- 5 cột vào `dbo.Lessons`: `OwnerParentId`, `ShareStatus`, `ShareNote`, `SharedAt`, `ShareReviewedAt`;
- 2 bảng: `dbo.LessonRequests`, `dbo.LessonRequestVotes`.

Các thành viên chỉ cần pull code rồi chạy app.

Kiểm thử: `py scripts\smoke-family.py` sau khi build.

**Xem PDF ngay trong trang:**
- PDF của bài học hiển thị ngay trong trang (PDF.js 4.10.38, để sẵn ở `wwwroot/vendor/pdfjs-4.10.38`, giấy phép Apache-2.0). Bé không phải mở tab mới nên thời gian đọc vẫn được tính vào giờ học.
- Có nút phóng to, thu nhỏ và toàn màn hình. Nếu trình duyệt không chạy được PDF.js, trang hiện PDF bằng khung nhúng.
- File trong `/uploads/lessons` chỉ tải được khi đã đăng nhập.

## PIN riêng của bé và đồng ý xử lý dữ liệu (US-08, US-10)

- **US-08, PIN của từng bé** (không bắt buộc):
  - Ba mẹ đặt, đổi hoặc bỏ PIN 4 số cho từng bé ở `/pin`, mục "PIN riêng của từng bé".
  - Ở màn hình "Hôm nay ai học nào?", bé có PIN phải nhập đúng PIN mới vào được, nên anh chị em không vào nhầm hồ sơ của nhau.
  - Nhập sai 5 lần thì hồ sơ của bé đó khóa 5 phút; các bé khác không bị ảnh hưởng.
  - PIN được băm, lưu ở `dbo.ChildProfiles` (`PinHash`, `PinFailedAttempts`, `PinLockedUntil`).
  - Bé không có email hay mật khẩu riêng: "tài khoản của trẻ" là hồ sơ bé có PIN riêng, học trên phiên đăng nhập của ba mẹ.
- **US-10, đồng ý xử lý dữ liệu theo từng bé:**
  - Khi tạo hồ sơ, ba mẹ phải tự tick ô đồng ý Chính sách dữ liệu trẻ em (ô không tick sẵn). Sự đồng ý lưu ở `dbo.UserConsents`, loại `ChildDataPolicy`, bản `v1.0`, kèm thời điểm và IP.
  - Ở `/privacy`, ba mẹ xem trạng thái của từng bé, **rút đồng ý** hoặc đồng ý lại. Mỗi lần thay đổi được ghi vào nhật ký thao tác.
  - Bé chưa có hoặc đã bị rút đồng ý thì không vào học được: màn hình chọn bé khóa hồ sơ đó, và nếu bé đang học thì bị đưa về màn hình chọn bé. Trang của ba mẹ vẫn khóa cho tới khi nhập PIN hoặc mật khẩu.
  - Xóa hồ sơ bé thì xóa luôn bản ghi đồng ý của bé.
- **Lưu ý khi cập nhật:** các hồ sơ bé tạo trước bản này chưa có bản ghi đồng ý. Ba mẹ vào `/privacy` bấm "Đồng ý" một lần cho từng bé; trang Tổng quan có nhắc việc này.
- Form có `data-confirm` giờ hỏi xác nhận trên mọi trang. Trước đây chỉ trang admin có hộp xác nhận.

Kiểm thử: `py scripts\smoke-child-access.py`.

## Trang tổng quan phụ huynh

`/parent-home` lấy toàn bộ số liệu thật từ SQL cho bé đang chọn (`?child=`):
- phút học hôm nay so với giới hạn;
- số bài xong trong tuần so với mục tiêu;
- điểm bài tập trung bình và chuỗi ngày học;
- biểu đồ 7 ngày;
- mục "Cần ba mẹ xem": xin thêm giờ, bài tập chưa đạt, thông báo chưa đọc;
- bài đang học dở và tiến độ theo môn.

Bộ chọn bé ở thanh bên được tạo trên server. Ô tìm kiếm trên cùng mở `/library?q=` (phụ huynh) hoặc `/admin-catalog?q=` (admin).

Kiểm thử: `py scripts\smoke-home.py`.

**Quét toàn bộ giao diện:** `py scripts\crawl-ui.py . crawl-report.json` mở trình duyệt thật với 4 vai (khách, phụ huynh, bé, admin), đi qua mọi trang tìm được và báo:
- lỗi 500, link hỏng, lỗi script;
- trang tràn ngang trên điện thoại;
- nút bấm không phản hồi.

Khi bấm thử nút, kịch bản chặn mọi request ghi dữ liệu nên không sửa gì trong DB.

## Triển khai (deploy)

Cần làm trước khi public:
1. **HTTPS và domain.** Đặt `Email:PublicBaseUrl` là địa chỉ `https://` thật. Môi trường Production từ chối chạy nếu thiếu.
2. **SMTP thật.** Cấu hình `Email:Host`, `Port`, `UserName`, `Password` và `FromAddress` thuộc domain của mình (có SPF/DKIM), nếu không mail xác minh dễ vào spam.
3. **Bí mật.** Đặt chuỗi kết nối, mật khẩu SMTP, `YouTube:ApiKey` và `Ai:ApiKey` qua biến môi trường của server, ví dụ `ConnectionStrings__DefaultConnection`. Không commit vào appsettings.
4. **Tài khoản SQL.** Tài khoản app cần quyền đọc/ghi và quyền `ALTER`/`CREATE TABLE` để `SchemaUpdater` chạy lúc khởi động. Có thể bỏ quyền này sau lần chạy đầu.
5. **Khóa DataProtection.** Mặc định lưu ở `App_Data/keys`, đổi bằng `DataProtection:KeysPath`. Thư mục này phải được giữ lại khi deploy lại, nếu không mọi người bị đăng xuất và link trong email cũ hỏng.
6. **File tải lên.** `wwwroot/uploads` chứa ảnh đại diện, ảnh và PDF bài học, nên phải nằm trên ổ được giữ lại và sao lưu.
7. **Reverse proxy (Nginx/IIS/cloud).** Đặt `Proxy:Enabled=true` và `Proxy:KnownProxies` (IP của proxy) để lấy đúng IP người dùng cho giới hạn số lần thử và đúng HTTPS.
8. **Giám sát và sao lưu.** `GET /healthz` trả `ok` khi kết nối được database. Sao lưu database `hoctapchotre` hằng ngày.

Ở môi trường Production:
- `/design-system` và `/states` trả 404;
- tài khoản demo và InMemory bị chặn;
- cookie chỉ gửi qua HTTPS và bật HSTS.

## Email xác minh và khôi phục

Profile Development dùng **Pickup**: email `.eml` được lưu riêng tại `%LOCALAPPDATA%\SafeLearnKids\Mail`. Mở thư mới nhất trên máy để lấy liên kết; đây là thư thử nghiệm, chưa gửi tới Gmail. Không có endpoint xem thư/token trong ứng dụng. Không đặt folder này trong repo hoặc wwwroot; không đưa thư/token lên Git/Trello/chat.

SMTP thật dùng cấu hình riêng qua environment: `Email__DeliveryMethod=Smtp`, `Email__Host`, `Email__Port` (587), `Email__FromAddress`, `Email__UserName`, `Email__Password`, `Email__PublicBaseUrl`. Không lưu password trong JSON/Git. SMTP luôn yêu cầu STARTTLS; URL thư lấy từ `PublicBaseUrl`, không lấy Host header. Non-Development yêu cầu URL HTTPS, SMTP được cấu hình và tắt toàn bộ demo.

Script smoke tự ép `Email:DeliveryMethod=Pickup` và địa chỉ giả trong process test, nên không dùng SMTP thật khi chạy bộ test. Cấu hình SMTP riêng không đi theo Git/clone; cần thiết lập trên từng môi trường.

Token đặt lại mặc định 30 phút, xác minh email 24 giờ, cấu hình riêng ở `Auth`. Link xác minh mở trang xác nhận và chỉ kích hoạt khi POST. Email đi qua hàng đợi riêng tối đa 100 thư để thời gian SMTP không làm lộ tài khoản tồn tại; hàng đợi trong bộ nhớ, chưa có outbox bền/retry tự động khi process dừng. Nếu chưa nhận được thư, dùng form gửi lại. Lỗi SMTP chỉ ghi thông báo chung, không ghi email/token/mật khẩu.

Muốn chạy riêng demo trong bộ nhớ, đặt riêng `Demo__ParentPassword` và `Demo__AdminPassword` trong environment (đạt chính sách mật khẩu), rồi bật hai tùy chọn:

```powershell
dotnet run --project SafeLearnKids.Web.csproj --launch-profile http -- --Demo:UseInMemoryDatabase=true --Demo:SeedDemoAccounts=true
```

Chỉ chế độ demo này seed các tài khoản sau:

| Vai trò | Email | Nguồn mật khẩu riêng |
|---|---|---|
| Phụ huynh | ha.nguyen@gmail.com | `Demo__ParentPassword` |
| Quản trị | admin@safelearnkids.vn | `Demo__AdminPassword` |

Dữ liệu nghiệp vụ demo nằm ở `wwwroot/js/store.js`, localStorage `kl.v1`. InMemory mất dữ liệu khi dừng process; dữ liệu demo không chứng minh đã lưu nghiệp vụ vào SQL.

## Chạy bằng Visual Studio

Mở project SafeLearnKids.Web.csproj, chọn cấu hình **Debug** và profile **http** cạnh nút Start; dừng phiên debug cũ, Rebuild rồi nhấn F5 hoặc Ctrl+F5. Mở http://localhost:5000. Profile **https** vẫn dùng cổng 7080/5080.

Hai profile đặt workingDirectory về $(ProjectDir) để Visual Studio chạy tại thư mục project. Khi kiểm tra CSS/JS, xem dòng Content root path trong console và status thực tế của /css/theme.css, /css/app.css, /css/auth.css và /lib/bootstrap/css/bootstrap.min.css. Sau khi khởi động lại app, tải lại tab trình duyệt để bỏ trạng thái trang cũ.

Bản Debug nạp SDK static web assets manifest để CSS/JS trong wwwroot vẫn được tìm thấy khi môi trường hoặc content root của lần chạy khác. Console có dòng Static assets với ThemeCss, AuthCss, UiJs; các cờ này phải là True. Đoạn nạp bổ sung chỉ nằm trong Debug, không ép nạp manifest ở Release. Thay đổi Program.cs cần dừng và khởi động lại app, không chỉ tải lại trang.

## Tích hợp SQL Server

`ApplicationDbContext` ánh xạ Identity vào `dbo.Users`, `dbo.Roles`, `dbo.UserRoles` hiện có, cùng `UserClaims`, `RoleClaims`, `UserLogins`, `UserTokens`. `scripts/ensure-identity-schema.sql` chỉ tạo bốn bảng phụ còn thiếu và cột `Users.AvatarUrl` trong `hoctapchotre`, không tạo lại database hoặc sửa dữ liệu cũ; có thể chạy lại. Không dùng `EnsureCreated` hoặc migration tạo mới toàn schema trên DB đã có dữ liệu.

`Program.cs` cũng đăng ký `SafeLearnDbContext` khi chạy SQL để các module nghiệp vụ dùng cùng kết nối. Nhiều trang vẫn dùng dữ liệu demo/localStorage; chưa phải mọi module đã có backend SQL. Riêng hồ sơ bé (tên, lớp, năm sinh, môn) lưu trong `dbo.ChildProfiles` và `dbo.ChildSubjects`: layout đưa danh sách của phụ huynh đang đăng nhập vào `store.js`, Admin xem tại `/admin-users/{id}`. Giới hạn giờ học, tiến độ và cài đặt tìm kiếm của bé vẫn nằm ở localStorage; chế độ InMemory giữ toàn bộ hồ sơ bé ở localStorage. `/settings` hiển thị tài khoản hiện tại và liên kết đổi mật khẩu thật; chỉnh sửa tên/email chưa triển khai nên tạm chỉ đọc. Không lưu mật khẩu trong localStorage; script xóa credential mock của bản trước khi tải trang.

Kiểm tra lại luồng thật trong cùng terminal đã cấu hình `ConnectionStrings__DefaultConnection` (cần Python 3 và quyền Windows vào SQL):

```powershell
dotnet build SafeLearnKids.Web.csproj
python scripts\smoke-sql-auth.py
python scripts\check-auth-regressions.py --race-rounds 24
```

Script dùng cổng HTTP và thư mục email tạm ngoài repo; kiểm tra SQL/role/hash, xác minh và token sai/hết hạn/dùng lại, CSRF, lockout, cookie replay, quyền Admin, reset/change password, rate limit và restart. Chỉ xóa các tài khoản UUID của chính lượt chạy, tự dừng các process test và không in token/password.

`check-auth-regressions.py` bổ sung các cặp đăng ký đồng thời (kể cả email khác chữ hoa/khoảng trắng) và các alias URL của 8 action xác thực. Đăng ký thử lại toàn bộ giao dịch User + Parent tối đa 3 lần khi SQL báo deadlock 1205; hết số lần thử thì trả lỗi 503 chung để người dùng thử lại. Rate limit dùng action đã được routing xác định để các URL khác chữ hoa, dấu `/` cuối hoặc mã hóa ký tự dùng chung ngân sách theo IP. Cả hai script ép Pickup riêng, không dùng SMTP thật.

## Cấu trúc project

- `Areas/Parent`, `Areas/Kids`, `Areas/Admin`: controller/view theo khu vực.
- `Views/Shared`: layout/sidebar/top bar.
- `Controllers/AccountController.cs`: backend Sprint1 cho đăng ký/xác minh/resend, login/logout, forgot/reset/change password.
- `Services/AccountEmailService.cs`, `AccountEmailQueue.cs`: tạo liên kết tin cậy và gửi thư riêng.
- `Data/`, `Models/`: Identity, entity và mapping.
- `wwwroot/css/`, `wwwroot/js/`: design system, UI và dữ liệu demo.
