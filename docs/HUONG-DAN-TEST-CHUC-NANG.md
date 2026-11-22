# Hướng dẫn sử dụng và test chức năng theo sprint

File này dùng khi **chuẩn bị và trình bày báo cáo sprint**: mở trang nào, bấm nút gì, kết quả đúng phải thấy gì, ca lỗi nào nên demo, và gặp vướng thì gỡ thế nào.

Ba file học đi cùng nhau:

| File | Trả lời câu hỏi |
|---|---|
| `docs/HUONG-DAN-TEST-CHUC-NANG.md` (file này) | Dùng hệ thống thế nào, bấm gì, kết quả ra sao |
| `docs/BAO-CAO-SPRINT.md` | Code của chức năng nằm ở file nào |
| `docs/HOC-DATABASE.md` | Dữ liệu được lưu vào bảng nào, trường nào |

Cách đọc mỗi sprint:
- **Luồng chính**: các bước demo theo đúng thứ tự, tên nút ghi trong **ngoặc kép in đậm** giống hệt trên giao diện.
- **Ca lỗi nên demo**: thầy cô hay hỏi "nếu nhập sai thì sao?"; chuẩn bị sẵn để bấm cho xem.
- **Giải thích nghiệp vụ**: câu nói ngắn để trả lời khi được hỏi "tại sao lại làm vậy".

---

## 0. Chuẩn bị trước buổi báo cáo (làm trước ít nhất 1 ngày)

### 0.1 Chạy app

```powershell
libman restore                       # lần đầu, nếu trang không có CSS
dotnet run --project SafeLearnKids.Web.csproj --launch-profile http
```

Mở http://localhost:5000. Chi tiết cấu hình SQL xem `README.md` mục "Chạy với SQL Server".

### 0.2 Tài khoản cần có

| Vai trò | Cách tạo | Ghi chú |
|---|---|---|
| **Admin** | `dotnet user-secrets set "Bootstrap:AdminEmail" "..."` và `"Bootstrap:AdminPassword" "..."` rồi khởi động lại app | `/register` chỉ tạo được Phụ huynh |
| **Phụ huynh A** | `/register` → xác minh email | Dùng làm luồng chính |
| **Phụ huynh B** (nên có) | `/register` | Để demo "gia đình khác không thấy bài riêng", "Tôi cũng cần" |
| **Bé** | Phụ huynh tạo hồ sơ ở `/onboarding` | Bé **không có tài khoản riêng**, học trên phiên của ba mẹ |

Mật khẩu: ít nhất 8 ký tự, có chữ thường và chữ số.

**Lấy email xác minh / đặt lại mật khẩu:** ở môi trường Development, app không gửi Gmail thật mà lưu file `.eml` tại `%LOCALAPPDATA%\SafeLearnKids\Mail`. Gõ đường dẫn đó vào thanh địa chỉ của File Explorer, mở thư mới nhất, bấm link trong thư.

### 0.3 Mẹo mở nhiều vai cùng lúc

Chế độ trẻ được lưu theo trình duyệt. Khi một trình duyệt đang ở chế độ trẻ, mọi trang phụ huynh trên trình duyệt đó bị khóa. Vì vậy khi demo nên mở:

| Cửa sổ | Đăng nhập | Dùng để |
|---|---|---|
| Chrome thường | Phụ huynh A | Màn hình ba mẹ |
| Edge (hoặc Chrome profile khác) | Phụ huynh A, bấm **"Chế độ trẻ"** | Màn hình của bé |
| Cửa sổ ẩn danh | Admin | Màn hình quản trị |

Như vậy demo được luồng qua lại (bé xin thêm giờ → ba mẹ duyệt; bé báo cáo → admin xử lý) mà không phải đăng xuất.

### 0.4 Dựng dữ liệu mẫu theo đúng thứ tự phụ thuộc

Nhiều chức năng chỉ hiện khi dữ liệu phía trước đã có. Làm theo thứ tự này, nếu nhảy bước thì bé sẽ "không thấy bài nào":

1. **Admin** `/admin-catalog`: có ít nhất 1 môn **đang dùng**, áp dụng cho lớp của bé.
2. **Admin** `/admin-curate`: thêm 1 nguồn YouTube → **"Duyệt nguồn"** → **"Đồng bộ video"** → `/admin-videos` **"Duyệt"** vài video.
3. **Admin** `/admin-lesson/new`: tạo bài đúng môn và lớp, thêm nội dung (bài đọc + video đã duyệt) → **"Xuất bản"**.
4. **Admin** `/admin-questions`: tạo 3–5 câu hỏi cho bài → **"Duyệt"** → **"Tạo bài tập"** (trạng thái Đang mở).
5. **Admin** `/admin-paths`, `/admin-badges`: 1 lộ trình, vài huy hiệu (Sprint 7).
6. **Phụ huynh** `/onboarding`: tạo bé đúng lớp, **tick đồng ý** chính sách dữ liệu, chọn đúng môn ở bước "Chọn môn và giờ học".
7. **Phụ huynh** `/pin`: đặt PIN chế độ trẻ.

### 0.5 Chạy test tự động của sprint (tuỳ chọn, để có bằng chứng)

```powershell
dotnet build SafeLearnKids.Web.csproj
py scripts\smoke-<ten>.py
```

