# SafeLearn Kids — Frontend (ASP.NET Core MVC, .NET 10)

Hệ thống học tập an toàn cho trẻ em. Đây là bộ **frontend hoàn chỉnh** (Razor Views + CSS/JS + Controller khung + Identity) theo Tech Stack của nhóm:
C# · ASP.NET Core MVC (.NET 10 LTS) · SQL Server · EF Core · Razor · Bootstrap 5 · ASP.NET Core Identity.

## Chạy nhanh (chế độ demo, không cần SQL Server)
```bash
cd SafeLearnKids.Web
dotnet tool install -g Microsoft.Web.LibraryManager.Cli   # lần đầu
libman restore                                            # tải Bootstrap 5.3.3 + jQuery vào wwwroot/lib
dotnet run
```
Mặc định `Demo:UseInMemoryDatabase = true` (appsettings.json) → dùng EF Core InMemory, tự seed tài khoản:

| Vai trò | Email | Mật khẩu |
|---|---|---|
| Phụ huynh | ha.nguyen@gmail.com | matkhau123 |
| Quản trị | admin@safelearnkids.vn | admin12345 |

Dữ liệu nghiệp vụ demo (bé, bài học, quiz, thời gian học…) hiện nằm ở `wwwroot/js/store.js` (localStorage `kl.v1`) — xem `docs/FE-HANDOFF.md` mục 5 để thay bằng EF Core/API.

## Chuyển sang SQL Server
1. Sửa `ConnectionStrings:Default` và đặt `Demo:UseInMemoryDatabase = false` trong `appsettings.json`.
2. `dotnet ef migrations add Init && dotnet ef database update`.

## Cấu trúc
- `Areas/Parent|Kids|Admin` — controller theo khu vực, URL gọn ở gốc (`/parent-home`, `/library`, `/kids-home`, `/admin-home`…).
- `Views/Shared` — `_Layout` → `_ParentLayout` / `_AdminLayout` / `_KidsLayout`, sidebar, top bar.
- `Controllers/AccountController` — đăng nhập, đăng ký, đăng xuất (POST), quên/đặt lại mật khẩu (Identity, khóa 5 lần/5 phút).
- `wwwroot/css/theme.css` (design tokens sáng/tối/tự động, theme trẻ em) và `app.css`.
- `wwwroot/js` — `store.js`, `ui.js`, `parent.js`, `kids.js`, `admin.js`, `auth.js`.
- `../prototype-static` — bản tĩnh chạy thẳng bằng trình duyệt (mở `index.html`), dùng để demo nhanh/đối chiếu.
