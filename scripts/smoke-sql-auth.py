"""Exercise real MVC/Identity/SQL persistence; removes only its generated test user.

Run from the repository root after building and applying ensure-identity-schema.sql.
Requires Python 3, dotnet and sqlcmd, with Windows access to the configured SQL server.
"""

import contextlib
import copy
from concurrent.futures import ThreadPoolExecutor
from email import policy
from email.parser import BytesParser
import http.cookiejar
import json
import os
import re
from pathlib import Path
import socket
import subprocess
import tempfile
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid
from html.parser import HTMLParser
from html import unescape


ROOT = Path(__file__).resolve().parent.parent
CONFIG = json.loads((ROOT / "appsettings.json").read_text(encoding="utf-8-sig"))
CONNECTION_STRING = os.environ.get("ConnectionStrings__DefaultConnection") or CONFIG["ConnectionStrings"]["DefaultConnection"]
CONNECTION = dict(part.split("=", 1) for part in CONNECTION_STRING.split(";") if part)
SERVER, DATABASE = CONNECTION["Server"], CONNECTION["Database"]
EMAIL = f"sql-smoke-{uuid.uuid4().hex}@example.invalid"
PASSWORD = f"SqlTest1{uuid.uuid4().hex}"
checks = 0


def check(condition, label):
    global checks
    if not condition:
        raise AssertionError(label)
    checks += 1
    print(f"PASS {label}", flush=True)


def sql(query):
    result = subprocess.run(
        ["sqlcmd", "-S", SERVER, "-d", DATABASE, "-E", "-C", "-I", "-b", "-l", "5", "-h", "-1", "-W",
         "-Q", "SET NOCOUNT ON; " + query], capture_output=True, text=True, check=True, timeout=20)
    return result.stdout.strip()


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


class TokenParser(HTMLParser):
    token = None

    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs)
        if tag == "input" and attrs.get("name") == "__RequestVerificationToken":
            self.token = attrs["value"]


class Client:
    def __init__(self, url):
        self.url = url
        self.cookies = http.cookiejar.CookieJar()
        self.opener = urllib.request.build_opener(
            urllib.request.ProxyHandler({}), urllib.request.HTTPCookieProcessor(self.cookies), NoRedirect())

    def request(self, path, data=None, headers=None, raw=False):
        data = None if data is None else urllib.parse.urlencode(data).encode()
        try:
            request = urllib.request.Request(self.url + path, data=data, headers=headers or {})
            response = self.opener.open(request, timeout=10)
        except urllib.error.HTTPError as error:
            response = error
        with response:
            body = response.read().decode("utf-8")
            return response.code, response.headers, body if raw else unescape(body)

    def post_form(self, path, values, get_path=None):
        status, _, body = self.request(get_path or path)
        if status != 200:
            raise AssertionError(f"GET {path}: expected 200, got {status}")
        parser = TokenParser()
        parser.feed(body)
        if not parser.token:
            raise AssertionError(f"GET {path}: missing antiforgery token")
        return self.request(path, {**values, "__RequestVerificationToken": parser.token})


@contextlib.contextmanager
def running_app(url, mail_directory, overrides=None, logs=None):
    with tempfile.TemporaryFile(mode="w+b") as log:
        env = {**os.environ, "ASPNETCORE_ENVIRONMENT": "Development",
               "Email__DeliveryMethod": "Pickup", "Email__FromAddress": "no-reply@example.invalid",
               "Email__PublicBaseUrl": url, "Email__PickupDirectory": str(mail_directory),
               "Auth__RateLimitPermitLimit": "200", **(overrides or {})}
        process = subprocess.Popen(
            ["dotnet", "run", "--project", "SafeLearnKids.Web.csproj", "--no-build", "--no-launch-profile", "--urls", url],
            cwd=ROOT, env=env, stdout=log, stderr=subprocess.STDOUT,
            creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0)
        try:
            client = Client(url)
            deadline = time.monotonic() + 45
            while True:
                if process.poll() is not None:
                    raise RuntimeError("Application exited before accepting HTTP requests.")
                try:
                    if client.request("/login")[0] == 200:
                        break
                except (OSError, urllib.error.URLError):
                    pass
                if time.monotonic() >= deadline:
                    raise TimeoutError("Application did not start within 45 seconds.")
                time.sleep(0.25)
            yield client
        finally:
            if process.poll() is None:
                if os.name == "nt":
                    subprocess.run(["taskkill", "/PID", str(process.pid), "/T", "/F"], capture_output=True, check=True)
                else:
                    process.terminate()
                process.wait(timeout=15)
            log.seek(0)
            output = log.read().decode("utf-8", errors="replace")
            if logs is not None:
                logs.append(output)