| Sprint | Script |
|---|---|
| 1 | `smoke-sql-auth.py`, `check-auth-regressions.py`, `smoke-admin-users.py`, `smoke-child-access.py` |
| 2 | `smoke-catalog.py`, `smoke-family.py` |
| 3 | `smoke-sources.py` |
| 4 | `smoke-learning.py` |
| 5 | `smoke-time.py` |
| 6 | `smoke-quiz.py` |
| 7 | `smoke-progress.py` |
| 8 | `smoke-reports.py` (chạy khoảng 3–4 phút), `smoke-home.py` |

Chụp màn hình kết quả để đưa vào báo cáo.

---

## 1. Bản đồ hệ thống: ai dùng trang nào

### Phụ huynh (menu bên trái)

| Nhóm | Mục menu | Địa chỉ |
|---|---|---|
| | Thêm hồ sơ bé | `/onboarding` |
| | Tổng quan | `/parent-home` |
| | Hồ sơ của bé | `/profiles` |
| | Mã PIN và chế độ trẻ | `/pin` |
| Nội dung | Thư viện bài học | `/library` → chi tiết `/lesson-detail/{id}` |
| | Bài của gia đình | `/family-lessons` |
| | Đề xuất bài học | `/lesson-requests` |
| | Nguồn YouTube | `/sources` |
| Giám sát | Thời gian học | `/time` |
| | Lịch sử và báo cáo | `/history` |
| | Tiến độ và lộ trình | `/progress` |
| | Báo cáo tuần | `/weekly` |
| Tài khoản | Thông báo | `/notifications` |
| | Quyền riêng tư | `/privacy` |
| | Cài đặt | `/settings` |

Nút **"Chế độ trẻ"** ở thanh trên cùng chuyển sang màn hình của bé.

### Bé (chế độ trẻ)

| Trang | Địa chỉ |
|---|---|
| Chọn bé "Hôm nay ai học nào?" | `/kids-picker` |
| Trang chủ của bé | `/kids-home` |
| Học bài | `/kids-lesson?id=` |
| Tìm kiếm | `/kids-search` |
| Làm bài tập / kết quả | `/kids-quiz?id=` / `/kids-result?id=` |
| Huy hiệu / ôn câu sai | `/kids-badges` / `/kids-review` |
| Hết giờ | `/kids-time-up` |
| Thoát chế độ trẻ | `/kids-exit` (nút **"Dành cho ba mẹ (cần PIN)"**) |

### Admin (menu bên trái)

| Nhóm | Mục menu | Địa chỉ |
|---|---|---|
| | Tổng quan / Thống kê nội dung | `/admin-home` / `/admin-stats` |
| Nội dung | Môn học và bài học | `/admin-catalog` |
| | Soạn bài học | `/admin-lesson` |
| | Câu hỏi và quiz | `/admin-questions` |
| | Bài cộng đồng | `/admin-community` |
| | Đề xuất bài học | `/admin-lesson-requests` |
| | Lộ trình và huy hiệu | `/admin-paths`, `/admin-badges` |
| | Nguồn nội dung | `/admin-curate` |
| | Duyệt video | `/admin-videos` |
| Vận hành | Báo cáo nội dung | `/admin-reports` |
| | Người dùng | `/admin-users` |
| | Yêu cầu dữ liệu | `/admin-data-requests` |
| | Nhật ký | `/admin-audit` |

---

## Sprint 1: Quản lý tài khoản và phân quyền (US-01 → US-10)

### Luồng chính

**A. Đăng ký → xác minh → đăng nhập (US-01, 02)**
1. `/register`: nhập họ tên, email, mật khẩu, tick đồng ý điều khoản → **"Tạo tài khoản"**.
2. Chuyển tới `/check-email`. App **chưa** tự đăng nhập.
3. Mở thư trong `%LOCALAPPDATA%\SafeLearnKids\Mail` → bấm link → trang `/confirm-email` → **"Xác minh tài khoản"**.
4. Thấy thông báo "Đã xác minh email. Bạn có thể đăng nhập." → `/login` → **"Đăng nhập"**.
5. Phụ huynh vào `/parent-home` (lần đầu chưa có bé sẽ được dẫn tới `/onboarding`); admin vào `/admin-home`.
6. Đăng xuất: nút **"Đăng xuất"** cuối menu trái.

**B. Đổi và khôi phục mật khẩu (US-03)**
- Đổi: `/settings` → **"Đổi mật khẩu"** (hoặc `/change-password`) → nhập mật khẩu cũ và mới → **"Đổi mật khẩu"** → bị đăng xuất, đăng nhập lại bằng mật khẩu mới.
- Quên: `/login` → **"Quên mật khẩu?"** (`/recover`) → nhập email → **"Gửi yêu cầu khôi phục"** → mở thư → `/reset-password` → **"Lưu mật khẩu mới"**.

**C. Admin quản lý tài khoản (US-04, 05)**
1. Admin `/admin-users`: ô tìm kiếm → **"Tìm"**, lọc vai trò/trạng thái, **"Xuất danh sách"** (CSV).
2. **"Thêm người dùng"** → **"Tạo và gửi lời mời"**: người được mời nhận email, tự đặt mật khẩu.
3. **"Chi tiết"** một phụ huynh: **"Khóa tài khoản"** / **"Mở khóa"**, đổi vai trò → **"Lưu vai trò"**, **"Gửi email đặt lại mật khẩu"**, **"Gửi lại email xác minh"**, **"Xóa tài khoản"**. Bên dưới có lịch sử đăng nhập.

