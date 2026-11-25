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

**Thử trên iPhone, iPad, Android mà không cần deploy:** xem `DEPLOY.md` mục A (điện thoại cùng Wi-Fi mở `http://<IP-máy-tính>:5000`) hoặc mục B (link HTTPS tạm qua Cloudflare Tunnel). Deploy thật lên máy chủ: `DEPLOY.md` mục C.

### 0.2 Tài khoản cần có

| Vai trò | Cách tạo | Ghi chú |
|---|---|---|
| **Admin** | `dotnet user-secrets set "Bootstrap:AdminEmail" "..."` và `"Bootstrap:AdminPassword" "..."` rồi khởi động lại app | `/register` chỉ tạo được Phụ huynh |
| **Phụ huynh A** | `/register` → xác minh email | Dùng làm luồng chính |
| **Phụ huynh B** (nên có) | `/register` → **tạo 1 bé** | Để demo "gia đình khác không thấy bài riêng", "Tôi cũng cần". Chưa có bé thì mọi trang của B bị đẩy về `/onboarding` |
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
4. **Admin** `/admin-questions`: tạo 3–5 câu hỏi cho bài → bấm **"Duyệt"** từng câu → **"Tạo bài tập"** (trạng thái Đang mở).
5. **Admin** `/admin-paths`, `/admin-badges`: 1 lộ trình, vài huy hiệu (Sprint 7).
6. **Phụ huynh** `/onboarding`: tạo bé đúng lớp, **tick đồng ý** chính sách dữ liệu. Bé tự nhận mọi môn đang dùng **lúc tạo**; nếu bước 1 làm sau khi tạo bé thì vào `/profiles` → **"Sửa"** để thêm môn.
7. **Phụ huynh** `/pin`: đặt PIN chế độ trẻ.

### 0.5 Chạy test tự động của sprint (tuỳ chọn, để có bằng chứng)

```powershell
dotnet build SafeLearnKids.Web.csproj
py scripts\smoke-<ten>.py
```

| Sprint | Script |
|---|---|
| 1 | `smoke-sql-auth.py`, `check-auth-regressions.py`, `smoke-admin-users.py`, `smoke-child-access.py`, `smoke-pin.py` (mọi trường hợp PIN) |
| 2 | `smoke-catalog.py`, `smoke-family.py` |
| 3 | `smoke-sources.py` |
| 4 | `smoke-learning.py` |
| 5 | `smoke-time.py` |
| 6 | `smoke-quiz.py`, `smoke-ai.py` (AI qua server Claude giả: đánh giá nguồn, gợi ý câu hỏi, giải thích cách khác, bài nên ôn lại, nút đọc to) |
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
1. `/onboarding` (menu **Thêm hồ sơ bé**). Lần đầu đăng nhập khi chưa có bé, **mọi trang phụ huynh đều tự chuyển về đây**.
   - Chỉ có **một màn hình**: chọn nhân vật, nhập **"Tên hoặc biệt danh của bé"**, chọn **Lớp** 1–5, **"Năm sinh (không bắt buộc)"**, tick ô đồng ý **Chính sách dữ liệu trẻ em** → **"Tiếp tục"** → hồ sơ được tạo ngay, chuyển về `/parent-home`.
   - Thanh bên trái ghi "3 Chọn môn và giờ học", "4 Đặt mã PIN" nhưng đó chỉ là hình minh họa, không có màn hình riêng.
   - Bé được **tự gán mọi môn đang dùng tại thời điểm tạo**. Môn admin tạo sau đó sẽ không tự thêm cho bé; muốn thêm thì vào `/profiles` → **"Sửa"**.
2. `/profiles`: mỗi thẻ bé có **"Sửa"** (đổi tên, lớp, môn), **"Giờ học"**, **"Vào học"**; ô "Thêm bé thứ N" khi chưa đủ 4 bé.

