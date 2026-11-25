# Triển khai SafeLearn Kids

Có 3 cách đưa app ra khỏi `localhost`. Chọn theo mục đích:

| Cách | Dùng khi | Thời gian | Cần gì | Ai vào được |
|---|---|---|---|---|
| **A. Cùng mạng Wi-Fi** | Thử nhanh trên iPhone, iPad, Android ngay tại chỗ | ~2 phút | Máy tính đang chạy app, điện thoại cùng Wi-Fi | Chỉ thiết bị trong cùng mạng |
| **B. Đường hầm tạm (Cloudflare Tunnel)** | Cần link **HTTPS** công khai để thử từ xa, gửi cho bạn trong nhóm hoặc giảng viên xem thử | ~10 phút | Cài `cloudflared`, máy tính phải bật | Ai có link (link đổi mỗi lần chạy) |
| **C. Máy chủ VPS + Docker** | Chạy lâu dài, demo báo cáo, thử nghiệm kín với các gia đình | 1–2 giờ lần đầu | VPS, tên miền, dịch vụ gửi mail | Ai có địa chỉ web |

> **Không phải deploy mới test được trên điện thoại.** Cách A đủ để thử giao diện, chế độ trẻ, làm bài và nút đọc to trên iPhone. Cách B dùng khi cần HTTPS hoặc người ở xa. Cách C là chạy thật.