**D. Hồ sơ bé (US-06, 07)**
1. `/onboarding` (menu **Thêm hồ sơ bé**; lần đầu đăng nhập chưa có bé cũng tự chuyển tới đây), gồm 3 bước:
   - **Bước 2 / 4, hồ sơ của bé:** chọn nhân vật, nhập **"Tên hoặc biệt danh của bé"**, chọn **Lớp** 1–5, **"Năm sinh (không bắt buộc)"**, tick ô đồng ý **Chính sách dữ liệu trẻ em** → **"Tiếp tục"**.
   - **Bước 3 / 4, chọn môn và giờ học** → **"Tiếp tục"**.
   - **Bước 4 / 4, đặt mã PIN** → **"Tiếp tục"**. Nút **"Quay lại"** để về bước trước.
2. `/profiles`: danh sách bé, các nút **"Thêm bé"**, **"Giao bài"**, **"Giờ học"**, **"Vào học"**; sửa hoặc xóa hồ sơ.

**E. PIN và chế độ trẻ (US-08, 09)**
1. `/pin`: **"Đặt PIN"** cho chế độ trẻ (4 số, phải nhập mật khẩu tài khoản).
2. Mục "PIN riêng của từng bé": **"Đặt PIN"** cho bé (không bắt buộc), **"Bỏ PIN"** để gỡ.
3. Bấm **"Chế độ trẻ"** ở thanh trên → `/kids-picker` → chọn bé → (bé có PIN thì nhập PIN) → **"Vào học"** → `/kids-home`.
4. Thoát: **"Dành cho ba mẹ (cần PIN)"** → `/kids-exit` → bấm số PIN → **"Mở khóa"**. Quên PIN thì **"Mở khóa bằng mật khẩu"**.

**F. Đồng ý xử lý dữ liệu của bé (US-10)**
- `/privacy`: mỗi bé có trạng thái đồng ý, nút **"Rút đồng ý"** / **"Đồng ý"**.

### Ca lỗi nên demo

| Thao tác | Kết quả đúng |
|---|---|
| Đăng nhập khi chưa xác minh email | Không vào được, báo lỗi chung |
| Đăng nhập sai mật khẩu | "Không thể đăng nhập. Hãy kiểm tra email, mật khẩu hoặc thử lại sau." Không nói email có tồn tại hay không |
| Sai mật khẩu 5 lần | Tài khoản khóa 5 phút, nhập đúng cũng không vào |
| Bấm lại link xác minh hoặc đặt lại mật khẩu đã dùng | "Liên kết không hợp lệ, đã được dùng hoặc đã hết hạn..." |
| Đăng ký email đã có | "Email này đã được đăng ký..." |
| Phụ huynh gõ `/admin-users` | Bị chặn, không vào được trang admin |
| Admin tự khóa hoặc tự đổi vai trò của mình | Bị từ chối, hệ thống luôn còn ít nhất 1 admin |
| Tạo bé thứ 5 | Bị từ chối, tối đa 4 bé |
| Tạo 2 bé trùng tên | Bị từ chối |
| Xóa bé cuối cùng | Bị từ chối, phải giữ ít nhất 1 hồ sơ |
| Không tick ô đồng ý khi tạo bé | Không tạo được |
| Đang ở chế độ trẻ, gõ `/parent-home` | Bị đưa về `/kids-home` |
| Nhập sai PIN của bé 5 lần | Riêng bé đó khóa 5 phút, các bé khác vẫn vào được |
| `/privacy` **"Rút đồng ý"** một bé | Ở `/kids-picker` hồ sơ bé đó bị khóa |
| Admin khóa tài khoản đang đăng nhập ở máy khác | Máy kia bị đăng xuất ngay ở lần bấm tiếp theo |

### Giải thích nghiệp vụ
- **Vì sao bé không có tài khoản?** Trẻ tiểu học không tự quản lý mật khẩu. Bé dùng hồ sơ (có thể có PIN), học trên phiên của ba mẹ; server khóa khu phụ huynh khi đang ở chế độ trẻ.
- **Vì sao báo lỗi đăng nhập chung chung?** Để người lạ không dò được email nào đã đăng ký.
- **Đổi mật khẩu thì sao các máy khác bị đăng xuất?** Security stamp đổi, cookie cũ hết hiệu lực.

---

## Sprint 2: Quản lý môn học và bài học (US-11 → US-16)

### Luồng chính

**A. Môn học (US-11)**
1. Admin `/admin-catalog` → **"Thêm môn"** → nhập tên, mã môn, mô tả, màu, biểu tượng, thứ tự, các lớp áp dụng → **"Thêm môn"**.
2. **"Sửa"** một môn → **"Lưu thay đổi"**; bỏ tick "đang dùng" để ẩn; **"Xóa môn"** (chỉ khi môn chưa có bài và chưa có bé học).

**B. Bài học (US-12, 13, 14)**
1. `/admin-catalog` → **"Bài học mới"** (hoặc menu **Soạn bài học**) → chọn môn, lớp, loại bài, tiêu đề, nhãn → **"Tạo bài nháp"**.
2. Trang bài vừa tạo: khung **"Thêm nội dung"**, chọn loại:
   - Bài đọc: gõ đoạn văn.
   - Video: chọn trong danh sách video đã duyệt (cần làm Sprint 3 trước).
   - Ảnh JPG/PNG/WebP hoặc PDF, tối đa 5 MB.
3. Dùng ↑ ↓ để đổi thứ tự nội dung.
4. Cột trạng thái bên phải: **"Gửi duyệt"** → **"Xuất bản"**. Đã xuất bản thì có **"Gỡ xuất bản"** và **"Lưu trữ"**.
5. Sửa tiêu đề hoặc nhãn → **"Lưu thông tin bài"**.