**E. PIN và chế độ trẻ (US-08, 09)**
0. Bé mới tạo và tài khoản mới **chưa có PIN nào**. Hệ thống nhắc ở 2 chỗ: mục "Cần ba mẹ xem" trên `/parent-home` và khung **"Dành cho ba mẹ"** khi bấm "Chế độ trẻ"; cả hai có nút dẫn tới `/pin`.
1. `/pin`: **"Đặt PIN"** cho chế độ trẻ (4 số, phải nhập mật khẩu tài khoản).
2. Mục "PIN riêng của từng bé": **"Đặt PIN"** cho bé (không bắt buộc, nên dùng khi nhà có từ 2 bé), **"Bỏ PIN"** để gỡ.
3. Bấm **"Chế độ trẻ"** ở thanh trên → `/kids-picker` → chọn bé → (bé có PIN thì nhập PIN) → **"Vào học"** → `/kids-home`.
4. Thoát: **"Dành cho ba mẹ (cần PIN)"** → `/kids-exit` → bấm 4 số PIN trên bàn phím số. **Bấm đủ số thứ 4 là tự gửi**, không cần bấm "Mở khóa" (nút "Mở khóa" chỉ dùng khi gõ PIN bằng bàn phím máy tính). Quên PIN thì **"Mở khóa bằng mật khẩu"**.

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
| Ba mẹ **chưa đặt PIN**, xem `/parent-home` | Mục "Cần ba mẹ xem" có dòng **"Chưa đặt mã PIN chế độ trẻ"** kèm nút **"Đặt PIN"**. Đặt PIN xong thì dòng này biến mất |
| Ba mẹ **chưa đặt PIN** mà bấm **"Chế độ trẻ"** | Màn hình chọn bé hiện khung vàng **"Dành cho ba mẹ: Ba mẹ chưa đặt mã PIN chế độ trẻ…"** kèm nút **"Đặt PIN ngay"**; nút thoát ghi **"Dành cho ba mẹ (cần mật khẩu)"**. Trang phụ huynh vẫn bị khóa khi bé đã vào học |
| Bé chọn xong, quay lại màn hình chọn bé để đổi bạn | Khung "Dành cho ba mẹ" **không** hiện nữa (lời nhắc chỉ dành cho lúc ba mẹ vừa mở) |
| Thoát khi chưa có PIN ba mẹ | `/kids-exit` hiện "Ba mẹ nhập mật khẩu" và "Ba mẹ chưa đặt PIN nên cần mật khẩu tài khoản…". Sai mật khẩu: "Mật khẩu chưa đúng. Còn N lần thử." |
| Nhà có **từ 2 bé**, có bé **chưa có PIN riêng** | Khung "Dành cho ba mẹ" nêu tên các bé chưa có PIN riêng; trang Tổng quan có dòng "[tên bé] chưa có PIN riêng" ở cuối danh sách. Bấm vào thẻ bé vẫn vào học ngay vì PIN riêng không bắt buộc. Nhà **1 bé** thì không nhắc PIN riêng |
| Bé có PIN riêng | Thẻ ghi "· có mã PIN", bấm vào phải nhập PIN; đổi sang bé khác rồi quay lại vẫn phải nhập lại |
| Ba mẹ đổi PIN (của ba mẹ hoặc của bé) | PIN cũ hết tác dụng ngay. Đặt PIN mới cho bé đang bị khóa sẽ **gỡ khóa** luôn |
| Sai PIN ba mẹ 5 lần ở `/kids-exit` | Khóa 5 phút, trong lúc khóa nhập đúng PIN hay mật khẩu đều chưa thoát được; máy của ba mẹ không bị ảnh hưởng |
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
2. **"Đồng bộ video"** → trang hiện băng xanh **"Đang đồng bộ video…"** và **tự tải lại** cho tới khi xong. Việc lấy danh sách chạy nền nên không phải chờ trên nút; bấm lại khi đang chạy cũng không xếp lệnh trùng. Xong thì hiện **"Lần đồng bộ gần nhất: N video, M video mới chờ duyệt"**, lỗi thì hiện **"Đồng bộ chưa thành công: …"**. Video có tiêu đề chứa từ khóa bị chặn tự động bị chặn.
3. **"Đánh giá"** (nên đồng bộ trước để có tiêu đề video):
   - Không có `Ai:ApiKey`: bộ quy tắc `rule-v1` (bắt đầu 60, cộng/trừ theo quy tắc, ghi lý do từng điểm), nhãn **"Theo quy tắc"**.
   - Có `Ai:ApiKey`: Claude đọc tên, mô tả, 50 tiêu đề gần nhất và kết quả bộ quy tắc, trả về điểm, các vấn đề cần lưu ý và lý do kèm dẫn chứng, nhãn **"AI + quy tắc"**. Nếu bộ quy tắc thấy từ khóa bị chặn, điểm AI không được cao hơn điểm quy tắc.
   - Từ 75 điểm gợi ý duyệt, dưới 50 gợi ý từ chối. Gọi AI lỗi thì tự dùng kết quả bộ quy tắc và ghi rõ.