def clone(client):
    other = Client(client.url)
    for cookie in client.cookies:
        other.cookies.set_cookie(copy.copy(cookie))
    return other


def token_from(body):
    parser = TokenParser()
    parser.feed(body)
    if not parser.token:
        raise AssertionError("Antiforgery token missing")
    return parser.token


def mail_link(directory, address, route, after_count=None):
    deadline = time.monotonic() + 8
    while True:
        files = sorted(directory.glob("*.eml"), key=lambda path: path.stat().st_mtime_ns, reverse=True)
        if after_count is None or len(files) > after_count:
            for path in files:
                message = BytesParser(policy=policy.default).parsebytes(path.read_bytes())
                if address not in str(message["To"]):
                    continue
                body = message.get_body(preferencelist=("plain",)) if message.is_multipart() else message
                for url in re.findall(r"https?://[^\s]+", body.get_content()):
                    parsed = urllib.parse.urlsplit(url)
                    if parsed.path == route:
                        return url, {key: values[0] for key, values in urllib.parse.parse_qs(parsed.query).items()}
        if time.monotonic() >= deadline:
            raise AssertionError(f"Private email for {route} was not delivered")
        time.sleep(0.05)


def confirm(client, link):
    _, values = link
    return client.post_form("/confirm-email?" + urllib.parse.urlencode(values),
                            {"UserId": values["userId"], "Code": values["code"]})