**C. Phụ huynh xem thư viện (US-15, 16)**
1. Phụ huynh `/library`: lọc môn, lớp, loại, nhãn, gõ từ khóa → **"Lọc"**.
2. Bấm vào bài → `/lesson-detail/{id}`: xem bài đọc, video (trình phát youtube-nocookie), PDF xem ngay trong trang (phóng to, toàn màn hình).
3. Môn của bé (US-15) được chọn khi tạo hoặc sửa hồ sơ bé.

Trạng thái bài: **Bản nháp → Chờ duyệt → Đã xuất bản → Lưu trữ**.

### Ca lỗi nên demo

| Thao tác | Kết quả đúng |
|---|---|
| **"Xuất bản"** bài chưa có nội dung nào | Bị từ chối, cần ít nhất 1 nội dung |
| Tạo bài ở lớp mà môn không áp dụng | Không có lớp đó để chọn / bị từ chối |
| Tải file `.exe` đổi đuôi thành `.pdf`, hoặc file > 5 MB | Bị từ chối (kiểm tra theo nội dung file, không chỉ đuôi) |
| Hai admin mở cùng một bài, A lưu trước, B lưu sau | B bị báo lỗi, không ghi đè thay đổi của A (RowVersion) |
| Xóa môn đã có bài | Không xóa được, chỉ ẩn được |
| Bài ở trạng thái Nháp | Phụ huynh không thấy trong `/library` |
| Đăng xuất rồi mở trực tiếp link file trong `/uploads/lessons/...` | Không tải được |
| Đổi mã môn sau khi tạo | Không cho đổi (hồ sơ bé dùng mã làm khóa) |

### Giải thích nghiệp vụ
- Bài phải qua trạng thái để không đưa bài chưa kiểm tra tới trẻ.
- Ẩn thay vì xóa để không mất lịch sử học của bé.

---

## Sprint 3: Quản lý nguồn nội dung an toàn (US-17 → US-22)

> Cần **có mạng**. Không có YouTube API key thì đồng bộ dùng RSS công khai, lấy tối đa 15 video mới nhất, không có thời lượng.

### Luồng chính

**A. Thêm và duyệt nguồn (US-17, 18, 22)**
1. Admin `/admin-curate` → **"Thêm nguồn"** → dán link dạng `https://www.youtube.com/playlist?list=...` hoặc `https://www.youtube.com/channel/UC...`, mô tả → **"Thêm nguồn"**. Nguồn mới ở trạng thái chờ duyệt.
2. Trang chi tiết nguồn: **"Đánh giá"** → hiện điểm (bắt đầu 60, cộng/trừ theo quy tắc) và lý do từng điểm. Từ 75 điểm trở lên gợi ý duyệt, dưới 50 gợi ý từ chối.
3. **"Duyệt nguồn"**; hoặc **"Từ chối nguồn"** / **"Tạm ngưng nguồn"** (bắt buộc ghi lý do).
4. **"Đồng bộ video"** → video mới vào hàng chờ. Video có tiêu đề chứa từ khóa bị chặn tự động bị chặn.

**B. Duyệt từng video (US-19, 20)**
1. **"Video của nguồn"** hoặc menu **Duyệt video** (`/admin-videos`) → lọc trạng thái → **"Lọc"**.
2. Mỗi video: **"Duyệt"** hoặc **"Chặn"**.
3. Thêm 1 video lẻ không cần cả kênh: ô **"Thêm video"** nhận link `watch?v=`, `youtu.be/`, `shorts/`, `embed/`, `live/` hoặc mã video; có thể chọn "Duyệt luôn".
4. Nút **"Đồng bộ nguồn đã duyệt"** ở `/admin-curate` đồng bộ tất cả nguồn đã duyệt một lần.

**C. Phụ huynh kiểm soát nguồn (US-21)**
1. Phụ huynh `/sources`: mỗi nguồn đã duyệt có **"Chặn cho gia đình"** / **"Cho phép lại"**.
2. Đề xuất nguồn mới → **"Gửi đề xuất"** (tối đa 5 đề xuất đang chờ). Admin thấy đề xuất ở `/admin-curate`. Bị từ chối thì phụ huynh xem được lý do.

### Ca lỗi nên demo

| Thao tác | Kết quả đúng |
|---|---|
| Dán link `youtube.com/@ten-kenh` | Chưa hỗ trợ, báo lỗi; dùng link `channel/UC...` |
| **"Từ chối nguồn"** không ghi lý do | Không cho lưu |
| Video đã duyệt nhưng nguồn bị **"Tạm ngưng"** | Video biến khỏi thư viện; bài dùng video đó không xuất bản được |
| Phụ huynh **"Chặn cho gia đình"** một nguồn | Video của nguồn đó không hiện với gia đình này, gia đình khác vẫn thấy |
| Thêm video riêng tư, đã xóa hoặc không cho nhúng | Bị từ chối |
| Gửi đề xuất nguồn thứ 6 khi 5 cái đang chờ | Bị từ chối |

### Giải thích nghiệp vụ
- **Hai lớp duyệt:** video phát được khi **video đã duyệt** và **ít nhất một nguồn chứa nó đang được duyệt** (video lẻ chỉ cần chính nó được duyệt), và gia đình không chặn nguồn đó.
- **Vì sao dùng `youtube-nocookie` trong iframe có sandbox?** Không đặt cookie theo dõi, không cho video điều hướng trang hay mở popup.
- **"Đánh giá tự động" có phải AI không?** Không. Đây là bộ quy tắc minh bạch (`rule-v1`), có hiển thị lý do cộng/trừ; admin là người quyết định.