4. **"Duyệt nguồn"**; hoặc **"Từ chối nguồn"** / **"Tạm ngưng nguồn"** (bắt buộc ghi lý do).

**B. Duyệt video (US-19, 20)**
1. **"Video của nguồn"** hoặc menu **Duyệt video** (`/admin-videos`) → lọc trạng thái → **"Lọc"**.
2. Từng video: **"Duyệt"** hoặc **"Chặn"** (ghi lý do).
3. **Hàng loạt:** tick ô đầu mỗi video hoặc **"Chọn cả trang"** (hiện "N video đã chọn") → **"Duyệt đã chọn"**, hoặc ghi **"Lý do chặn"** rồi **"Chặn đã chọn"**. Tối đa 200 video một lần; mỗi lần ghi một dòng nhật ký.
4. Thêm 1 video lẻ không cần cả kênh: ô **"Thêm video"** nhận link `watch?v=`, `youtu.be/`, `shorts/`, `embed/`, `live/` hoặc mã video; có thể chọn "Duyệt luôn".
5. Nút **"Đồng bộ nguồn đã duyệt"** ở `/admin-curate` xếp lịch đồng bộ mọi nguồn đã duyệt; chạy nền lần lượt từng nguồn, cột "Đồng bộ" hiện **"Đang đồng bộ"** tới khi xong.

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
| **"Chặn đã chọn"** khi chưa ghi lý do / chưa tick video nào | Trình duyệt nhắc ngay; server cũng từ chối |
| Bấm **"Đồng bộ video"** 2 lần liền | Lần 2 báo "Nguồn này đang được đồng bộ", không chạy trùng |
| Tắt app giữa lúc đang đồng bộ rồi mở lại | Lệnh còn dở được chạy tiếp khi app khởi động |

### Giải thích nghiệp vụ
- **Hai lớp duyệt:** video phát được khi **video đã duyệt** và **ít nhất một nguồn chứa nó đang được duyệt** (video lẻ chỉ cần chính nó được duyệt), và gia đình không chặn nguồn đó.
- **Vì sao dùng `youtube-nocookie` trong iframe có sandbox?** Không đặt cookie theo dõi, không cho video điều hướng trang hay mở popup.
- **"Đánh giá tự động" có phải AI không?** Có key thì có: Claude chấm cùng bộ quy tắc, mọi lý do đều hiện ra. Không có key thì chỉ là bộ quy tắc `rule-v1`. Dù là gì, **admin vẫn là người quyết định**; tiêu đề video được coi là dữ liệu, AI không làm theo nội dung trong đó; điểm AI không vượt được danh sách từ khóa bị chặn.
- **Vì sao đồng bộ chạy nền?** Một kênh có thể tới 200 video, mạng chậm mất 10–20 giây; chạy trong lúc admin bấm nút dễ bị hết thời gian chờ.

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
4. Bài chia thành từng phần. Học xong một phần → **"Con xong phần này rồi"** → mở phần tiếp theo. Phần bài đọc có nút **"🔊 Đọc to cho con nghe"** (xem mục Đọc to ở Sprint 6).
5. Phần cuối → **"Con học xong rồi"** → bài hoàn thành, bài được giao tự đóng.
6. Thoát giữa chừng rồi vào lại: tiếp tục đúng phần đang dở.

**C. Tìm kiếm và yêu thích (US-27, 28)**
- Trang bài học: **"Yêu thích"** → đổi thành **"Đã thích"**; bài hiện ở mục yêu thích trên `/kids-home`.
- `/kids-search`: gõ từ khóa → **"Tìm"**.
- Ba mẹ tắt tìm kiếm: `/profiles` → **"Sửa"** → công tắc **"Cho phép tìm kiếm"** → lưu. Công tắc lưu trong SQL và server kiểm tra, đổi máy vẫn giữ.