> **Trước khi cho người ngoài nhóm dùng** (dù chỉ thử nghiệm), đọc mục [C10](#c10-trước-khi-cho-gia-đình-thật-dùng). Hệ thống chứa dữ liệu của trẻ em.

---

## A. Thử trên điện thoại cùng mạng Wi-Fi

App chạy ở chế độ Development trên máy tính, dùng database `hoctapchotre` trên máy như khi lập trình. Email không gửi thật mà lưu thành file `.eml` trên máy tính (xem `README.md`).

**A1. Lấy địa chỉ IP Wi-Fi của máy tính** (PowerShell):

```powershell
Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.IPAddress -notlike '127.*' } | Select-Object InterfaceAlias, IPAddress
```

Lấy IP ở dòng **`Wi-Fi`** (thường dạng `192.168.x.x`). Bỏ qua các dòng `VMware`, `vEthernet`, `VPN`, `Bluetooth` và địa chỉ `169.254.x.x`: đó là card mạng ảo, điện thoại không vào được.

**A2. Cho điện thoại đi qua tường lửa Windows** (PowerShell **chạy với quyền Administrator**, làm một lần):

```powershell
# Mạng Wi-Fi ở nhà/trường nên để chế độ Private. Xem: Get-NetConnectionProfile
Set-NetConnectionProfile -InterfaceAlias "Wi-Fi" -NetworkCategory Private
New-NetFirewallRule -DisplayName "SafeLearnKids dev 5000" -Direction Inbound -Protocol TCP -LocalPort 5000 -Action Allow -Profile Private
```

Wi-Fi công cộng (quán cà phê, ký túc xá) nên **không** chuyển sang Private; dùng cách B thay vì mở tường lửa ở mạng lạ.

**A3. Chạy app, lắng nghe trên mọi địa chỉ mạng** (tại thư mục chứa `SafeLearnKids.Web.csproj`):

```powershell
dotnet build SafeLearnKids.Web.csproj
dotnet run --project SafeLearnKids.Web.csproj --no-build --launch-profile http -- --urls http://0.0.0.0:5000
```

Phần `-- --urls http://0.0.0.0:5000` là điểm khác so với cách chạy thường (mặc định chỉ nhận `localhost`).

**A4. Trên iPhone/iPad/Android** (cùng Wi-Fi): mở Safari hoặc Chrome, gõ `http://<IP ở bước A1>:5000`, ví dụ `http://192.168.110.29:5000`.

- Có thể bấm **Chia sẻ → Thêm vào MH chính** để mở như một ứng dụng.
- Link trong email xác minh trỏ về `http://localhost:5000`, nên mở thư `.eml` và bấm link **trên máy tính**. Có thể tạo sẵn tài khoản trên máy tính rồi đăng nhập trên điện thoại.

**A5. Xong thì đóng lại:** `Ctrl+C` để dừng app. Muốn xóa luật tường lửa:

```powershell
Remove-NetFirewallRule -DisplayName "SafeLearnKids dev 5000"
```

**Không vào được từ điện thoại?** Kiểm tra theo thứ tự: điện thoại có đúng cùng Wi-Fi không (không phải 4G); IP có đúng dòng `Wi-Fi` không; mạng có ở chế độ Private không; luật tường lửa đã tạo chưa; một số router bật "AP isolation / Client isolation" chặn các máy nói chuyện với nhau, khi đó dùng cách B.

---

## B. Link HTTPS tạm thời qua Cloudflare Tunnel

`cloudflared` tạo một đường hầm từ máy bạn ra Internet và cấp link dạng `https://<tên-ngẫu-nhiên>.trycloudflare.com`. Không cần tên miền hay tài khoản, không mở cổng router. Máy tính phải bật suốt thời gian thử; tắt là link chết; mỗi lần chạy link khác nhau.

**B1. Cài** (làm một lần):

```powershell
winget install --id Cloudflare.cloudflared
```

**B2. Mở đường hầm** (cửa sổ PowerShell thứ nhất, để nguyên):

```powershell
cloudflared tunnel --url http://127.0.0.1:5000
```

Trong log hiện ra dòng có `https://....trycloudflare.com`. Chép lại link đó. Dùng `127.0.0.1` thay cho `localhost` để chắc chắn đi qua IPv4 (khớp với cấu hình proxy ở bước B3).

**B3. Chạy app** (cửa sổ PowerShell thứ hai), thay `<link>` bằng link ở bước B2:

```powershell
$env:Email__PublicBaseUrl = "<link>"          # link trong email trỏ về đường hầm thay vì localhost
$env:Proxy__Enabled = "true"                  # tin header X-Forwarded-* ...
$env:Proxy__KnownProxies__0 = "127.0.0.1"     # ... nhưng chỉ khi đến từ cloudflared trên chính máy này
dotnet run --project SafeLearnKids.Web.csproj --launch-profile http
```

Hai biến proxy giúp app biết người dùng đến bằng HTTPS và lấy đúng IP của họ (giới hạn số lần đăng nhập sai tính theo IP). Không bật thì mọi người dùng bị coi là cùng một IP.

**B4. Mở `<link>`** trên bất kỳ thiết bị nào, kể cả 4G.

**Lưu ý an toàn:** app đang chạy chế độ Development với database trên máy bạn, ai có link cũng vào được trang đăng ký. Chỉ gửi link cho người cần thử, tắt cả hai cửa sổ khi xong. Không dùng cách này cho dữ liệu thật của các gia đình.

---

## C. Máy chủ VPS với Docker (chạy thật)

Một máy chủ Linux chạy 3 container:
- **SQL Server 2022 Express**: miễn phí, tối đa 10 GB dữ liệu;
- **Web app** (`Dockerfile`);
- **Caddy**: tự lấy và gia hạn chứng chỉ HTTPS (Let's Encrypt).

Chỉ Caddy mở cổng ra ngoài (80/443). Database và app chỉ nói chuyện với nhau trong mạng nội bộ của Docker. Khi app khởi động, `Services/SchemaUpdater.cs` tự thêm các cột và bảng mới vào database, chỉ thêm, không xóa hay sửa dữ liệu cũ.

### C1. Chuẩn bị

| Cần | Ghi chú |
|---|---|
| VPS **x86_64**, Ubuntu 22.04 hoặc 24.04, **≥ 2 vCPU, 4 GB RAM**, ≥ 40 GB SSD | SQL Server **không chạy trên ARM** (không dùng VPS ARM, Raspberry Pi hay Docker trên Mac M1/M2/M3 để chạy thật). Gợi ý: DigitalOcean, Vultr, Linode (khu vực Singapore), hoặc VPS trong nước. Sinh viên: GitHub Student Developer Pack, Azure for Students thường có credit. |
| Tên miền | Mua ở Namecheap, Porkbun, Cloudflare; tên miền `.vn` mua ở nhà đăng ký trong nước. |
| Dịch vụ gửi mail (SMTP) | Brevo, Zoho Mail, Mailgun... Bắt buộc ở môi trường Production (app không chạy nếu thiếu). Phải xác minh tên miền (SPF, DKIM) để mail không vào spam. |
| (Không bắt buộc) YouTube Data API key | Không có key: đồng bộ dùng RSS công khai, 15 video mới nhất, không có thời lượng. |
| (Không bắt buộc) Anthropic API key | Bật AI cho: đánh giá nguồn (US-22), gợi ý câu hỏi (US-40), "giải thích cách khác". Không có key: app dùng bộ quy tắc và mẫu có sẵn. Chỉ tốn phí khi admin bấm các nút AI. |

### C2. Trỏ tên miền về máy chủ

Ở trang quản lý DNS của tên miền:
- bản ghi `A`: `@` → IP của VPS;
- bản ghi `A`: `www` → IP đó (hoặc `CNAME` `www` → `@`).

Đợi vài phút rồi kiểm tra: `ping tenmien.com` phải ra đúng IP. Nếu dùng Cloudflare làm DNS, **tắt đám mây cam (để "DNS only")** cho lần đầu để Caddy lấy được chứng chỉ.

### C3. Bảo vệ máy chủ (làm ngay sau khi có VPS)

```bash
ssh root@<IP>
apt update && apt -y upgrade
# Người dùng riêng, không làm việc bằng root
adduser deploy && usermod -aG sudo deploy
rsync --archive --chown=deploy:deploy ~/.ssh /home/deploy
# Tường lửa: chỉ SSH, HTTP, HTTPS
ufw allow OpenSSH && ufw allow 80 && ufw allow 443 && ufw --force enable
# Tự cài bản vá bảo mật
apt -y install unattended-upgrades && dpkg-reconfigure -plow unattended-upgrades
# Bộ nhớ đệm 2 GB (SQL Server cần RAM; tránh bị tắt khi thiếu bộ nhớ)
fallocate -l 2G /swapfile && chmod 600 /swapfile && mkswap /swapfile && swapon /swapfile
echo '/swapfile none swap sw 0 0' >> /etc/fstab
```

Nên đăng nhập SSH bằng khóa (không dùng mật khẩu): sau khi `ssh deploy@<IP>` vào được bằng khóa, sửa `/etc/ssh/sshd_config` thành `PasswordAuthentication no` và `PermitRootLogin no`, rồi `systemctl restart ssh`. **Giữ cửa sổ SSH cũ mở** cho tới khi thử đăng nhập lại thành công.

### C4. Cài Docker

```bash
curl -fsSL https://get.docker.com | sudo sh
sudo usermod -aG docker deploy     # đăng xuất rồi vào lại để có hiệu lực
docker --version && docker compose version
```

### C5. Lấy code và cấu hình

```bash
sudo mkdir -p /opt/safelearnkids && sudo chown deploy:deploy /opt/safelearnkids
git clone https://github.com/Nntienquang/SafeLearnKids.git /opt/safelearnkids
cd /opt/safelearnkids
git checkout main            # hoặc tag phiên bản muốn chạy, ví dụ: git checkout v1.0
cp .env.example .env
chmod 600 .env
nano .env
```

Repo riêng tư: dùng [deploy key](https://docs.github.com/en/authentication/connecting-to-github-with-ssh/managing-deploy-keys) (khóa SSH chỉ đọc cho đúng repo này) thay vì mật khẩu GitHub.

Các biến trong `.env`:

| Biến | Bắt buộc | Ý nghĩa |
|---|---|---|
| `DOMAIN` | Có | Tên miền, không có `https://`, không có `/` ở cuối |
| `MSSQL_SA_PASSWORD` | Có | Mật khẩu quản trị SQL Server (chỉ dùng để tạo database). Mạnh: ≥ 12 ký tự, có chữ hoa, chữ thường, số; **không dùng** `' ; " $` và khoảng trắng |
| `APP_DB_PASSWORD` | Có | Mật khẩu tài khoản SQL riêng của app (`safelearn_app`), cùng quy tắc |
| `MSSQL_MEMORY_LIMIT_MB` | Không | RAM tối đa cho SQL Server: VPS 4 GB → `1536`; 8 GB → `4096` |
| `SMTP_HOST`, `SMTP_PORT`, `SMTP_USER`, `SMTP_PASSWORD` | Có (trừ user/pass nếu dịch vụ không cần) | Thông tin từ dịch vụ mail; cổng 587 (STARTTLS) |
| `MAIL_FROM` | Có | Địa chỉ gửi, phải thuộc tên miền đã xác minh SPF/DKIM |
| `ADMIN_EMAIL`, `ADMIN_PASSWORD` | Lần đầu | Tài khoản quản trị đầu tiên, tạo khi app khởi động nếu chưa có admin nào. **Xóa sau lần đăng nhập đầu** (bước C6) |
| `YOUTUBE_API_KEY` | Không | Đồng bộ đầy đủ danh sách video và thời lượng |
| `AI_API_KEY` | Không | Anthropic API key cho các tính năng AI (xem C1) |

File `.env` chứa mật khẩu: **không bao giờ đưa lên Git** (đã có trong `.gitignore` và `.dockerignore`), không gửi qua chat.

### C6. Chạy lần đầu

```bash
docker compose up -d --build
docker compose logs -f db-init app       # Ctrl+C để thoát xem log, app vẫn chạy
```

Lần đầu mất 5–10 phút: tải image, build app, `db-init` tạo database từ `database/01-schema.sql` và `database/02-reference-data.sql`, tạo tài khoản SQL của app. Trong log app phải có dòng `Database schema is up to date`.

Kiểm tra:
1. `docker compose ps`: `db`, `app`, `caddy` ở trạng thái `running` (`db-init` là `exited (0)`, đúng).
2. `curl https://tenmien.com/healthz` trả `ok`.
3. Mở `https://tenmien.com`, đăng nhập bằng `ADMIN_EMAIL`.
4. **Xóa giá trị `ADMIN_EMAIL` và `ADMIN_PASSWORD` trong `.env`**, rồi `docker compose up -d`.

### C7. Kiểm tra sau khi triển khai (checklist)

Làm theo thứ tự, mỗi dòng mất 1–3 phút. Chi tiết từng thao tác ở `docs/HUONG-DAN-TEST-CHUC-NANG.md`.

| # | Kiểm tra | Đạt khi |
|---|---|---|
| 1 | `https://tenmien.com` và `https://www.tenmien.com` | Có ổ khóa HTTPS; `www` tự chuyển về tên miền chính |
| 2 | `curl -sI https://tenmien.com \| grep -i strict-transport` | Có header HSTS |
| 3 | `http://<IP>:8080`, `http://<IP>:1433` từ máy khác | **Không** vào được (app và SQL chỉ nội bộ) |
| 4 | Đăng ký tài khoản phụ huynh bằng email thật | Nhận được thư xác minh trong hộp thư chính (không phải spam), link trỏ về `https://tenmien.com` |
| 5 | Quên mật khẩu | Nhận thư, đặt lại được |
| 6 | Admin: thêm môn, bài học, tải 1 PDF và 1 ảnh | Xem được trong trang bài; mở link file khi chưa đăng nhập thì bị chặn |
| 7 | Admin: thêm nguồn YouTube → **"Đồng bộ video"** | Trang hiện "Đang đồng bộ…", tự cập nhật, có video chờ duyệt |
| 8 | Duyệt video hàng loạt, gắn video vào bài, xuất bản | Phụ huynh thấy bài trong thư viện, video phát được |
| 9 | Phụ huynh: tạo bé, đặt PIN, **"Chế độ trẻ"**, bé học và làm bài tập | Đếm giờ, chấm điểm, huy hiệu chạy đúng |
| 10 | (Có `AI_API_KEY`) **"Đánh giá"** nguồn, **"Gợi ý câu hỏi"**, **"AI soạn gợi ý"** | Nhãn "AI + quy tắc"; câu gợi ý ở "Chờ duyệt"; bản nháp hiện trong ô |
| 11 | Khởi động lại: `docker compose restart app` | Không bị đăng xuất (khóa đăng nhập được giữ), dữ liệu còn nguyên |
| 12 | `docker compose logs app \| grep -iE "error\|fail"` | Không có lỗi lạ |

### C8. Test trên thiết bị thật

Sau C7, thử trên các thiết bị gia đình hay dùng:

| Thiết bị | Cần thử |
|---|---|
| **iPhone (Safari)** | Đăng nhập, chế độ trẻ, nhập PIN bằng bàn phím số, làm bài (chấm tròn, không nhảy câu), **nút 🔊 Đọc to** (iOS có sẵn giọng tiếng Việt; nếu nút không hiện: Cài đặt → Trợ năng → Nội dung được đọc → Giọng nói → Tiếng Việt), xem video, xem PDF, xoay ngang/dọc |
| **iPad (Safari)** | Như iPhone; thêm "Thêm vào MH chính" và dùng toàn màn hình như ứng dụng của bé |
| **Android (Chrome)** | Như iPhone; giọng đọc dùng "Dịch vụ chuyển văn bản thành giọng nói" của Google (thêm tiếng Việt nếu thiếu) |
| **Máy tính (Chrome, Edge, Firefox)** | Trang quản trị, duyệt hàng loạt, in báo cáo tuần ra PDF |

Ghi lại lỗi kèm: thiết bị, phiên bản hệ điều hành, trình duyệt, ảnh chụp màn hình, các bước làm.

### C9. Cập nhật phiên bản mới và quay lại bản cũ

**Cập nhật** (nên làm lúc ít người dùng):

```bash
cd /opt/safelearnkids
./deploy/backup.sh                       # luôn sao lưu trước
git fetch && git log --oneline HEAD..origin/main   # xem có gì mới
git pull
docker compose up -d --build
docker compose logs -f app               # chờ "Database schema is up to date" và không có lỗi
curl https://tenmien.com/healthz
```

**Quay lại bản trước** khi bản mới có lỗi:

```bash
git log --oneline -5                     # chọn commit trước đó
git checkout <commit>
docker compose up -d --build
```

Các thay đổi database của `SchemaUpdater` chỉ **thêm** cột hoặc bảng, nên code cũ vẫn chạy được trên database mới; không cần khôi phục database khi quay lại code. Chỉ khôi phục database (C11) khi dữ liệu bị hỏng.

### C10. Trước khi cho gia đình thật dùng

Kỹ thuật chạy được chưa có nghĩa là được phép thu thập dữ liệu của trẻ. Trước khi mời người ngoài nhóm:
- Ghi rõ đây là **bản thử nghiệm**, chỉ mời gia đình tự nguyện và có đồng ý bằng văn bản.
- Rà soát với người có chuyên môn: quy định bảo vệ dữ liệu cá nhân của Việt Nam (Nghị định 13/2023/NĐ-CP và Luật Bảo vệ dữ liệu cá nhân), đặc biệt yêu cầu về **đồng ý của trẻ từ đủ 7 tuổi** (hiện app chỉ lấy đồng ý của ba mẹ); chuyển dữ liệu ra nước ngoài (YouTube, Anthropic); điều khoản của YouTube cho ứng dụng hướng tới trẻ em và chính sách sử dụng của Anthropic.
- Văn bản `/terms` và `/child-data-policy` do nhóm tự viết, cần người có chuyên môn duyệt.
- Có người trực xử lý **báo cáo nội dung** (`/admin-reports`) và **yêu cầu xóa dữ liệu** (`/admin-data-requests`).
- Bật sao lưu hằng ngày (C11) và giám sát (C12) **trước** khi có dữ liệu thật.

### C11. Sao lưu và khôi phục

**Sao lưu tự động hằng ngày:**

```bash
chmod +x deploy/backup.sh
./deploy/backup.sh                       # thử chạy tay một lần
crontab -e
```

Thêm dòng (chạy mỗi ngày lúc 2 giờ sáng):

```
0 2 * * * cd /opt/safelearnkids && ./deploy/backup.sh >> backups/backup.log 2>&1
```

Mỗi lần tạo 3 file trong `backups/`, giữ 14 ngày:
- `hoctapchotre-*.bak`: database;
- `uploads-*.tgz`: ảnh và PDF người dùng tải lên;
- `appdata-*.tgz`: khóa đăng nhập (mất file này thì mọi người bị đăng xuất và link trong email cũ hỏng).

**Chép bản sao ra ngoài máy chủ** ít nhất mỗi tuần. Máy chủ hỏng thì bản sao nằm trên nó cũng mất. Ví dụ từ máy cá nhân:

```powershell
scp -r deploy@<IP>:/opt/safelearnkids/backups D:\SafeLearnKids-backups
```

**Thử khôi phục** (nên làm 1 lần sau khi cài, để chắc bản sao dùng được). Khôi phục vào database **tên khác** để không đụng dữ liệu đang chạy:

```bash
cd /opt/safelearnkids
set -a; . ./.env; set +a
BAK=$(ls -t backups/hoctapchotre-*.bak | head -1)
docker compose cp "$BAK" db:/var/opt/mssql/restore-test.bak
docker compose exec db /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C \
  -Q "RESTORE FILELISTONLY FROM DISK=N'/var/opt/mssql/restore-test.bak'"
# Lấy 2 tên logic (cột LogicalName) ở kết quả trên, điền vào <data> và <log>:
docker compose exec db /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C \
  -Q "RESTORE DATABASE restore_test FROM DISK=N'/var/opt/mssql/restore-test.bak' WITH MOVE N'<data>' TO N'/var/opt/mssql/data/restore_test.mdf', MOVE N'<log>' TO N'/var/opt/mssql/data/restore_test_log.ldf'; SELECT COUNT(*) AS Users FROM restore_test.dbo.Users; DROP DATABASE restore_test;"
docker compose exec db rm -f /var/opt/mssql/restore-test.bak
```

Ra số người dùng (không lỗi) là bản sao dùng được.

**Khôi phục thật** khi dữ liệu hỏng: làm như mục C13 (mang database lên máy chủ), dùng file `.bak` trong `backups/`. File tải lên và khóa đăng nhập giải nén ngược vào volume:

```bash
docker compose stop app
docker run --rm -v safelearnkids_uploads:/data -v "$PWD/backups:/in" alpine sh -c "rm -rf /data/* && tar xzf /in/uploads-<ngày>.tgz -C /data"
docker run --rm -v safelearnkids_appdata:/data -v "$PWD/backups:/in" alpine sh -c "rm -rf /data/* && tar xzf /in/appdata-<ngày>.tgz -C /data"
docker compose up -d
```

### C12. Giám sát

- **Theo dõi app có sống không:** đăng ký một dịch vụ giám sát miễn phí (UptimeRobot, Better Stack...) gọi `https://tenmien.com/healthz` mỗi 5 phút, báo qua email hoặc Telegram khi không trả `ok`. `/healthz` kiểm tra cả kết nối database.
- **Xem nhanh tình trạng:**

  ```bash
  docker compose ps                          # container nào đang chạy
  docker compose logs --since 1h app         # log 1 giờ gần nhất
  docker stats --no-stream                   # CPU, RAM từng container
  df -h /                                    # dung lượng đĩa (SQL Express tối đa 10 GB dữ liệu)
  ```

- **Trong app:** `/admin-home` (việc cần làm, đăng nhập thất bại 24 giờ), `/admin-audit` (nhật ký thao tác và đăng nhập).
- Docker tự chạy lại container bị tắt (`restart: unless-stopped`), kể cả sau khi máy chủ khởi động lại.

**Giới hạn hiện tại: chỉ chạy 1 bản app.** Giới hạn số lần thử theo IP, hàng đợi gửi email và hàng đợi đồng bộ YouTube nằm trong bộ nhớ của tiến trình. Không tăng số bản app (`docker compose up --scale app=2`) hay đặt sau nhiều máy chủ khi chưa chuyển các phần này sang database hoặc Redis. Với một trường hoặc vài trăm gia đình, một VPS 4 GB là đủ.

### C13. Mang dữ liệu đang có trên máy dev lên máy chủ (không bắt buộc)

Bản cài mới chỉ có lớp, môn học và huy hiệu. Muốn giữ bài học và video đã soạn ở máy dev:

1. Trên máy dev:

   ```powershell
   sqlcmd -S . -E -C -Q "BACKUP DATABASE hoctapchotre TO DISK=N'C:\Temp\hoctapchotre.bak' WITH INIT"
   scp C:\Temp\hoctapchotre.bak deploy@<IP>:/opt/safelearnkids/
   ```

2. Trên máy chủ (dừng app trước):

   ```bash
   cd /opt/safelearnkids && set -a; . ./.env; set +a
   docker compose stop app
   docker compose cp hoctapchotre.bak db:/var/opt/mssql/restore.bak
   docker compose exec db /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C \
     -Q "RESTORE FILELISTONLY FROM DISK=N'/var/opt/mssql/restore.bak'"
   # Điền 2 tên logic (LogicalName) vào <data> và <log>:
   docker compose exec db /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C \
     -Q "ALTER DATABASE hoctapchotre SET SINGLE_USER WITH ROLLBACK IMMEDIATE; RESTORE DATABASE hoctapchotre FROM DISK=N'/var/opt/mssql/restore.bak' WITH REPLACE, MOVE N'<data>' TO N'/var/opt/mssql/data/hoctapchotre.mdf', MOVE N'<log>' TO N'/var/opt/mssql/data/hoctapchotre_log.ldf'; ALTER DATABASE hoctapchotre SET MULTI_USER;"
   docker compose up -d      # chạy lại db-init: tạo lại tài khoản SQL của app
   ```

3. Ảnh và PDF của bài học nằm ở `wwwroot\uploads` trên máy dev, không nằm trong database: nén thư mục đó, chép lên và giải nén vào volume `safelearnkids_uploads` (lệnh giống phần khôi phục ở C11).
4. Database dev có tài khoản và dữ liệu thử: **xóa các tài khoản test** ở `/admin-users` trước khi mời người dùng thật.

### C14. Sự cố thường gặp

| Hiện tượng | Nguyên nhân thường gặp | Cách xử lý |
|---|---|---|
| `db` khởi động lại liên tục | Thiếu RAM, hoặc `MSSQL_SA_PASSWORD` quá yếu | `docker compose logs db`; tăng RAM/swap (C3), đặt mật khẩu mạnh rồi **xóa volume `sqldata` nếu là lần cài đầu** (`docker compose down -v` xóa cả dữ liệu, chỉ dùng khi chưa có dữ liệu thật) |
| `db-init` báo lỗi, `app` không lên | Mật khẩu chứa ký tự cấm, hoặc SQL chưa sẵn sàng | `docker compose logs db-init`; sửa `.env`, `docker compose up -d` |
| App dừng ngay khi khởi động, log nhắc `PublicBaseUrl` hoặc `SMTP` | Production bắt buộc HTTPS và SMTP | Kiểm tra `DOMAIN`, `SMTP_HOST`, `MAIL_FROM` trong `.env` |
| Caddy không lấy được chứng chỉ | DNS chưa trỏ đúng, cổng 80/443 bị chặn, hoặc Cloudflare đang bật proxy (đám mây cam) | `docker compose logs caddy`; `ping tenmien.com`; mở cổng ở tường lửa của nhà cung cấp VPS |
| Không nhận được mail xác minh | Sai SMTP, tên miền chưa xác minh SPF/DKIM, mail vào spam | `docker compose logs app`; kiểm tra trang quản lý của dịch vụ mail |
| Mọi người bị đăng xuất sau khi cập nhật | Mất volume `appdata` (khóa đăng nhập) | Không chạy `docker compose down -v`; khôi phục `appdata-*.tgz` (C11) |
| Lỗi 500 khi gửi form | Truy cập thẳng cổng app thay vì qua Caddy | Chỉ dùng `https://tenmien.com` |
| "Đang đồng bộ…" mãi không xong | Không ra được YouTube, hoặc app khởi động lại giữa chừng | Chờ tối đa 10 phút (lệnh treo được chạy lại); `docker compose logs app \| grep -i sync` |
| Nút 🔊 Đọc to không hiện | Thiết bị không có giọng tiếng Việt | Thêm giọng tiếng Việt trong cài đặt thiết bị (C8) |
| Hết dung lượng đĩa | Bản sao lưu, log, image Docker cũ | `df -h`; `docker system prune` (xóa image không dùng); chép bản sao ra ngoài rồi xóa bớt |

Ứng dụng và database chạy theo giờ Việt Nam (`TZ=Asia/Ho_Chi_Minh`) để giới hạn giờ học và báo cáo tuần tính đúng ngày.