---

## Sprint 4: Quản lý hoạt động học tập của trẻ (US-23 → US-28)

### Luồng chính

**A. Ba mẹ giao bài (US-24)**
1. Phụ huynh `/library` → mở bài → khung giao bài: chọn bé, **"Hạn hoàn thành (không bắt buộc)"**, **"Lời nhắn cho bé (không bắt buộc)"** → **"Giao bài"**.
2. Bài giao xuất hiện ngay trên trang; **"Hủy"** để hủy giao.

**B. Bé học (US-23, 25, 26)**
1. Trình duyệt của bé: **"Chế độ trẻ"** → `/kids-picker` → chọn bé → **"Vào học"**.
2. `/kids-home` hiện: bài ba mẹ giao kèm lời nhắn, "Học tiếp", các môn của bé, bài yêu thích.
3. Bấm **"Bắt đầu"** (bài mới) hoặc **"Học tiếp"** (bài đang dở) → `/kids-lesson?id=`.
4. Bài chia thành từng phần. Học xong một phần → **"Con xong phần này rồi"** → mở phần tiếp theo.
5. Phần cuối → **"Con học xong rồi"** → bài hoàn thành, bài được giao tự đóng.
6. Thoát giữa chừng rồi vào lại: tiếp tục đúng phần đang dở.

**C. Tìm kiếm và yêu thích (US-27, 28)**
- Trang bài học: **"Yêu thích"** → đổi thành **"Đã thích"**; bài hiện ở mục yêu thích trên `/kids-home`.
- `/kids-search`: gõ từ khóa → **"Tìm"**.

### Ca lỗi nên demo

| Thao tác | Kết quả đúng |
|---|---|
| Bé lớp 2 tìm bài của lớp 4 chưa được giao | Không tìm thấy |
| Ba mẹ giao riêng bài lớp 4 cho bé lớp 2 | Bé thấy và học được bài đó |
| Gõ `/kids-lesson?id=` của bài không được mở | Không vào được |
| Tìm từ khóa bị chặn | Không trả kết quả; phụ huynh thấy từ khóa đó trong `/history` |
| Giao cùng một bài cho cùng một bé 2 lần khi bài cũ còn mở | Bị từ chối |
| Hạn giao bài ở quá khứ hoặc quá 1 năm | Bị từ chối |

### Giải thích nghiệp vụ
- **Bé thấy bài nào?** Bài đã xuất bản, môn đang dùng, và (a) đúng lớp và đúng môn ba mẹ chọn, hoặc (b) được giao riêng.
- **Vì sao phải xong phần trước mới mở phần sau?** Học theo trình tự; server tự đếm số phần, chỉ cho hoàn thành khi học hết, không tin trình duyệt.
- Tiến độ lưu trong `LessonActivities`, nên đổi máy vẫn học tiếp được.

---

## Sprint 5: Quản lý thời gian học và giám sát (US-29 → US-34)

### Luồng chính

**A. Đặt giới hạn thời gian (US-30, 31)**
1. Phụ huynh `/time` → chọn bé (thanh tên bé ở đầu trang).
2. **"Số phút mỗi ngày"**, số phút riêng từng thứ, khung giờ được học, **"Nhắc bé nghỉ mắt sau mỗi..."** → **"Lưu cài đặt"**.

**B. Hết giờ và xin thêm giờ (US-32)** — mẹo demo: đặt **1 phút**
1. Bé mở một bài học và **để nguyên trang** khoảng 1–1,5 phút (app báo thời gian mỗi 30 giây).
2. Trang tự chuyển sang `/kids-time-up`. Gõ địa chỉ bài khác cũng bị chuyển lại đây.
3. Bé chọn 5/10/15/30 phút → **"Xin ba mẹ thêm giờ"**.
4. Trình duyệt ba mẹ: `/time` → yêu cầu đang chờ → **"Cho thêm"** hoặc **"Từ chối"**. Chuông thông báo có số mới.
5. Bé bấm **"Xem ba mẹ trả lời chưa"** → được cho thêm thì hiện **"Học tiếp"**.

**C. Lịch sử học (US-29)**
- `/history` → chọn bé: bài đã học, tiến độ, số phút 14 ngày, từ khóa đã tìm (kể cả bị chặn), báo cáo của bé, bài tập đã làm → **"Xuất CSV"**.

**D. Báo cáo nội dung không phù hợp (US-33, 34)**
1. Bé: cuối trang bài học, mục **"Bài này có gì lạ?"** → chọn lý do → **"Gửi cho ba mẹ"**.
2. Phụ huynh: trang `/lesson-detail/{id}` → chọn **"Lý do"**, **"Ghi chú"** → **"Gửi báo cáo"**.
3. Admin `/admin-reports` → tab **"Cần xử lý"** → **"Nhận xem"** → **"Mở bài"** / **"Xem video"** để kiểm tra → **"Giữ nội dung"** hoặc **"Chặn video"** / **"Gỡ bài học"** (bắt buộc ghi kết quả).
4. Gia đình nhận thông báo; kết quả hiện trong `/history`.

### Ca lỗi nên demo