### Ca lỗi nên demo

| Thao tác | Kết quả đúng |
|---|---|
| Ba mẹ tắt "Cho phép tìm kiếm", bé gõ `/kids-search?q=...` | Trang báo **"Ba mẹ đã tắt tìm kiếm."**, không trả kết quả, không ghi lịch sử tìm |
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
1. Bé: cột phải trang bài học, **bấm mở** mục thu gọn **"▸ Bài này có gì lạ?"** → chọn lý do → **"Gửi cho ba mẹ"**.
2. Phụ huynh: trang `/lesson-detail/{id}` → **bấm mở** mục thu gọn **"Báo cáo nội dung này"** → chọn **"Lý do"**, **"Ghi chú"** → **"Gửi báo cáo"**.
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
1. Admin `/admin-questions` → chọn bài học → **"Câu hỏi mới"** → loại Trắc nghiệm (2–4 đáp án, đúng 1 đáp án đúng) hoặc Đúng/Sai, độ khó 1–3, giải thích đáp án, **"Trạng thái"** → **"Thêm câu hỏi"**. Mặc định là **Nháp**; chọn sẵn trạng thái đã duyệt trong ô "Trạng thái" thì dùng được ngay, không cần bấm thêm. Câu Nháp không đưa vào bài tập được.
2. Ở danh sách: **"Duyệt"**, **"Từ chối"**, **"Lưu trữ"**, **"Khôi phục"**, **"Sửa"**.
3. **"Gợi ý câu hỏi"** (1–5 câu): câu gợi ý vào **"Chờ duyệt"**. Không có `Ai:ApiKey` thì dùng bộ sinh offline; có key thì gọi Claude, và câu nào sai quy tắc (đáp án đúng không hợp lệ, trùng câu đã có...) bị loại.
4. **"Giải thích cách khác cho bé"** (mở câu đã lưu bằng **"Sửa"**): tự viết, hoặc bấm **"AI soạn gợi ý"** để điền bản nháp vào ô (có key: Claude viết theo ví dụ đời thường hoặc chia nhỏ từng bước, đúng lớp của bài; không key: mẫu nhắc lại đáp án và giải thích). AI **chỉ điền nháp**; admin đọc, sửa, bấm **"Lưu câu hỏi"** thì bé mới thấy. Dòng ghi chú cho biết bản đang lưu do admin viết hay do AI soạn và admin duyệt.

**B. Tạo bài tập (US-36)**
- **"Tạo bài tập"** → chọn các câu **đã duyệt**, điểm đạt (%), thời gian làm bài, xáo trộn câu, trạng thái Đang mở → **"Tạo bài tập"**.

**C. Giao bài tập có hạn nộp (US-37)**
- Phụ huynh `/lesson-detail/{id}` → thẻ "Bài tập kiểm tra" → chọn **"Bài tập"**, bé, **"Hạn nộp"** (bắt buộc), lời nhắn → **"Giao bài tập"**.

