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

`/admin-curate` quản lý nguồn: dán liên kết danh sách phát (`list=...`), kênh dạng `youtube.com/channel/UC...` hoặc website (chỉ ghi nhận, không tải). Liên kết dạng `@tên-kênh` chưa hỗ trợ. Nguồn mới ở trạng thái chờ duyệt; Admin duyệt, từ chối (cần lý do) hoặc tạm ngưng (cần lý do). Đồng bộ lấy danh sách video mới vào `dbo.SourceVideos` ở trạng thái chờ duyệt; tiêu đề chứa từ khóa trong `dbo.BlockedKeywords` (từ khóa chung) bị chặn tự động. `/admin-videos` là hàng chờ duyệt/chặn từng video. Một video chỉ phát cho bé khi video đã duyệt và ít nhất một nguồn chứa nó đang được duyệt; tạm ngưng nguồn sẽ gỡ video khỏi thư viện và chặn xuất bản bài dùng video đó.

"Đánh giá tự động" ghi vào `dbo.CurationSuggestions` với `ModelName = rule-v1`: bộ quy tắc minh bạch dựa trên loại nguồn, từ khóa bị chặn, từ ngữ không phù hợp và tiêu đề video, giải thích từng điểm cộng/trừ. Đây không phải mô hình AI; quản trị viên quyết định, và quyết định đó đóng gợi ý là đồng ý hoặc không.

Phụ huynh ở `/sources` chặn hoặc cho phép lại từng nguồn đã duyệt cho riêng gia đình mình (`dbo.ParentSources`) và đề xuất nguồn mới (tối đa 5 đề xuất chờ duyệt mỗi gia đình), xem lý do nếu bị từ chối.

**YouTube API key.** Để trống thì đồng bộ dùng RSS công khai của YouTube (15 video mới nhất, không có thời lượng). Có key thì dùng YouTube Data API v3: đọc toàn bộ danh sách tải lên (tối đa 200 video mỗi lần), lấy thời lượng và tự chặn video YouTube không cho nhúng. Bật **YouTube Data API v3** trong Google Cloud, tạo API key, rồi đặt riêng trên máy (không ghi vào `appsettings.json`/Git):

```powershell
dotnet user-secrets set "YouTube:ApiKey" "<api-key>"
```

Môi trường khác dùng biến `YouTube__ApiKey`. Key không được ghi vào log. Kiểm thử: `py scripts\smoke-sources.py` sau khi build (dùng YouTube giả cục bộ, không cần mạng hay key thật).

## Trang học của bé (Sprint 4)

Bé không có tài khoản riêng: phụ huynh đăng nhập, bấm **Chế độ trẻ**, chọn bé. Lựa chọn lưu trong cookie `.SafeLearnKids.Kid` và mỗi request đều kiểm tra bé thuộc phụ huynh đang đăng nhập; quay về màn hình chọn bé là xóa cookie. Bé chỉ mở được bài đã xuất bản thuộc môn đang dùng, và (a) đúng lớp và môn ba mẹ chọn cho bé, hoặc (b) được giao riêng. Video theo cùng quy tắc với thư viện (video và nguồn được duyệt, gia đình không chặn nguồn).

- Phụ huynh giao bài ở `/lesson-detail/{id}`: chọn bé, hạn (từ hôm nay đến 1 năm), lời nhắn; mỗi bé chỉ có một bài giao đang mở cho mỗi bài học; hủy được (`dbo.Assignments`).
- `/kids-home`: bài ba mẹ giao, "Học tiếp", các môn của bé kèm số bài đã xong, bài yêu thích.
- `/kids-lesson?id=` chia bài thành từng phần nội dung theo thứ tự; "Con xong phần này rồi" lưu tiến độ vào `dbo.LessonActivities`, lần sau học tiếp từ phần đang dở. Server tự đếm số phần, chỉ cho hoàn thành khi đã học hết; hoàn thành cũng đóng bài được giao. Thời gian học cộng mỗi 30 giây khi trang đang mở (mỗi lần tối đa 60 giây).
- Yêu thích lưu ở `dbo.Favorites`. `/kids-search` chỉ tìm trong bài bé được mở, ghi `dbo.SearchLogs`; từ khóa bị chặn (chung hoặc của gia đình) không trả kết quả.

Công tắc "Cho phép tìm kiếm" và giới hạn giờ học của bé vẫn lưu ở trình duyệt (chưa có cột trong SQL); quiz, huy hiệu, giờ học là phần của Sprint 5–7. Kiểm thử: `py scripts\smoke-learning.py` sau khi build.

## Thời gian học và giám sát (Sprint 5)

`/time` đặt cho từng bé: số phút mỗi ngày (mặc định 60 khi chưa đặt), số phút riêng từng thứ, khung giờ được học, nhắc nghỉ sau mỗi N phút (`dbo.TimeLimitPolicies`, `dbo.TimeLimitOverrides`). Trang bài học của bé báo thời gian mỗi 30 giây (mỗi lần tối đa 60 giây) vào `dbo.DailyUsages` và `dbo.LearningSessions`; khi hết phút hoặc ngoài khung giờ, server chặn mọi trang học của bé và chuyển tới `/kids-time-up`. Ngày và khung giờ tính theo giờ máy chủ. Chỉ thời gian trên trang bài học được tính.

Bé xin thêm 5/10/15/30 phút ở trang hết giờ (một yêu cầu chờ tại một thời điểm, tối đa 3 lần/ngày, không xin được khi đang ngoài khung giờ); phụ huynh cho thêm (không quá số bé xin) hoặc từ chối ở `/time`. Yêu cầu từ ngày trước tự hết hạn. Thông báo cho phụ huynh ghi vào `dbo.Notifications` (trang xem thông báo thuộc Sprint 8).

`/history` theo từng bé: bài đã học (tiến độ, thời gian xem), số phút 14 ngày, tìm kiếm (kể cả từ khóa bị chặn), báo cáo của bé; xuất CSV. Bé báo cáo bài ở trang học, phụ huynh báo cáo ở thư viện (`dbo.ContentReports`). `/admin-reports`: nhận xem, giữ nội dung hoặc gỡ (chặn video, hoặc lưu trữ bài nếu báo cáo về bài), bắt buộc ghi kết quả; gia đình nhận thông báo và thấy kết quả trong lịch sử. Kiểm thử: `py scripts\smoke-time.py` sau khi build.

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