| Thao tác | Kết quả đúng |
|---|---|
| Xin thêm giờ lần 2 khi lần 1 chưa được trả lời | Không cho, chỉ 1 yêu cầu chờ một lúc |
| Xin thêm giờ lần thứ 4 trong ngày | Không cho, tối đa 3 lần/ngày |
| Xin thêm giờ khi đang ngoài khung giờ | Không cho |
| Ba mẹ cho thêm nhiều hơn số bé xin | Không cho |
| Sửa đồng hồ máy tính hoặc chặn JavaScript để "gian lận" | Không tác dụng, server tự đếm giây |
| Admin xử lý báo cáo mà không ghi kết quả | Không cho lưu |

### Giải thích nghiệp vụ
- Thời gian **đếm trên server** qua nhịp 30 giây (mỗi nhịp tối đa 60 giây), không tin số giây trình duyệt gửi lên.
- Chỉ thời gian trên trang bài học và trang làm bài được tính; ngày và khung giờ tính theo giờ máy chủ.

---

## Sprint 6: Câu hỏi và bài tập (US-35 → US-40)

### Luồng chính

**A. Ngân hàng câu hỏi (US-35, 40)**
1. Admin `/admin-questions` → chọn bài học → **"Câu hỏi mới"** → loại Trắc nghiệm (2–4 đáp án, đúng 1 đáp án đúng) hoặc Đúng/Sai, độ khó 1–3, giải thích đáp án → **"Thêm câu hỏi"**.
2. Ở danh sách: **"Duyệt"**, **"Từ chối"**, **"Lưu trữ"**, **"Khôi phục"**, **"Sửa"**.
3. **"Gợi ý câu hỏi"** (1–5 câu): câu gợi ý vào **"Chờ duyệt"**. Không có `Ai:ApiKey` thì dùng bộ sinh offline; có key thì gọi Claude.

**B. Tạo bài tập (US-36)**
- **"Tạo bài tập"** → chọn các câu **đã duyệt**, điểm đạt (%), thời gian làm bài, xáo trộn câu, trạng thái Đang mở → **"Tạo bài tập"**.

**C. Giao bài tập có hạn nộp (US-37)**
- Phụ huynh `/lesson-detail/{id}` → thẻ "Bài tập kiểm tra" → chọn **"Bài tập"**, bé, **"Hạn nộp"** (bắt buộc), lời nhắn → **"Giao bài tập"**.

**D. Bé làm bài và xem kết quả (US-38, 39)**
1. Bé mở bài học → bấm tên bài tập → `/kids-quiz`.
2. Làm từng câu: **"Câu trước"** / **"Câu tiếp theo"**, có đồng hồ đếm ngược → **"Nộp bài"**.
3. `/kids-result`: điểm, số sao, đáp án đã chọn, đáp án đúng, giải thích. Nút **"Làm lại"**, **"Xem lại bài học"**, **"Về trang học"**.
4. Phụ huynh: `/history` mục "Bài tập đã làm" → `/quiz-result/{id}` xem từng câu.

### Ca lỗi nên demo

| Thao tác | Kết quả đúng |
|---|---|
| Câu trắc nghiệm có 2 đáp án đúng hoặc chỉ 1 đáp án | Không cho lưu |
| Đưa câu "Chờ duyệt" (AI gợi ý) vào bài tập | Không chọn được, phải duyệt trước |
| Bài tập không có câu đã duyệt | Bé không thấy |
| **"Nộp bài"** khi còn câu bỏ trống | Hỏi xác nhận trước |
| Hết thời gian làm bài | Tự nộp |
| Tải lại trang giữa lúc làm | Tiếp tục lượt đang làm, đồng hồ không đặt lại |
| Nộp sau hạn | Kết quả có nhãn "nộp muộn" |
| Xóa câu hỏi đã có bé trả lời | Không xóa được, chỉ lưu trữ |

### Giải thích nghiệp vụ
- Chấm điểm **trên server** theo điểm từng câu; trình duyệt không biết đáp án đúng trước khi nộp.
- Câu AI tạo **luôn chờ admin duyệt**, AI không tự đưa nội dung tới trẻ.
- Lần nộp đầu của bài được giao mới tính hoàn thành; các lần sau là luyện tập.

---

## Sprint 7: Tiến độ, lộ trình và thành tích (US-41 → US-46)

### Luồng chính

**A. Admin tạo lộ trình và huy hiệu (US-42, 43)**
1. `/admin-paths` → chọn môn, lớp → **"Tiếp tục"** → **"Tải danh sách bài"** → sắp thứ tự, đánh dấu bài bắt buộc → **"Tạo lộ trình"**.
2. `/admin-badges` → **"Huy hiệu mới"** → tiêu chí (số bài hoàn thành, số ngày học liên tiếp, số bài tập đạt 100%, tổng số phút) và mốc → **"Thêm huy hiệu"**.

**B. Phụ huynh theo dõi (US-41, 42, 45)**
1. `/progress` → chọn bé: tiến độ theo môn, điểm bài tập trung bình.
2. Lộ trình phù hợp lớp và môn → **"Chọn cho bé"** / **"Bỏ"**.
3. Mục tiêu tuần: số bài và/hoặc số phút → **"Đặt mục tiêu"** (đã có thì **"Cập nhật mục tiêu"**).
4. Mục "Cần chú ý": bài tập chưa đạt, số câu cần ôn → **"Xem bài làm"**.

**C. Bé (US-43, 44, 46)**
- Học xong bài hoặc làm bài tập đạt mốc → thông báo nhận huy hiệu ngay.
- `/kids-badges`: huy hiệu đã có, tiến độ tới huy hiệu chưa có, số sao.
- `/kids-review`: các câu lần gần nhất làm sai hoặc bỏ trống → chọn lại → **"Kiểm tra"**; **"Làm lại bài tập"** để xóa câu khỏi danh sách.