def main():
    check(not CONFIG["Demo"]["UseInMemoryDatabase"] and not CONFIG["Demo"]["SeedDemoAccounts"], "SQL default and no known demo users")
    check(DATABASE == "hoctapchotre", "target database is hoctapchotre")
    check(sql("SELECT COUNT(*) FROM sys.tables WHERE schema_id=SCHEMA_ID(N'dbo') AND name IN "
              "(N'Users',N'Roles',N'UserRoles',N'UserClaims',N'RoleClaims',N'UserLogins',N'UserTokens');") == "7", "all Identity tables exist")
    with socket.socket() as listener:
        listener.bind(("127.0.0.1", 0))
        url = f"http://127.0.0.1:{listener.getsockname()[1]}"
    count_query = f"SELECT COUNT(*) FROM dbo.Users WHERE Email=N'{EMAIL}';"
    other_email = EMAIL.replace("sql-smoke-", "sql-smoke-other-")
    race_email = EMAIL.replace("sql-smoke-", "sql-smoke-race-")
    missing_email = EMAIL.replace("sql-smoke-", "sql-smoke-missing-")
    user = {"FullName": "SQL smoke test", "Email": EMAIL, "Password": PASSWORD, "ConfirmPassword": PASSWORD, "Agree": "true"}
    reset_password, changed_password = "Reset1" + uuid.uuid4().hex, "Changed1" + uuid.uuid4().hex
    logs, secrets = [], [PASSWORD, reset_password, changed_password]
    try:
        with tempfile.TemporaryDirectory(prefix="safelearn-private-mail-") as temporary:
            directory = Path(temporary)
            with running_app(url, directory, logs=logs) as client:
                for asset in ("/css/auth.css", "/js/auth.js"):
                    check(client.request(asset)[0] == 200, f"asset {asset}")
                status, headers, _ = client.request("/register")
                check("no-store" in headers.get("Cache-Control", "") and headers.get("Referrer-Policy") == "no-referrer", "account pages are private and suppress referrers")
                check(headers.get("X-Content-Type-Options") == "nosniff" and "frame-ancestors 'none'" in headers.get("Content-Security-Policy", ""), "security response headers")
                check(client.request("/settings")[0] == 302 and client.request("/change-password")[0] == 302, "anonymous account pages require login")
                for path in ("/register", "/login", "/recover", "/resend-confirmation", "/confirm-email", "/reset-password"):
                    check(client.request(path, user)[0] == 400, f"CSRF protects {path}")
                check(sql(count_query) == "0", "missing CSRF cannot insert a user")
                check(client.post_form("/register", {**user, "Agree": "false"})[0] == 200 and sql(count_query) == "0", "consent required on server")
                check(client.post_form("/register", {**user, "Password": "abcdefgh", "ConfirmPassword": "abcdefgh"})[0] == 200 and sql(count_query) == "0", "password policy required on server")
                forged = {**user, "Role": "Admin", "IsActive": "true", "EmailConfirmed": "true"}
                status, headers, _ = client.post_form("/register", forged)
                check(status == 302 and headers.get("Location") == "/check-email", "registration redirects to email verification")
                check(sql(count_query) == "1" and sql(f"SELECT COUNT(*) FROM dbo.Users WHERE Email=N'{EMAIL}' AND EmailConfirmed=0 AND IsActive=1 AND LEN(PasswordHash)>40;") == "1", "one pending user and password hash persist in SQL")
                check(sql(f"SELECT COUNT(*) FROM dbo.UserRoles ur JOIN dbo.Users u ON u.Id=ur.UserId JOIN dbo.Roles r ON r.Id=ur.RoleId WHERE u.Email=N'{EMAIL}' AND r.NormalizedName=N'PARENT';") == "1", "server assigns Parent despite forged role fields")
                check(client.request("/settings")[0] == 302 and client.post_form("/login", {"Email": EMAIL, "Password": PASSWORD})[0] == 200, "unconfirmed account cannot sign in")
                link = mail_link(directory, EMAIL, "/confirm-email")
                secrets.append(link[1]["code"])
                check(link[0].startswith(url + "/confirm-email?"), "confirmation uses the configured origin")
                before = len(list(directory.glob("*.eml")))
                check(client.post_form("/resend-confirmation", {"Email": EMAIL}, "/check-email")[0] == 302, "pending user can request another confirmation email")
                link = mail_link(directory, EMAIL, "/confirm-email", after_count=before)
                secrets.append(link[1]["code"])
                check(client.request(urllib.parse.urlsplit(link[0]).path + "?" + urllib.parse.urlsplit(link[0]).query)[0] == 200
                      and sql(f"SELECT EmailConfirmed FROM dbo.Users WHERE Email=N'{EMAIL}';") == "0", "email preview does not activate the account")
                status, _, body = client.post_form("/confirm-email?" + urllib.parse.urlencode(link[1]), {"UserId": link[1]["userId"], "Code": "invalid"})
                check(status == 200 and "Liên kết" in body, "invalid confirmation token rejected")
                check(confirm(client, link)[0] == 302 and sql(f"SELECT EmailConfirmed FROM dbo.Users WHERE Email=N'{EMAIL}';") == "1", "confirmation activates only the token owner")
                check(confirm(client, link)[0] == 200, "confirmation cannot be replayed")
                status, headers, _ = Client(url).post_form("/register", user)
                check(status == 302 and headers.get("Location") == "/check-email" and sql(count_query) == "1", "duplicate registration has a generic result")
                before = len(list(directory.glob("*.eml")))
                check(client.post_form("/resend-confirmation", {"Email": missing_email}, "/check-email")[0] == 302
                      and client.post_form("/resend-confirmation", {"Email": EMAIL}, "/check-email")[0] == 302
                      and len(list(directory.glob("*.eml"))) == before, "resend does not disclose confirmed or missing accounts")
                status, headers, _ = client.post_form("/login", {"Email": EMAIL, "Password": PASSWORD, "ReturnUrl": "https://attacker.invalid/"})
                cookies = headers.get_all("Set-Cookie") or []
                check(status == 302 and headers.get("Location") == "/parent-home", "external return URL is ignored")
                check(any(".SafeLearnKids.Identity=" in cookie and "httponly" in cookie.lower() and "samesite=lax" in cookie.lower() for cookie in cookies), "identity cookie is HttpOnly and SameSite")
                status, _, body = client.request("/settings")
                check(status == 200 and EMAIL in body and "/change-password" in body, "verified user reaches SQL account and real password link")
                check(client.request("/admin-users")[0] != 200, "Parent cannot access Admin")
                try:
                    sql(f"UPDATE dbo.Users SET FullName=N'<script>alert(1)</script>' WHERE Email=N'{EMAIL}';")
                    status, _, raw = client.request("/settings", raw=True)
                    check(status == 200 and "<script>alert(1)</script>" not in raw and "\\u003Cscript" in raw, "profile text is encoded in Razor and embedded JSON")
                finally:
                    sql(f"UPDATE dbo.Users SET FullName=N'SQL smoke test' WHERE Email=N'{EMAIL}';")
                check(client.request("/logout", {})[0] == 400 and client.request("/change-password", {})[0] == 400, "authenticated mutation requires CSRF")
                replay = clone(client)
                check(client.request("/logout", {"__RequestVerificationToken": token_from(body)})[0] == 302
                      and client.request("/settings")[0] == 302 and replay.request("/settings")[0] == 302, "logout also revokes captured tickets")
                wrong = client.post_form("/login", {"Email": EMAIL, "Password": "WrongPassword123"})
                absent = client.post_form("/login", {"Email": missing_email, "Password": "WrongPassword123"})
                check(wrong[0] == absent[0] == 200 and "Không thể đăng nhập" in wrong[2] and "Không thể đăng nhập" in absent[2], "wrong and missing users receive the same login error")
                for _ in range(4):
                    client.post_form("/login", {"Email": EMAIL, "Password": "WrongPassword123"})
                check(sql(f"SELECT COUNT(*) FROM dbo.Users WHERE Email=N'{EMAIL}' AND LockoutEnd>SYSUTCDATETIME();") == "1"
                      and client.post_form("/login", {"Email": EMAIL, "Password": PASSWORD})[0] == 200, "five failures lock the account without exposing counts")
                sql(f"UPDATE dbo.Users SET LockoutEnd=NULL, AccessFailedCount=0 WHERE Email=N'{EMAIL}';")
                check(client.post_form("/login", {"Email": EMAIL, "Password": PASSWORD, "ReturnUrl": "/change-password"})[1].get("Location") == "/change-password", "local return URL is honored")
                observer = clone(client)
                sql(f"UPDATE dbo.Users SET IsActive=0 WHERE Email=N'{EMAIL}';")
                check(observer.request("/settings")[0] == 302 and client.post_form("/login", {"Email": EMAIL, "Password": PASSWORD})[0] == 200, "disabled user loses existing tickets and cannot sign in")
                sql(f"UPDATE dbo.Users SET IsActive=1 WHERE Email=N'{EMAIL}';")
                client = Client(url)
                client.post_form("/login", {"Email": EMAIL, "Password": PASSWORD})
                observer = clone(client)
                before = len(list(directory.glob("*.eml")))
                known = client.post_form("/recover", {"Email": EMAIL})
                mail_link(directory, EMAIL, "/reset-password", after_count=before)
                absent = client.post_form("/recover", {"Email": missing_email})
                check(known[0] == absent[0] == 200 and "Đã tiếp nhận yêu cầu" in known[2] and "Đã tiếp nhận yêu cầu" in absent[2]
                      and len(list(directory.glob("*.eml"))) == before + 1, "recovery delivers privately with a generic response")
                reset_link, reset = mail_link(directory, EMAIL, "/reset-password")
                secrets.append(reset["token"])
                check(reset["token"] not in known[2] and reset["token"] not in absent[2], "reset token never appears in recovery HTTP response")
                form = "/reset-password?" + urllib.parse.urlencode(reset)
                values = {"Email": EMAIL, "Token": "invalid", "Password": reset_password, "ConfirmPassword": reset_password}
                check(client.post_form(form, values)[0] == 200 and reset_password not in client.post_form(form, values)[2], "invalid reset rejected without echoing a password")
                check(client.post_form(form, {**values, "Email": missing_email, "Token": reset["token"]})[0] == 200, "reset token cannot target another account")
                check(client.post_form(form, {**values, "Token": reset["token"]})[0] == 302, "valid reset changes SQL password")
                check(observer.request("/settings")[0] == 302, "reset revokes previous login tickets")
                check(client.post_form(form, {**values, "Token": reset["token"]})[0] == 200, "reset token can be used only once")
                check(client.post_form("/login", {"Email": EMAIL, "Password": PASSWORD})[0] == 200
                      and client.post_form("/login", {"Email": EMAIL, "Password": reset_password})[0] == 302, "reset replaces the old password")
                observer = clone(client)
                bad_change = {"CurrentPassword": "WrongCurrent1", "Password": changed_password, "ConfirmPassword": changed_password}
                status, _, body = client.post_form("/change-password", bad_change)
                check(status == 200 and changed_password not in body and "WrongCurrent1" not in body, "wrong current password fails without echo")
                check(client.post_form("/change-password", {**bad_change, "CurrentPassword": reset_password, "Password": "abcdefgh", "ConfirmPassword": "abcdefgh"})[0] == 200, "change enforces password policy")
                check(client.post_form("/change-password", {**bad_change, "CurrentPassword": reset_password, "Email": missing_email, "Role": "Admin"})[0] == 302
                      and client.request("/settings")[0] == 302 and observer.request("/settings")[0] == 302, "change password signs out all old tickets and ignores forged fields")
                check(client.post_form("/login", {"Email": EMAIL, "Password": reset_password})[0] == 200
                      and client.post_form("/login", {"Email": EMAIL, "Password": changed_password})[0] == 302, "changed password replaces the previous one")
                host_client = Client(url)
                _, _, body = host_client.request("/recover")
                before = len(list(directory.glob("*.eml")))
                status, _, _ = host_client.request("/recover", {"Email": EMAIL, "__RequestVerificationToken": token_from(body)}, {"Host": "attacker.invalid"})
                poisoned_link = mail_link(directory, EMAIL, "/reset-password", after_count=before)
                secrets.append(poisoned_link[1]["token"])
                check(status == 200 and len(list(directory.glob("*.eml"))) == before + 1 and poisoned_link[0].startswith(url + "/reset-password?"), "Host header cannot poison emailed links")
                for path in ("/appsettings.json", "/Program.cs", "/Services/AccountEmailService.cs", "/InputContext/11_CURRENT_STATE.md"):
                    check(client.request(path)[0] == 404, f"private file {path} not exposed")
                _, _, script = client.request("/js/store.js")
                check("matkhau123" not in script and "admin12345" not in script and "state.accounts=[]" in script, "public script has no passwords and clears legacy credentials")
                with ThreadPoolExecutor(max_workers=2) as executor:
                    results = list(executor.map(lambda _: Client(url).post_form("/register", {**user, "Email": race_email}), range(2)))
                check(all(result[0] == 302 and result[1].get("Location") == "/check-email" for result in results)
                      and sql(f"SELECT COUNT(*) FROM dbo.Users WHERE Email=N'{race_email}';") == "1", "simultaneous registration keeps one account and a generic response")
            check(sql(count_query) == "1", "SQL user survives process shutdown")
            with running_app(url, directory, {"Auth__PasswordResetTokenLifetime": "00:00:01", "Auth__EmailConfirmationTokenLifetime": "00:00:01"}, logs) as client:
                check(client.post_form("/login", {"Email": EMAIL, "Password": changed_password})[0] == 302, "fresh process signs in with persisted changed password")
                before = len(list(directory.glob("*.eml")))
                client.post_form("/recover", {"Email": EMAIL})
                _, expired = mail_link(directory, EMAIL, "/reset-password", after_count=before)
                secrets.append(expired["token"])
                client.post_form("/register", {**user, "Email": other_email})
                expired_confirmation = mail_link(directory, other_email, "/confirm-email")
                secrets.append(expired_confirmation[1]["code"])
                time.sleep(1.5)
                check(client.post_form("/reset-password?" + urllib.parse.urlencode(expired), {"Email": EMAIL, "Token": expired["token"], "Password": reset_password, "ConfirmPassword": reset_password})[0] == 200, "expired reset token is rejected")
                check(confirm(client, expired_confirmation)[0] == 200 and sql(f"SELECT EmailConfirmed FROM dbo.Users WHERE Email=N'{other_email}';") == "0", "expired confirmation token is rejected")
                sql(f"UPDATE dbo.Users SET EmailConfirmed=1, PasswordHash=N'not-valid-base64' WHERE Email=N'{other_email}';")
                check(client.post_form("/login", {"Email": other_email, "Password": PASSWORD})[0] == 200, "legacy malformed password hashes do not expose an exception")
            with running_app(url, directory, {"Auth__RateLimitPermitLimit": "2"}, logs) as client:
                first = client.post_form("/recover", {"Email": missing_email})
                second = client.post_form("/recover", {"Email": missing_email})
                third = client.post_form("/recover", {"Email": missing_email})
                check(first[0] == second[0] == 200 and third[0] == 429 and third[1].get("Retry-After"), "per-IP rate limiter returns 429 and Retry-After")
            check(all(secret not in log for secret in secrets for log in logs), "runtime logs contain no passwords or issued tokens")
            for overrides in (
                {"ASPNETCORE_ENVIRONMENT": "Production", "Email__DeliveryMethod": "Pickup", "Email__PublicBaseUrl": "https://example.invalid"},
                {"Email__PublicBaseUrl": "http://attacker.invalid"},
                {"ASPNETCORE_ENVIRONMENT": "Production", "Demo__UseInMemoryDatabase": "true"},
            ):
                refused = False
                try:
                    with running_app(url, directory, overrides):
                        pass
                except RuntimeError:
                    refused = True
                check(refused, "unsafe production/demo/email configuration is refused at startup")
    finally:
        sql("DELETE FROM dbo.LoginHistories WHERE UserNameTried LIKE N'sql-smoke-%@example.invalid';")
        sql(f"DELETE FROM dbo.Users WHERE Email IN (N'{EMAIL}',N'{other_email}',N'{race_email}') AND FullName=N'SQL smoke test';")
        check(sql(f"SELECT COUNT(*) FROM dbo.Users WHERE Email IN (N'{EMAIL}',N'{other_email}',N'{race_email}');") == "0", "only generated test users and their Identity links are cleaned up")
    print(f"RESULT {checks}/{checks} passed (SQL + email + security + restart)")


if __name__ == "__main__":
    main()
