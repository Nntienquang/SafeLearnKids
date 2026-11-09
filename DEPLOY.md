# Đưa SafeLearn Kids lên Internet (Docker)

Một máy chủ Linux chạy 3 container:
- **SQL Server 2022 Express**: miễn phí, giới hạn 10 GB dữ liệu.
- **Web app**.
- **Caddy**: tự lấy và gia hạn chứng chỉ HTTPS Let's Encrypt.

Chỉ Caddy mở cổng ra ngoài (80/443). Database và app chỉ nói chuyện với nhau trong mạng nội bộ của Docker.

## 1. Chuẩn bị

| Cần | Gợi ý |
|---|---|
| VPS **x86_64** (SQL Server không chạy trên ARM), Ubuntu 22.04/24.04, **≥ 2 vCPU, 4 GB RAM**, 40 GB SSD | DigitalOcean / Vultr / Linode khu vực Singapore, hoặc VPS trong nước (Vietnix, AZDIGI, BKNS...). Sinh viên: GitHub Student Developer Pack thường có credit DigitalOcean và Azure for Students. |
| Tên miền | Namecheap, Porkbun, Cloudflare; tên miền `.vn` mua ở nhà đăng ký trong nước (cần giấy tờ). |
| Dịch vụ gửi mail (SMTP) | Brevo (gói miễn phí ~300 mail/ngày), Zoho Mail, Mailgun. Cần xác minh tên miền (SPF, DKIM) để mail không vào spam. |

## 2. Trỏ tên miền về máy chủ

Ở trang quản lý DNS của tên miền, tạo:
- bản ghi `A`: `@` → IP của VPS;
- bản ghi `A` hoặc `CNAME`: `www` → IP đó (hoặc `@`).

Đợi vài phút rồi kiểm tra bằng `ping tenmien.com`: phải ra đúng IP.

## 3. Cài Docker trên VPS

```bash
ssh root@<IP>
apt update && apt -y upgrade
curl -fsSL https://get.docker.com | sh
ufw allow OpenSSH && ufw allow 80 && ufw allow 443 && ufw --force enable
```

## 4. Lấy code và cấu hình

```bash
git clone https://github.com/Nntienquang/SafeLearnKids.git /opt/safelearnkids
cd /opt/safelearnkids
cp .env.example .env
nano .env
```

Điền các giá trị trong `.env`:
- **`DOMAIN`**: tên miền, không có `https://`.
- **`MSSQL_SA_PASSWORD`**, **`APP_DB_PASSWORD`**: mật khẩu mạnh, không dùng các ký tự `' ; " $` và khoảng trắng.
- **`SMTP_*`**, **`MAIL_FROM`**: thông tin từ dịch vụ mail. `MAIL_FROM` phải thuộc tên miền đã xác minh.
- **`ADMIN_EMAIL`**, **`ADMIN_PASSWORD`**: tài khoản quản trị đầu tiên, được tạo khi app khởi động.
- **`YOUTUBE_API_KEY`**, **`AI_API_KEY`**: không bắt buộc.

File `.env` chứa mật khẩu và **không bao giờ đưa lên Git** (đã có trong `.gitignore`).

## 5. Chạy

```bash
docker compose up -d --build
docker compose logs -f db-init app
```

Lần đầu mất vài phút: tải image, build app, rồi `db-init` tạo database từ `database/01-schema.sql` và `database/02-reference-data.sql`. Kiểm tra:
- `https://tenmien.com/healthz` trả `ok`;
- mở `https://tenmien.com`, đăng nhập bằng `ADMIN_EMAIL`;
- sau khi đăng nhập được, **xóa `ADMIN_EMAIL` và `ADMIN_PASSWORD` khỏi `.env`** rồi chạy lại `docker compose up -d`.

Thêm nội dung trong trang quản trị: môn học đã có sẵn; admin thêm bài học, nguồn YouTube và câu hỏi.