**D. Bé làm bài và xem kết quả (US-38, 39)**
1. Bé mở bài học → bấm tên bài tập → `/kids-quiz`.
2. Làm từng câu, có đồng hồ đếm ngược. Bấm vào đáp án → đáp án được tô, **trang không tự chuyển câu** → bấm **"Câu tiếp theo"** (nút sáng lên khi câu đã có đáp án), **"Câu trước"** để quay lại. Hàng **chấm tròn đánh số** cho biết câu nào đã làm (xanh) và cho bấm nhảy thẳng tới câu bất kỳ. Ở câu cuối → **"Nộp bài"**; còn câu trống thì hỏi lại và đưa về câu trống.
3. **Đọc to:** mỗi câu có nút **"🔊 Đọc to"** đọc câu hỏi và các đáp án bằng giọng tiếng Việt của máy (chậm hơn bình thường một chút), bấm lần nữa để dừng. Nút chỉ hiện khi máy có giọng tiếng Việt (Windows/Android/iPad thường có sẵn), không gửi dữ liệu ra ngoài.
4. `/kids-result`: điểm, số sao, đáp án đã chọn, đáp án đúng, giải thích. Câu làm sai có **"💡 Giải thích cách khác"** nếu admin đã soạn. Nút **"Làm lại"**, **"Xem lại bài học"**, **"Về trang học"**.
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
- Câu AI tạo **luôn chờ admin duyệt**, và lời "giải thích cách khác" do AI soạn cũng phải được admin lưu: **không có chữ nào của AI tới thẳng tay trẻ**.
- Trước đây chọn đáp án là tự sang câu sau, nên trẻ bấm thêm "Câu tiếp theo" sẽ bị nhảy mất một câu. Nay bỏ tự chuyển và thêm chấm tròn để trẻ thấy câu nào còn trống.
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
- `/kids-review`: các câu lần gần nhất làm sai hoặc bỏ trống → chọn lại → **"Kiểm tra"**; **"Làm lại bài tập"** để xóa câu khỏi danh sách. Mỗi câu có nút đọc to và **"💡 Giải thích cách khác"** (nếu admin đã soạn, tự mở khi bé kiểm tra lại vẫn sai).
- **Học thích ứng:** đầu trang `/kids-review` có mục **"Con nên học lại bài nào?"**: các bài có câu bé làm sai, bài sai nhiều xếp trước, bấm để mở lại bài. Trang chủ `/kids-home` có thẻ **"Nên học lại: [tên bài]"** cho bài cần ôn nhất. Chỉ gợi ý bài bé còn được mở.

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
2. Yêu cầu xóa: **bấm mở** mục thu gọn **"Yêu cầu xóa hồ sơ và dữ liệu"** dưới thẻ bé → gõ đúng tên bé để xác nhận → **"Gửi yêu cầu xóa"**; chưa xử lý thì **"Hủy yêu cầu"**.
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
| Trang nguồn hiện "Đang đồng bộ video…" khá lâu | Đồng bộ chạy nền; một kênh tới 200 video, mạng chậm mất 10–20 giây; "Đồng bộ nguồn đã duyệt" chạy lần lượt từng nguồn | Để trang tự tải lại; không cần bấm lại (bấm lại cũng không chạy trùng) |
| Bé không thấy bài tập | Bài tập chưa "Đang mở" hoặc chưa có câu đã duyệt | Duyệt câu hỏi, mở bài tập |
| Đặt 1 phút mà chưa hết giờ | Chỉ tính khi đang ở trang bài học/làm bài, nhịp 30 giây | Để nguyên trang bài học 1–1,5 phút |
| Gợi ý câu hỏi không giống AI | Không có `Ai:ApiKey`, đang dùng bộ offline | Nói rõ khi báo cáo; có key thì đặt bằng `dotnet user-secrets set "Ai:ApiKey" "..."` |
| Sửa code `Program.cs` không thấy thay đổi | Chưa khởi động lại app | Dừng app và chạy lại |
| Phụ huynh bấm menu nào cũng về `/onboarding` | Tài khoản chưa có bé nào | Tạo 1 hồ sơ bé |
| Không thấy ô chọn lý do báo cáo / ô gõ tên khi xóa dữ liệu | Nằm trong mục thu gọn ("Bài này có gì lạ?", "Báo cáo nội dung này", "Yêu cầu xóa hồ sơ và dữ liệu") | Bấm vào tiêu đề mục để mở |
| Hàng chờ `/admin-videos` quá dài (hàng trăm video sau khi đồng bộ) | Mỗi trang 24 video | Lọc theo nguồn, **"Chọn cả trang"** rồi **"Duyệt đã chọn"** / **"Chặn đã chọn"** |
| Không thấy nút "🔊 Đọc to" | Máy/trình duyệt không có giọng tiếng Việt (nút tự ẩn để không đọc sai) | Windows: Settings → Time & language → Speech → thêm giọng Tiếng Việt; hoặc dùng Edge/Chrome trên máy có sẵn giọng Việt |
| "Đánh giá" nguồn vẫn ghi "Theo quy tắc" dù đã có key | Nguồn chưa có video (AI cần tiêu đề để chấm), hoặc gọi AI lỗi (trang ghi rõ lý do) | Đồng bộ video trước rồi bấm **"Đánh giá lại"** |

---

## 10. Kịch bản demo nhanh 5 phút cho mỗi sprint

