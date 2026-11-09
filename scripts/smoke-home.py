"""The parent home page shows the chosen child's real numbers; the sidebar switcher and the health check work.

Uses the existing smoke harness, disposable UUID accounts and private email pickup. Build first. Removes everything it creates.
"""

import html
import importlib.util
import json
from pathlib import Path
import re
import socket
import sys
import tempfile
import urllib.request
import uuid


sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location("auth_smoke", Path(__file__).with_name("smoke-sql-auth.py"))
smoke = importlib.util.module_from_spec(spec)
spec.loader.exec_module(smoke)
check, sql = smoke.check, smoke.sql

RUN = uuid.uuid4().hex
EMAIL = f"sql-smoke-home-{RUN}@example.invalid"
PASSWORD = f"HomeTest1{RUN}"
FIRST, SECOND = f"Be Mot {RUN[:4]}", f"Be Hai {RUN[:4]}"


def free_url():
    with socket.socket() as listener:
        listener.bind(("127.0.0.1", 0))
        return f"http://127.0.0.1:{listener.getsockname()[1]}"


def add_child(client, name):
    body = client.request("/profiles", raw=True)[2]
    token = json.loads(re.search(r'window\.SL_CSRF = ("[^"]+")', body).group(1))
    request = urllib.request.Request(client.url + "/children", data=json.dumps({"name": name, "cls": 3, "subjects": ["toan"]}).encode(),
                                     headers={"Content-Type": "application/json", "RequestVerificationToken": token})
    with client.opener.open(request, timeout=10) as response:
        return json.loads(response.read())["id"]


def page(client, path):
    status, _, body = client.request(path, raw=True)
    return status, html.unescape(body)


def main():
    url = free_url()
    try:
        with tempfile.TemporaryDirectory(prefix="safelearn-private-mail-") as temporary, smoke.running_app(url, Path(temporary)):
            anonymous = smoke.Client(url)
            status, body = page(anonymous, "/healthz")
            check(status == 200 and body.strip() == "ok", "the health check answers ok when the database is reachable")

            client = smoke.Client(url)
            status, _, _ = client.post_form("/register", {"FullName": "Me Thu", "Email": EMAIL, "Password": PASSWORD, "ConfirmPassword": PASSWORD, "Agree": "true"})
            check(status == 302 and smoke.confirm(client, smoke.mail_link(Path(temporary), EMAIL, "/confirm-email"))[0] == 302, "registers a parent")
            check(client.post_form("/login", {"Email": EMAIL, "Password": PASSWORD})[0] == 302, "the parent signs in")
            status, headers, _ = client.request("/parent-home", raw=True)
            check(status == 302 and "/onboarding" in (headers.get("Location") or ""), "without a child the home page sends the parent to onboarding")

            first, second = add_child(client, FIRST), add_child(client, SECOND)
            sql(f"INSERT INTO dbo.DailyUsages (ChildProfileId, UsageDate, Seconds) VALUES ({first}, CAST(GETDATE() AS date), 1260), "
                f"({first}, DATEADD(day, -2, CAST(GETDATE() AS date)), 2400);")
            sql(f"INSERT INTO dbo.TimeExtensionRequests (ChildProfileId, ForDate, RequestedMinutes, Reason, Status, RequestedAt) "
                f"VALUES ({first}, CAST(GETDATE() AS date), 15, N'Con lam bai {RUN[:6]}', N'Pending', SYSUTCDATETIME());")

            status, body = page(client, "/parent-home")
            check(status == 200, "the home page opens")
            for fake in ("Minh An", "chị Hà", "Phân số cơ bản", "Bảng nhân 5"):
                check(fake not in body, f"no demo data on the home page ({fake})")
            check(FIRST in body and "Me Thu" in body, "the greeting names the parent and the first child")
            check("21 phút" in body, "today's minutes come from the database")
            check(">40<" in body, "the 7-day chart shows a day two days ago")
            check(f"xin thêm 15 phút" in body and f"Con lam bai {RUN[:6]}" in body and f"/time?child={first}" in body,
                  "a pending time request waits for the parent")
            check("chưa có bài nào đang học dở" in body, "with no lesson started the card says so")

            status, body = page(client, f"/parent-home?child={second}")
            check(status == 200 and f"hôm nay {SECOND}" in body and "0 phút" in body and "xin thêm" not in body,
                  "choosing the second child shows that child's numbers")
            check(f'href="/parent-home?child={first}"' in body and f'href="/parent-home?child={second}"' in body,
                  "the sidebar switcher lists every child as a link")
            status, body = page(client, f"/progress?child={second}")
            check(f'href="/progress?child={first}"' in body, "on a per-child page the switcher keeps the current page")
            status, body = page(client, "/library")
            check(f'href="/parent-home?child={first}"' in body, "on other pages the switcher opens the child's home")

            other = sql("SELECT TOP 1 Id FROM dbo.ChildProfiles WHERE ParentId <> (SELECT Id FROM dbo.Users WHERE Email=N'" + EMAIL + "') ORDER BY Id;")
            if other:
                status, body = page(client, f"/parent-home?child={other}")
                check(status == 200 and f"hôm nay {FIRST}" in body, "another family's child id falls back to the parent's own child")

            ui = urllib.request.urlopen(url + "/js/ui.js", timeout=10).read().decode("utf-8")
            check("generic fallback for demo-only buttons" not in ui and "Dữ liệu mẫu của bản demo" not in ui, "buttons no longer fake success")
            check("/admin-catalog?q=" in ui and "/library?q=" in ui, "the top search opens a real search")
    finally:
        ids = f"SELECT Id FROM dbo.Users WHERE Email=N'{EMAIL}'"
        kids = f"SELECT Id FROM dbo.ChildProfiles WHERE ParentId IN ({ids})"
        for statement in (f"DELETE FROM dbo.DailyUsages WHERE ChildProfileId IN ({kids});",
                          f"DELETE FROM dbo.TimeExtensionRequests WHERE ChildProfileId IN ({kids});",
                          f"DELETE FROM dbo.ChildSubjects WHERE ChildProfileId IN ({kids});",
                          f"DELETE FROM dbo.ChildProfiles WHERE ParentId IN ({ids});",
                          f"DELETE FROM dbo.Notifications WHERE UserId IN ({ids});",
                          f"DELETE FROM dbo.UserSettings WHERE UserId IN ({ids});",
                          f"DELETE FROM dbo.AuditLogs WHERE ActorId IN ({ids});",
                          f"DELETE FROM dbo.LoginHistories WHERE UserNameTried=N'{EMAIL}';",
                          f"DELETE FROM dbo.Users WHERE Email=N'{EMAIL}';"):
            try:
                sql(statement)
            except Exception as error:  # a table may not exist in an older schema
                print("cleanup:", error)
    print("smoke-home: all checks passed")


if __name__ == "__main__":
    main()