**Mẹo demo:** tạo huy hiệu mốc thấp (ví dụ "hoàn thành 1 bài") rồi cho bé học xong 1 bài để thấy huy hiệu được trao ngay.

### Ca lỗi nên demo

| Thao tác | Kết quả đúng |
|---|---|
| Chọn lộ trình lớp 3 cho bé lớp 2 | Không có trong danh sách |
| Xóa lộ trình đã có bé theo / huy hiệu đã có bé nhận | Không xóa được, chỉ tắt |
| Bé trả lời đúng ở `/kids-review` | Thấy giải thích, nhưng câu chỉ rời danh sách khi làm lại bài tập đúng |
| Nghỉ một ngày rồi học lại | Chuỗi ngày bắt đầu lại từ 1, kỷ lục vẫn giữ |

---

## Sprint 8: Thống kê, thông báo và dữ liệu (US-47 → US-52)

### Luồng chính

**A. Báo cáo tuần (US-47)**
- `/weekly` → chọn bé, chọn tuần → **"Xem"**: phút mỗi ngày, bài xong, điểm bài tập, môn tốt nhất, môn cần ôn, huy hiệu mới. **"In / lưu PDF"**, **"Tải CSV các tuần"**.

**B. Admin tổng quan và thống kê (US-48, 49)**
- `/admin-home`: số phụ huynh, số bé, biểu đồ 14 ngày, "Việc cần làm" (báo cáo, video và nguồn chờ duyệt, câu hỏi gợi ý, yêu cầu xóa dữ liệu, đăng nhập thất bại 24 giờ).
- `/admin-stats`: chọn **7 / 30 / 90 ngày**, thống kê theo môn, bài, bài tập, câu hay sai, từ khóa tìm → **"CSV bài học"**.

**C. Thông báo và cài đặt (US-50)**
- `/notifications`: tab **"Tất cả"** / **"Chưa đọc"**, **"Xem"** (đánh dấu đã đọc và mở link), **"Đánh dấu đã đọc"** tất cả, xóa.
- `/settings`: giao diện sáng/tối, bật/tắt từng loại thông báo, email báo cáo tuần → **"Lưu cài đặt"**; đổi ảnh → **"Lưu ảnh"**.

**D. Nhật ký (US-51)**
- `/admin-audit`: tab **"Thao tác"** và **"Đăng nhập"**, lọc người, loại, ngày → **"Lọc"**, xuất CSV.

**E. Dữ liệu của bé (US-52)**
1. Phụ huynh `/privacy` → **"Tải dữ liệu (JSON)"**.
2. Yêu cầu xóa: gõ đúng tên bé để xác nhận → **"Gửi yêu cầu xóa"**; chưa xử lý thì **"Hủy yêu cầu"**.
3. Admin `/admin-data-requests` → tab **"Cần xử lý"** → **"Xóa dữ liệu"** hoặc **"Từ chối"** (kèm lý do). Phụ huynh nhận thông báo.

### Ca lỗi nên demo

| Thao tác | Kết quả đúng |
|---|---|
| Gõ sai tên bé khi gửi yêu cầu xóa | Không gửi được |
| Mở thông báo có link ra ngoài trang | Không chuyển hướng ra ngoài |
| `/admin-stats` | Chỉ có số tổng, không có tên bé cụ thể |
| Báo cáo tuần trước trống ngay sau khi chạy app | Chờ khoảng 20 giây (job nền chạy lượt đầu) rồi tải lại |

---

## Mở rộng: bài của gia đình, đề xuất bài học, kho cộng đồng

1. Phụ huynh `/family-lessons` → **"Soạn bài mới"** → **"Tạo bài"** → **"Thêm bài đọc"**, **"Thêm video"**, **"Tải lên"**, **"Thêm câu hỏi"** → đổi trạng thái "Bé học được" → **"Xem như phụ huynh"** để xem trước.
2. **"Gửi chia sẻ"** (đồng ý hiện tên) → Admin `/admin-community` → **"Xem và duyệt"** → **"Duyệt lên cộng đồng"** / **"Từ chối"**; sau này có **"Gỡ khỏi cộng đồng"**.
3. Phụ huynh `/lesson-requests` → **"Gửi đề xuất"**; phụ huynh B bấm **"Tôi cũng cần"** → Admin `/admin-lesson-requests` đổi trạng thái → **"Cập nhật và báo gia đình"**.

Ca lỗi: đăng nhập phụ huynh B, bài riêng của A không hiện; A sửa bài đã duyệt thì bài tự quay về chờ duyệt.

---

## 9. Gặp vướng khi demo: nguyên nhân và cách gỡ