| Sprint | Kịch bản |
|---|---|
| 1 | `/register` → mở mail → **"Xác minh tài khoản"** → `/login` sai 1 lần (thông báo chung) → đúng → `/onboarding` tạo bé + tick đồng ý → `/pin` **"Đặt PIN"** → **"Chế độ trẻ"** → gõ `/parent-home` bị chặn → `/kids-exit` mở khóa → admin `/admin-users` **"Khóa tài khoản"** |
| 2 | `/admin-catalog` **"Thêm môn"** → **"Bài học mới"** → **"Tạo bài nháp"** → thử **"Xuất bản"** khi chưa có nội dung (bị chặn) → **"Thêm nội dung"** (bài đọc + PDF) → **"Xuất bản"** → phụ huynh `/library` thấy bài |
| 3 | `/admin-curate` **"Thêm nguồn"** → **"Đồng bộ video"** (trang tự cập nhật) → **"Đánh giá"** (giải thích điểm, AI nếu có key) → **"Duyệt nguồn"** → **"Video của nguồn"**: **"Chặn"** 1 video (ghi lý do), **"Chọn cả trang"** → **"Duyệt đã chọn"** → gắn video vào bài → phụ huynh `/sources` **"Chặn cho gia đình"** → video biến mất |
| 4 | Phụ huynh **"Giao bài"** kèm lời nhắn → bé `/kids-home` thấy bài giao → **"Bắt đầu"** → **"Con xong phần này rồi"** → thoát, vào lại học tiếp đúng chỗ → **"Con học xong rồi"** → **"Yêu thích"** → `/kids-search` **"Tìm"** |
| 5 | `/time` đặt 1 phút → bé học tới khi hết giờ → **"Xin ba mẹ thêm giờ"** → ba mẹ **"Cho thêm"** → bé **"Học tiếp"** → bé **"Gửi cho ba mẹ"** báo cáo → admin **"Nhận xem"** → **"Giữ nội dung"** → `/history` |
| 6 | **"Câu hỏi mới"** (chọn trạng thái đã duyệt) → **"Sửa"** → **"AI soạn gợi ý"** → **"Lưu câu hỏi"** → **"Gợi ý câu hỏi"** → **"Duyệt"** → **"Tạo bài tập"** → phụ huynh **"Giao bài tập"** có hạn nộp → bé làm (chỉ ra chấm tròn, **"🔊 Đọc to"**), cố tình sai 1 câu, **"Nộp bài"** → `/kids-result` mở **"💡 Giải thích cách khác"** → `/kids-review` thấy **"Con nên học lại bài nào?"** |
| 7 | Admin tạo huy hiệu mốc thấp + lộ trình → phụ huynh **"Chọn cho bé"**, **"Đặt mục tiêu"** → bé học xong, nhận huy hiệu → `/kids-badges` → `/kids-review` **"Kiểm tra"** → `/progress` |
| 8 | `/weekly` **"In / lưu PDF"** → `/admin-home` → `/admin-stats` 30 ngày, **"CSV bài học"** → `/notifications` → `/privacy` **"Tải dữ liệu (JSON)"**, **"Gửi yêu cầu xóa"** → admin `/admin-data-requests` → `/admin-audit` thấy thao tác vừa làm |

Kết thúc mỗi demo nên mở `/admin-audit` để chứng minh thao tác đã được ghi nhật ký, và mở bảng tương ứng trong SQL Server (xem `docs/HOC-DATABASE.md`) để chứng minh dữ liệu đã lưu thật.

---

## 11. Kết quả kiểm thử toàn bộ luồng (25/11/2026)

Lần chạy cuối, sau đợt nâng cấp (đồng bộ chạy nền, duyệt video hàng loạt, công tắc tìm kiếm lưu SQL, khóa PIN lưu SQL, US-22 dùng Claude, giải thích cách khác, bài nên ôn lại, đọc to, sửa lỗi bỏ sót câu khi làm bài).

**A. 15 bộ test tự động** (`scripts/`, HTTP thật vào app + SQL Server, chạy trên DB dev): **15/15 bộ đạt, 996 kiểm tra PASS**.