## 6. Cập nhật phiên bản mới

```bash
cd /opt/safelearnkids && git pull && docker compose up -d --build
```

Database, file tải lên và khóa đăng nhập nằm trong Docker volume nên không mất khi cập nhật. App tự thêm cột hoặc bảng mới (`Services/SchemaUpdater.cs`).

## 7. Sao lưu

```bash
chmod +x deploy/backup.sh
./deploy/backup.sh
crontab -e
```

Trong `crontab -e`, thêm dòng sau để chạy mỗi ngày lúc 2 giờ:

```
0 2 * * * cd /opt/safelearnkids && ./deploy/backup.sh >> backups/backup.log 2>&1
```

Mỗi lần chạy tạo 3 file trong `backups/`, giữ trong 14 ngày:
- `hoctapchotre-*.bak`: database;
- `uploads-*.tgz`: ảnh và PDF người dùng tải lên;
- `appdata-*.tgz`: khóa đăng nhập.

Nên chép định kỳ thư mục `backups/` ra ngoài máy chủ (Google Drive, máy cá nhân...).

## 8. (Tùy chọn) Mang dữ liệu đang có trên máy lên

Một bản cài mới chỉ có môn học, lớp và huy hiệu. Nếu muốn giữ bài học và video đã soạn ở máy dev:
1. Trên máy dev:

   ```
   sqlcmd -S . -E -C -Q "BACKUP DATABASE hoctapchotre TO DISK=N'C:\Temp\hoctapchotre.bak' WITH INIT"
   ```

2. Chép file lên VPS: `scp C:\Temp\hoctapchotre.bak root@<IP>:/opt/safelearnkids/`.
3. Trên VPS, khi `app` đang tắt (`docker compose stop app`):

   ```bash
   docker compose cp hoctapchotre.bak db:/var/opt/mssql/restore.bak
   docker compose exec db /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P '<MSSQL_SA_PASSWORD>' -C -Q "RESTORE FILELISTONLY FROM DISK=N'/var/opt/mssql/restore.bak'"
   ```

   Lệnh trên in ra tên logic của file data và log. Dùng hai tên đó trong lệnh dưới:

   ```bash
   docker compose exec db /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P '<MSSQL_SA_PASSWORD>' -C -Q "RESTORE DATABASE hoctapchotre FROM DISK=N'/var/opt/mssql/restore.bak' WITH REPLACE, MOVE N'<data>' TO N'/var/opt/mssql/data/hoctapchotre.mdf', MOVE N'<log>' TO N'/var/opt/mssql/data/hoctapchotre_log.ldf'"
   docker compose up -d
   ```

   Lệnh `docker compose up -d` chạy lại `db-init`, bước này tạo lại tài khoản SQL của app.
4. Database dev có tài khoản và dữ liệu thử. Trước khi public, xóa các tài khoản test trong `/admin-users`.

## Sự cố thường gặp

| Hiện tượng | Cách xử lý |
|---|---|
| `db` khởi động lại liên tục | VPS thiếu RAM (cần ≥ 2 GB trống), hoặc `MSSQL_SA_PASSWORD` quá yếu. Xem bằng `docker compose logs db`. |
| Caddy không lấy được chứng chỉ | DNS chưa trỏ đúng IP, hoặc cổng 80/443 bị chặn. Xem bằng `docker compose logs caddy`. |
| Không nhận được mail xác minh | Sai thông tin SMTP, hoặc tên miền chưa xác minh SPF/DKIM. Xem bằng `docker compose logs app`. |
| Trang báo lỗi 500 ngay khi mở form | Truy cập trực tiếp cổng app thay vì qua Caddy. Chỉ dùng `https://tenmien.com`. |

Ứng dụng chạy theo giờ Việt Nam (`TZ=Asia/Ho_Chi_Minh`) để giới hạn giờ học và báo cáo tuần tính đúng ngày.
