"""Exercise user management and role enforcement against the built root app and real SQL.

Uses the existing smoke harness, disposable UUID accounts and private email pickup.
The bootstrap check runs InMemory. Build first. Removes only its generated data.
"""

import importlib.util
from pathlib import Path
import socket
import sys
import tempfile
import uuid


sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location("auth_smoke", Path(__file__).with_name("smoke-sql-auth.py"))
smoke = importlib.util.module_from_spec(spec)
spec.loader.exec_module(smoke)
check, sql = smoke.check, smoke.sql

RUN = uuid.uuid4().hex
ADMIN = f"sql-smoke-admin-{RUN}@example.invalid"
PARENT = f"sql-smoke-parent-{RUN}@example.invalid"
PASSWORD = f"AdminTest1{RUN}"
FORMULA_NAME = "=HYPERLINK(1) smoke"


def free_url():
    with socket.socket() as listener:
        listener.bind(("127.0.0.1", 0))
        return f"http://127.0.0.1:{listener.getsockname()[1]}"


def register(client, directory, email, name):
    status, _, _ = client.post_form("/register", {
        "FullName": name, "Email": email, "Password": PASSWORD, "ConfirmPassword": PASSWORD, "Agree": "true"})
    check(status == 302, f"registers {name}")
    check(smoke.confirm(client, smoke.mail_link(directory, email, "/confirm-email"))[0] == 302, f"confirms {name}")


def table(body):
    """Every page names the signed-in admin, so compare table rows only."""
    return body[body.index("<tbody"):body.index("</tbody>")]


def user_id(email):
    return sql(f"SELECT Id FROM dbo.Users WHERE Email=N'{email}';")


def post(client, page, path, values=None):
    """Submit a form from the details page, as the browser does."""
    return client.post_form(path, values or {}, get_path=page)


def audit_count(target, action):
    return sql(f"SELECT COUNT(*) FROM dbo.AuditLogs WHERE EntityId=N'{target}' AND Action=N'{action}';")


