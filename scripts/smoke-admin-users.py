"""Exercise user management and role enforcement against the built root app and real SQL.

Uses the existing smoke harness, disposable UUID accounts and private email pickup.
The bootstrap check runs InMemory. Build first. Removes only its generated data.
"""

import importlib.util
from pathlib import Path
import socket
import sys
import tempfile
import urllib.error
import urllib.parse
import urllib.request
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
INVITED = f"sql-smoke-invited-{RUN}@example.invalid"
MOVED = f"sql-smoke-moved-{RUN}@example.invalid"
PNG = bytes.fromhex("89504E470D0A1A0A") + b"\0" * 32


def free_url():
    with socket.socket() as listener:
        listener.bind(("127.0.0.1", 0))
        return f"http://127.0.0.1:{listener.getsockname()[1]}"


def register(client, directory, email, name):
    status, _, _ = client.post_form("/register", {
        "FullName": name, "Email": email, "Password": PASSWORD, "ConfirmPassword": PASSWORD, "Agree": "true"})
    check(status == 302, f"registers {name}")
    check(smoke.confirm(client, smoke.mail_link(directory, email, "/confirm-email"))[0] == 302, f"confirms {name}")


generated = []


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


def upload(client, page, path, name, content, content_type="image/png"):
    """Send one multipart file with the page's antiforgery token."""
    token = smoke.token_from(client.request(page)[2])
    boundary = uuid.uuid4().hex
    parts = (f'--{boundary}\r\nContent-Disposition: form-data; name="__RequestVerificationToken"\r\n\r\n{token}\r\n'
             f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="{name}"\r\n'
             f'Content-Type: {content_type}\r\n\r\n').encode() + content + f"\r\n--{boundary}--\r\n".encode()
    request = urllib.request.Request(client.url + path, data=parts,
                                     headers={"Content-Type": f"multipart/form-data; boundary={boundary}"})
    try:
        return client.opener.open(request, timeout=10).code
    except urllib.error.HTTPError as error:
        return error.code


def avatar_of(user):
    return sql(f"SELECT ISNULL(AvatarUrl, N'') FROM dbo.Users WHERE Id=N'{user}';")


def avatar_file(url):
    return smoke.ROOT / "wwwroot" / url.lstrip("/")