| Bộ test | PASS | Bộ test | PASS |
|---|---|---|---|
| `smoke-sql-auth` | 72 | `smoke-sources` | 87 |
| `check-auth-regressions` | 82 | `smoke-learning` | 59 |
| `smoke-admin-users` | 137 | `smoke-time` | 56 |
| `smoke-child-access` | 43 | `smoke-quiz` | 69 |
| `smoke-pin` (mới) | 68 | `smoke-ai` (mới) | 40 |
| `smoke-catalog` | 68 | `smoke-progress` | 62 |
| `smoke-family` | 60 | `smoke-home` | 21 |
| | | `smoke-reports` | 72 |

- `smoke-pin`: mọi trường hợp PIN ba mẹ và PIN riêng của bé, các lời nhắc khi chưa có PIN, khóa PIN còn nguyên sau khi khởi động lại app.
- `smoke-ai`: dựng **server Claude giả** trên máy nên không cần key thật, không tốn phí. Kiểm tra prompt gửi đi (tiêu đề video, kết quả quy tắc, schema JSON, dặn coi tiêu đề là dữ liệu), kết quả AI được kiểm soát (cờ lạ bị bỏ, lý do bị cắt độ dài, từ khóa chặn giới hạn điểm), AI lỗi thì quay về bộ quy tắc, key không lọt vào log, bản nháp giải thích chỉ lưu khi admin bấm lưu.
- `smoke-sources` trước đây FAIL trên DB có nhiều video chờ duyệt; nay test lọc theo nguồn của chính nó nên chạy được trên mọi DB.

**B. Đi theo đúng file hướng dẫn này trong trình duyệt thật** (Playwright, 4 cửa sổ, DB mới dựng từ `database/*.sql`, admin tạo bằng `Bootstrap:AdminEmail`): **89/89 bước PASS**.

**C. Kiểm tra giao diện các tính năng mới** (Playwright): **12/12 PASS**. Gồm: chọn đáp án không tự chuyển câu và không còn bỏ sót câu (3 câu, 0 câu trống); chấm tròn nhảy câu; nộp khi còn câu trống thì hỏi lại; đọc to bằng giọng vi-VN (giả lập), bấm lại thì dừng, máy không có giọng Việt thì ẩn nút; "AI soạn gợi ý" chỉ điền nháp; chọn cả trang và chặn hàng loạt phải có lý do; trang nguồn tự tải lại khi đang đồng bộ; 0 lỗi JavaScript.

**D. Quét toàn bộ giao diện** (`scripts/crawl-ui.py`, 130 trang, 4 vai): 0 lỗi server, 0 link hỏng, 0 lỗi script, 0 tài nguyên lỗi, 0 trang tràn ngang trên điện thoại. 1 "nút không phản hồi" là ô lớp "3" đã chọn sẵn ở `/onboarding` (báo động giả).

**Lỗi tìm ra và đã sửa trong quá trình kiểm thử:**
- Làm bài tập: chọn đáp án tự chuyển câu, bấm thêm "Câu tiếp theo" thì bỏ sót một câu → bỏ tự chuyển, thêm chấm tròn.
- Không nhắc gì khi chưa tạo PIN; nút thoát luôn ghi "cần PIN" → thêm lời nhắc ở trang Tổng quan và màn hình chọn bé, nút thoát ghi đúng.
- Đánh giá nguồn bằng AI lưu tên model kèm dấu ngoặc kép (do `ToString()` của SDK) → đã sửa, có test.
- Khóa PIN ba mẹ mất khi khởi động lại app; công tắc tìm kiếm chỉ nằm ở trình duyệt; đồng bộ YouTube làm admin chờ 10–20 giây trên nút → đã chuyển vào SQL / chạy nền.

**Những điểm hướng dẫn cũ ghi chưa đúng, đã sửa trong file này:** onboarding chỉ có 1 màn hình; thẻ bé ở `/profiles` có "Sửa / Giờ học / Vào học"; bàn phím PIN tự mở khóa khi đủ 4 số; câu hỏi chọn được trạng thái đã duyệt ngay trong form (không bắt buộc bấm "Duyệt" thêm); báo cáo nội dung và yêu cầu xóa dữ liệu nằm trong mục thu gọn; phụ huynh chưa có bé bị đẩy về `/onboarding`.