def sql_checks(url, directory):
    with smoke.running_app(url, directory) as anonymous:
        admin, parent = smoke.Client(url), smoke.Client(url)
        register(anonymous, directory, ADMIN, "SQL smoke test")
        register(parent, directory, PARENT, FORMULA_NAME)
        admin_id, parent_id = user_id(ADMIN), user_id(PARENT)
        sql(f"DELETE ur FROM dbo.UserRoles ur WHERE ur.UserId=N'{admin_id}';")
        sql(f"INSERT dbo.UserRoles(UserId, RoleId) SELECT N'{admin_id}', Id FROM dbo.Roles WHERE NormalizedName=N'ADMIN';")
        check(sql(f"SELECT COUNT(*) FROM dbo.UserConsents WHERE UserId=N'{parent_id}' AND ConsentType='Terms' AND Decision='Accepted';") == "1",
              "registration stores accepted terms")

        check(anonymous.request("/admin-users")[0] == 302 and anonymous.request(f"/admin-users/{parent_id}")[0] == 302
              and anonymous.request("/admin-users/export")[0] == 302, "anonymous users are sent to login")
        status, headers, _ = parent.post_form("/login", {"Email": PARENT, "Password": PASSWORD})
        check(status == 302 and headers.get("Location") == "/parent-home", "Parent lands on parent home")
        check(sql(f"SELECT COUNT(*) FROM dbo.Users WHERE Id=N'{parent_id}' AND LastLoginAt IS NOT NULL;") == "1", "login updates LastLoginAt")
        check(sql(f"SELECT COUNT(*) FROM dbo.LoginHistories WHERE UserId=N'{parent_id}' AND Succeeded=1;") == "1", "login is recorded")
        for path in ("/admin-users", f"/admin-users/{admin_id}", "/admin-users/export", "/admin-home"):
            status, headers, _ = parent.request(path)
            check(status == 302 and "/error/403" in headers.get("Location", ""), f"Parent is denied {path}")
        status, _, body = parent.request("/error/403")
        check(status == 403 and "không có quyền" in body, "access denied has its own page")
        check(parent.request(f"/admin-users/{admin_id}/lock", {})[0] in (302, 400), "Parent cannot post admin actions")
        check(sql(f"SELECT IsActive FROM dbo.Users WHERE Id=N'{admin_id}';") == "1", "Parent post did not lock anyone")

        status, headers, _ = admin.post_form("/login", {"Email": ADMIN, "Password": PASSWORD})
        check(status == 302 and headers.get("Location") == "/admin-home", "Admin lands on admin home")
        check(admin.request("/parent-home")[0] == 302, "Admin cannot open parent pages")
        page = f"/admin-users/{parent_id}"

        status, _, body = admin.request("/admin-users?q=" + PARENT)
        check(status == 200 and PARENT in table(body) and ADMIN not in table(body), "search finds only the matching account")
        check(PARENT in table(admin.request("/admin-users?role=Parent&q=sql-smoke")[2])
              and PARENT not in table(admin.request("/admin-users?role=Admin&q=sql-smoke")[2]), "role filter works")
        check(PARENT not in table(admin.request("/admin-users?status=locked&q=sql-smoke")[2]), "status filter works")
        check(admin.request("/admin-users?page=99999&q=sql-smoke")[0] == 200, "out-of-range page is clamped")
        status, headers, body = admin.request("/admin-users/export?q=" + PARENT)
        check(status == 200 and "text/csv" in headers.get("Content-Type", "") and PARENT in body and ADMIN not in body, "export honors filters")
        check('"\'=HYPERLINK' in body, "export neutralizes spreadsheet formulas")
        check(admin.request("/admin-users/not-a-user")[0] == 404, "unknown account returns 404")
        check(admin.request(f"/admin-users/{parent_id}/lock", {})[0] == 400, "admin actions require CSRF")

        check(post(admin, page, f"{page}/role", {"role": "Moderator"})[0] == 302 and PARENT in sql(f"SELECT Email FROM dbo.Users WHERE Id=N'{parent_id}';")
              and sql(f"SELECT r.Name FROM dbo.UserRoles ur JOIN dbo.Roles r ON r.Id=ur.RoleId WHERE ur.UserId=N'{parent_id}';") == "Parent",
              "unsupported role is refused")
        self_page = f"/admin-users/{admin_id}"
        token = smoke.token_from(admin.request(page)[2])
        admin.request(f"{self_page}/lock", {"__RequestVerificationToken": token})
        admin.request(f"{self_page}/role", {"role": "Parent", "__RequestVerificationToken": token})
        check(sql(f"SELECT IsActive FROM dbo.Users WHERE Id=N'{admin_id}';") == "1"
              and sql(f"SELECT r.Name FROM dbo.UserRoles ur JOIN dbo.Roles r ON r.Id=ur.RoleId WHERE ur.UserId=N'{admin_id}';") == "Admin",
              "Admin cannot lock or demote themselves")

        session = smoke.clone(parent)
        check(session.request("/settings")[0] == 200, "Parent session is live before the lock")
        check(post(admin, page, f"{page}/lock")[0] == 302 and sql(f"SELECT IsActive FROM dbo.Users WHERE Id=N'{parent_id}';") == "0", "Admin locks an account")
        check(session.request("/settings")[0] == 302, "lock ends the existing session")
        check(smoke.Client(url).post_form("/login", {"Email": PARENT, "Password": PASSWORD})[0] == 200, "locked account cannot sign in")
        check(sql(f"SELECT COUNT(*) FROM dbo.LoginHistories WHERE UserId=N'{parent_id}' AND Succeeded=0 AND FailReason=N'Disabled';") == "1", "blocked login records its reason")
        check(PARENT in table(admin.request("/admin-users?status=locked&q=sql-smoke")[2]), "locked account appears in the locked filter")
        check(audit_count(parent_id, "LockUser") == "1", "lock is audited")

        check(post(admin, page, f"{page}/unlock")[0] == 302 and sql(f"SELECT IsActive FROM dbo.Users WHERE Id=N'{parent_id}';") == "1", "Admin unlocks an account")
        check(audit_count(parent_id, "UnlockUser") == "1", "unlock is audited")
        relogin = smoke.Client(url)
        check(relogin.post_form("/login", {"Email": PARENT, "Password": PASSWORD})[1].get("Location") == "/parent-home", "unlocked account signs in again")

        check(post(admin, page, f"{page}/role", {"role": "Admin"})[0] == 302
              and sql(f"SELECT r.Name FROM dbo.UserRoles ur JOIN dbo.Roles r ON r.Id=ur.RoleId WHERE ur.UserId=N'{parent_id}';") == "Admin",
              "Admin promotes an account")
        check(relogin.request("/settings")[0] == 302 and audit_count(parent_id, "ChangeRole") == "1", "role change ends old sessions and is audited")
        check(post(admin, page, f"{page}/role", {"role": "Parent"})[0] == 302
              and sql(f"SELECT r.Name FROM dbo.UserRoles ur JOIN dbo.Roles r ON r.Id=ur.RoleId WHERE ur.UserId=N'{parent_id}';") == "Parent",
              "Admin demotes an account")

        count = len(list(directory.glob("*.eml")))
        check(post(admin, page, f"{page}/send-reset")[0] == 302, "Admin requests a password reset email")
        smoke.mail_link(directory, PARENT, "/reset-password", count)
        check(audit_count(parent_id, "SendPasswordReset") == "1", "reset email is audited")
        _, _, body = admin.request(page)
        check("Khóa tài khoản" in body and "Lịch sử đăng nhập" in body and "Thành công" in body, "details page shows history and actions")
        check(PASSWORD not in body and "PasswordHash" not in body, "details page exposes no secrets")


def bootstrap_checks(url, directory):
    email, password = f"bootstrap-{RUN}@example.invalid", f"Bootstrap1{RUN}"
    overrides = {"Demo__UseInMemoryDatabase": "true", "Bootstrap__AdminEmail": email, "Bootstrap__AdminPassword": password}
    with smoke.running_app(url, directory, overrides) as client:
        status, headers, _ = client.post_form("/login", {"Email": email, "Password": password})
        check(status == 302 and headers.get("Location") == "/admin-home", "bootstrap creates a working first Admin")
        status, _, body = client.request("/admin-users")
        check(status == 200 and email in table(body), "user management renders without SQL business tables")


def main():
    try:
        with tempfile.TemporaryDirectory(prefix="safelearn-private-mail-") as temporary:
            directory = Path(temporary)
            sql_checks(free_url(), directory)
            bootstrap_checks(free_url(), directory)
    finally:
        ids = f"SELECT Id FROM dbo.Users WHERE Email IN (N'{ADMIN}',N'{PARENT}')"
        sql(f"DELETE FROM dbo.AuditLogs WHERE EntityId IN ({ids}) OR ActorId IN ({ids});")
        sql(f"DELETE FROM dbo.LoginHistories WHERE UserNameTried IN (N'{ADMIN}',N'{PARENT}');")
        sql(f"DELETE FROM dbo.Users WHERE Email IN (N'{ADMIN}',N'{PARENT}');")
        check(sql(f"SELECT COUNT(*) FROM dbo.Users WHERE Email IN (N'{ADMIN}',N'{PARENT}');") == "0", "generated accounts are cleaned up")
    print(f"RESULT {smoke.checks}/{smoke.checks} passed (user management + roles + bootstrap)")


if __name__ == "__main__":
    main()