def crud_checks(admin, url, directory, admin_id):
    anonymous = smoke.Client(url)
    new = "/admin-users/new"
    check(anonymous.request(new)[0] == 302, "anonymous users cannot open the create form")
    values = {"FullName": "SQL smoke invited", "Email": INVITED, "Role": "Parent"}
    check(admin.request(new, values)[0] == 400, "creating an account requires CSRF")
    check(admin.post_form(new, {**values, "Email": ADMIN})[0] == 200 and sql(f"SELECT COUNT(*) FROM dbo.Users WHERE Email=N'{ADMIN}';") == "1", "duplicate email is refused")
    check(admin.post_form(new, {**values, "Role": "Moderator"})[0] == 200 and user_id(INVITED) == "", "unsupported role cannot be created")
    status, headers, _ = admin.post_form(new, values)
    invited_id = user_id(INVITED)
    check(status == 302 and invited_id in headers.get("Location", ""), "Admin creates an account")
    check(sql(f"SELECT COUNT(*) FROM dbo.Users WHERE Id=N'{invited_id}' AND PasswordHash IS NULL AND EmailConfirmed=0;") == "1", "invited account has no password yet")
    check(sql(f"SELECT r.Name FROM dbo.UserRoles ur JOIN dbo.Roles r ON r.Id=ur.RoleId WHERE ur.UserId=N'{invited_id}';") == "Parent", "invited account has the chosen role")
    check(audit_count(invited_id, "CreateUser") == "1", "account creation is audited")

    link = smoke.mail_link(directory, INVITED, "/confirm-email")
    invited = smoke.Client(url)
    check(invited.post_form("/login", {"Email": INVITED, "Password": PASSWORD})[0] == 200, "invited account cannot sign in before accepting")
    status, headers, _ = smoke.confirm(invited, link)
    location = headers.get("Location", "")
    check(status == 302 and location.startswith("/reset-password"), "accepting the invitation leads to choosing a password")
    query = dict(urllib.parse.parse_qsl(urllib.parse.urlsplit(location).query))
    status, _, _ = invited.post_form(location, {"Email": query["email"], "Token": query["token"], "Password": PASSWORD, "ConfirmPassword": PASSWORD})
    check(status == 302, "invited user sets a password")
    check(invited.post_form("/login", {"Email": INVITED, "Password": PASSWORD})[1].get("Location") == "/parent-home", "invited user signs in with that password")

    page, edit = f"/admin-users/{invited_id}", f"/admin-users/{invited_id}/edit"
    check(INVITED in admin.request(edit)[2], "edit form shows current values")
    check(admin.post_form(edit, {"FullName": "SQL smoke renamed", "Email": INVITED})[0] == 302
          and sql(f"SELECT FullName FROM dbo.Users WHERE Id=N'{invited_id}';") == "SQL smoke renamed", "Admin renames an account")
    check("SQL smoke renamed" in invited.request("/settings")[2], "rename reaches the live session")
    check(admin.post_form(edit, {"FullName": "SQL smoke renamed", "Email": PARENT})[0] == 200
          and sql(f"SELECT Email FROM dbo.Users WHERE Id=N'{invited_id}';") == INVITED, "edit refuses an address already in use")
    self_edit = f"/admin-users/{admin_id}/edit"
    check(admin.post_form(self_edit, {"FullName": "SQL smoke test", "Email": MOVED})[0] == 200
          and sql(f"SELECT Email FROM dbo.Users WHERE Id=N'{admin_id}';") == ADMIN, "Admin cannot change their own email here")

    png_url = None
    session = smoke.clone(invited)
    check(upload(admin, page, f"{page}/avatar", "a.png", PNG) == 302 and avatar_of(invited_id).endswith(".png"), "Admin uploads an avatar")
    png_url = avatar_of(invited_id)
    check(avatar_file(png_url).read_bytes() == PNG, "avatar bytes are stored")
    check(anonymous.opener.open(url + png_url, timeout=10).headers.get_content_type() == "image/png", "avatar is served as an image")
    check(png_url in session.request("/settings")[2], "avatar reaches the live session")
    check(upload(admin, page, f"{page}/avatar", "a.png", b"<script>alert(1)</script>") == 302 and avatar_of(invited_id) == png_url, "script disguised as PNG is refused")
    check(upload(admin, page, f"{page}/avatar", "a.svg", b"<svg xmlns='http://www.w3.org/2000/svg'/>", "image/svg+xml") == 302 and avatar_of(invited_id) == png_url, "SVG is refused")
    check(upload(admin, page, f"{page}/avatar", "a.png", PNG + b"\0" * (2 * 1024 * 1024)) == 302 and avatar_of(invited_id) == png_url, "oversized avatar is refused")
    check(upload(invited, "/settings", "/settings/avatar", "b.png", PNG) == 302 and avatar_of(invited_id) not in ("", png_url), "Parent replaces their own avatar")
    check(not avatar_file(png_url).exists(), "replaced avatar file is removed")
    check(upload(admin, "/admin-users", "/settings/avatar", "c.png", PNG) == 302 and avatar_of(admin_id) == "", "Admin cannot use the Parent avatar route")
    check(post(invited, "/settings", "/settings/avatar/remove")[0] == 302 and avatar_of(invited_id) == "", "Parent removes their own avatar")
    check(upload(admin, page, f"{page}/avatar", "d.png", PNG) == 302, "Admin uploads an avatar again")
    png_url = avatar_of(invited_id)
    check(post(admin, page, f"{page}/avatar/remove")[0] == 302 and avatar_of(invited_id) == "" and not avatar_file(png_url).exists(), "Admin removes an avatar")

    check(admin.post_form(edit, {"FullName": "SQL smoke renamed", "Email": MOVED})[0] == 302
          and sql(f"SELECT COUNT(*) FROM dbo.Users WHERE Id=N'{invited_id}' AND Email=N'{MOVED}' AND UserName=N'{MOVED}' AND EmailConfirmed=0;") == "1", "Admin changes an email and it needs confirming")
    check(invited.request("/settings")[0] == 302, "email change ends existing sessions")
    check(smoke.Client(url).post_form("/login", {"Email": INVITED, "Password": PASSWORD})[0] == 200, "old email can no longer sign in")
    moved = smoke.Client(url)
    check(smoke.confirm(moved, smoke.mail_link(directory, MOVED, "/confirm-email"))[1].get("Location") == "/login", "new address confirms without re-setting the password")
    check(moved.post_form("/login", {"Email": MOVED, "Password": PASSWORD})[1].get("Location") == "/parent-home", "new email signs in")

    delete = f"{page}/delete"
    check(admin.request(delete, {})[0] == 400, "deleting requires CSRF")
    check(moved.request(delete, {})[0] in (302, 400) and user_id(MOVED) == invited_id, "Parent cannot delete accounts")
    self_page = f"/admin-users/{admin_id}"
    check(post(admin, self_page, f"{self_page}/delete")[0] == 302 and user_id(ADMIN) == admin_id, "Admin cannot delete themselves")
    sql(f"INSERT dbo.ChildProfiles(ParentId, DisplayName, GradeLevelId) SELECT TOP 1 N'{invited_id}', N'Smoke', Id FROM dbo.GradeLevels;")
    check(post(admin, page, delete)[0] == 302 and user_id(MOVED) == invited_id, "account with a child profile is not deleted")
    sql(f"DELETE FROM dbo.ChildProfiles WHERE ParentId=N'{invited_id}';")
    sql(f"INSERT dbo.ContentSources(Name, ExternalId, Url, SuggestedById) VALUES (N'Smoke', N'{RUN[:32]}', N'https://example.invalid', N'{invited_id}');")
    check(post(admin, page, delete)[0] == 302 and user_id(MOVED) == invited_id
          and "không xóa được" in admin.request(page)[2], "account referenced by shared records is not deleted")
    sql(f"DELETE FROM dbo.ContentSources WHERE ExternalId=N'{RUN[:32]}';")
    check(upload(moved, "/settings", "/settings/avatar", "e.png", PNG) == 302, "avatar is set before deletion")
    png_url = avatar_of(invited_id)
    check(post(admin, page, delete)[0] == 302 and user_id(MOVED) == "", "Admin deletes an account")
    check(not avatar_file(png_url).exists(), "deletion removes the avatar file")
    check(moved.request("/settings")[0] == 302, "deletion ends existing sessions")
    check(admin.request(page)[0] == 404, "deleted account returns 404")
    # The attempt on the old address after the change never belonged to this account.
    check(sql(f"SELECT COUNT(*) FROM dbo.LoginHistories WHERE UserNameTried=N'{MOVED}';") == "0"
          and sql(f"SELECT COUNT(*) FROM dbo.LoginHistories WHERE UserNameTried=N'{INVITED}';") == "1", "deletion removes the account's login history")
    check(audit_count(invited_id, "DeleteUser") == "1", "deletion is audited")
    return invited_id


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
        generated.append(crud_checks(admin, url, directory, admin_id))