| Hiện tượng | Nguyên nhân thường gặp | Cách gỡ |
|---|---|---|
| Trang không có giao diện, chữ trơn | Chưa tải thư viện CSS/JS | `libman restore`, khởi động lại app, Ctrl+F5 |
| Không có admin để đăng nhập | Chưa đặt `Bootstrap:AdminEmail/AdminPassword` | Xem mục 0.2, khởi động lại app |
| Không thấy email xác minh | Development không gửi Gmail | Mở `%LOCALAPPDATA%\SafeLearnKids\Mail`; hoặc `/check-email` → **"Gửi lại yêu cầu xác minh"** |
| Đúng mật khẩu vẫn không vào | Chưa xác minh email, hoặc đang bị khóa 5 phút do sai 5 lần | Xác minh email; đợi 5 phút hoặc admin **"Mở khóa"** |
| Bấm gì cũng báo thử lại sau | Giới hạn số lần thử theo IP | Đợi vài phút hoặc khởi động lại app |
| Vào trang phụ huynh bị đưa về `/kids-home` | Trình duyệt đang ở chế độ trẻ | **"Dành cho ba mẹ (cần PIN)"** → nhập PIN hoặc mật khẩu |
| Bé không thấy bài nào | Bài chưa xuất bản / môn bị ẩn / khác lớp / bé chưa chọn môn đó / chưa đồng ý dữ liệu | Làm lại theo thứ tự mục 0.4; hoặc **"Giao bài"** trực tiếp cho bé |
| Hồ sơ bé bị khóa ở `/kids-picker` | Chưa đồng ý hoặc đã rút đồng ý; hoặc sai PIN bé 5 lần | `/privacy` → **"Đồng ý"**; hoặc đợi 5 phút |
| Không chọn được video khi soạn bài | Chưa có video được duyệt | Sprint 3: duyệt nguồn → đồng bộ → duyệt video |
| **"Xuất bản"** bị từ chối | Bài chưa có nội dung, môn đang ẩn, hoặc có video bị chặn/nguồn bị tạm ngưng | Thêm nội dung, bật môn, thay video |
| Video không phát | Video hoặc nguồn chưa duyệt, gia đình chặn nguồn, máy không có mạng | Kiểm tra `/admin-videos`, `/admin-curate`, `/sources` |
| Đồng bộ nguồn lỗi | Link dạng `@tên-kênh`, hoặc không có mạng | Dùng link `channel/UC...` hoặc `playlist?list=...` |
| Bé không thấy bài tập | Bài tập chưa "Đang mở" hoặc chưa có câu đã duyệt | Duyệt câu hỏi, mở bài tập |
| Đặt 1 phút mà chưa hết giờ | Chỉ tính khi đang ở trang bài học/làm bài, nhịp 30 giây | Để nguyên trang bài học 1–1,5 phút |
| Gợi ý câu hỏi không giống AI | Không có `Ai:ApiKey`, đang dùng bộ offline | Nói rõ khi báo cáo; có key thì đặt bằng `dotnet user-secrets set "Ai:ApiKey" "..."` |
| Sửa code `Program.cs` không thấy thay đổi | Chưa khởi động lại app | Dừng app và chạy lại |

---

## 10. Kịch bản demo nhanh 5 phút cho mỗi sprint

| Sprint | Kịch bản |
|---|---|
| 1 | `/register` → mở mail → **"Xác minh tài khoản"** → `/login` sai 1 lần (thông báo chung) → đúng → `/onboarding` tạo bé + tick đồng ý → `/pin` **"Đặt PIN"** → **"Chế độ trẻ"** → gõ `/parent-home` bị chặn → `/kids-exit` mở khóa → admin `/admin-users` **"Khóa tài khoản"** |
| 2 | `/admin-catalog` **"Thêm môn"** → **"Bài học mới"** → **"Tạo bài nháp"** → thử **"Xuất bản"** khi chưa có nội dung (bị chặn) → **"Thêm nội dung"** (bài đọc + PDF) → **"Xuất bản"** → phụ huynh `/library` thấy bài |
| 3 | `/admin-curate` **"Thêm nguồn"** → **"Đánh giá"** (giải thích điểm) → **"Duyệt nguồn"** → **"Đồng bộ video"** → `/admin-videos` **"Duyệt"** 1, **"Chặn"** 1 → gắn video vào bài → phụ huynh `/sources` **"Chặn cho gia đình"** → video biến mất |
| 4 | Phụ huynh **"Giao bài"** kèm lời nhắn → bé `/kids-home` thấy bài giao → **"Bắt đầu"** → **"Con xong phần này rồi"** → thoát, vào lại học tiếp đúng chỗ → **"Con học xong rồi"** → **"Yêu thích"** → `/kids-search` **"Tìm"** |
| 5 | `/time` đặt 1 phút → bé học tới khi hết giờ → **"Xin ba mẹ thêm giờ"** → ba mẹ **"Cho thêm"** → bé **"Học tiếp"** → bé **"Gửi cho ba mẹ"** báo cáo → admin **"Nhận xem"** → **"Giữ nội dung"** → `/history` |
| 6 | **"Câu hỏi mới"** → **"Gợi ý câu hỏi"** → **"Duyệt"** → **"Tạo bài tập"** → phụ huynh **"Giao bài tập"** có hạn nộp → bé làm, **"Nộp bài"** → `/kids-result` → phụ huynh `/quiz-result/{id}` |
| 7 | Admin tạo huy hiệu mốc thấp + lộ trình → phụ huynh **"Chọn cho bé"**, **"Đặt mục tiêu"** → bé học xong, nhận huy hiệu → `/kids-badges` → `/kids-review` **"Kiểm tra"** → `/progress` |
| 8 | `/weekly` **"In / lưu PDF"** → `/admin-home` → `/admin-stats` 30 ngày, **"CSV bài học"** → `/notifications` → `/privacy` **"Tải dữ liệu (JSON)"**, **"Gửi yêu cầu xóa"** → admin `/admin-data-requests` → `/admin-audit` thấy thao tác vừa làm |

Kết thúc mỗi demo nên mở `/admin-audit` để chứng minh thao tác đã được ghi nhật ký, và mở bảng tương ứng trong SQL Server (xem `docs/HOC-DATABASE.md`) để chứng minh dữ liệu đã lưu thật.