def bootstrap_checks(url, directory):
    email, password = f"bootstrap-{RUN}@example.invalid", f"Bootstrap1{RUN}"
    overrides = {"Demo__UseInMemoryDatabase": "true", "Bootstrap__AdminEmail": email, "Bootstrap__AdminPassword": password}
    with smoke.running_app(url, directory, overrides) as client:
        status, headers, _ = client.post_form("/login", {"Email": email, "Password": password})
        check(status == 302 and headers.get("Location") == "/admin-home", "bootstrap creates a working first Admin")
        status, _, body = client.request("/admin-users")
        check(status == 200 and email in table(body), "user management renders without SQL business tables")
        created = f"created-{RUN}@example.invalid"
        status, headers, _ = client.post_form("/admin-users/new", {"FullName": "Memory user", "Email": created, "Role": "Parent"})
        check(status == 302, "create works without SQL business tables")
        page = headers["Location"]
        check(post(client, page, f"{page}/delete")[0] == 302 and created not in table(client.request("/admin-users")[2]), "delete works without SQL business tables")


def main():
    try:
        with tempfile.TemporaryDirectory(prefix="safelearn-private-mail-") as temporary:
            directory = Path(temporary)
            sql_checks(free_url(), directory)
            bootstrap_checks(free_url(), directory)
    finally:
        emails = ",".join(f"N'{item}'" for item in (ADMIN, PARENT, INVITED, MOVED))
        ids = f"SELECT Id FROM dbo.Users WHERE Email IN ({emails})"
        for url in sql(f"SELECT AvatarUrl FROM dbo.Users WHERE Email IN ({emails}) AND AvatarUrl IS NOT NULL;").split():
            avatar_file(url).unlink(missing_ok=True)
        sql(f"DELETE FROM dbo.ChildProfiles WHERE ParentId IN ({ids});")
        sql(f"DELETE FROM dbo.ContentSources WHERE SuggestedById IN ({ids});")
        sql(f"DELETE FROM dbo.AuditLogs WHERE EntityId IN ({ids}) OR ActorId IN ({ids});")
        for item in generated:
            sql(f"DELETE FROM dbo.AuditLogs WHERE EntityId=N'{item}';")
        sql(f"DELETE FROM dbo.LoginHistories WHERE UserNameTried IN ({emails});")
        sql(f"DELETE FROM dbo.Users WHERE Email IN ({emails});")
        check(sql(f"SELECT COUNT(*) FROM dbo.Users WHERE Email IN ({emails});") == "0", "generated accounts are cleaned up")
    print(f"RESULT {smoke.checks}/{smoke.checks} passed (user management + roles + bootstrap)")


if __name__ == "__main__":
    main()
