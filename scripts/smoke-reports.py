"""Exercise Sprint 8 (US-47 to US-52) on real SQL: child-mode lock and PIN, settings and notifications, weekly reports
(page and background job), admin dashboard and content statistics, logs, and export/deletion of a child's data.

Uses the existing smoke harness, disposable UUID accounts and private email pickup. Build first.
Turns the background jobs on for this run and removes everything it creates, including rows the jobs add for other data.
"""

from datetime import date, datetime, timedelta
import importlib.util
import json
from pathlib import Path
import re
import socket
import sys
import tempfile
import time
import urllib.request
import uuid


sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location("auth_smoke", Path(__file__).with_name("smoke-sql-auth.py"))
smoke = importlib.util.module_from_spec(spec)
spec.loader.exec_module(smoke)
check, sql = smoke.check, smoke.sql
# sqlcmd prints in the console code page, so text compared through SQL stays ASCII.

RUN = uuid.uuid4().hex
PARENT = f"sql-smoke-rep-parent-{RUN}@example.invalid"
OTHER = f"sql-smoke-rep-other-{RUN}@example.invalid"
ADMIN = f"sql-smoke-rep-admin-{RUN}@example.invalid"
PASSWORD = f"RepTest1{RUN}"
CODE = f"RP{RUN[:8].upper()}"
KEY = CODE.lower()
TAG = RUN[:6]
TODAY = date.today()
LAST_MONDAY = TODAY - timedelta(days=TODAY.weekday() + 7)
STARTED = datetime.utcnow() - timedelta(seconds=5)


def free_url():
    with socket.socket() as listener:
        listener.bind(("127.0.0.1", 0))
        return f"http://127.0.0.1:{listener.getsockname()[1]}"


def register(directory, url, email, name):
    client = smoke.Client(url)
    status, _, _ = client.post_form("/register", {"FullName": name, "Email": email, "Password": PASSWORD, "ConfirmPassword": PASSWORD, "Agree": "true"})
    check(status == 302 and smoke.confirm(client, smoke.mail_link(directory, email, "/confirm-email"))[0] == 302, f"registers {name}")


def form(client, page, path, pairs=()):
    body = client.request(page, raw=True)[2]
    found = re.search(r'window\.SL_CSRF = ("[^"]+")', body)
    token = smoke.token_from(body) if 'name="__RequestVerificationToken"' in body else json.loads(found.group(1))
    return client.request(path, list(pairs) + [("__RequestVerificationToken", token)])


def add_child(client, name):
    body = client.request("/profiles", raw=True)[2]
    token = json.loads(re.search(r'window\.SL_CSRF = ("[^"]+")', body).group(1))
    request = urllib.request.Request(client.url + "/children", data=json.dumps({"name": name, "cls": 2, "subjects": [KEY]}).encode(),
                                     headers={"Content-Type": "application/json", "RequestVerificationToken": token})
    with client.opener.open(request, timeout=10) as response:
        return json.loads(response.read())["id"]


def exit_child(client, pin=None, password=None):
    pairs = [("pin", pin)] if pin is not None else [("password", password)]
    return form(client, "/kids-exit", "/kids-exit", pairs)


def location(client, path):
    return client.request(path)[1].get("Location", "") or ""


def checks(url, directory):
    overrides = {"Jobs__Enabled": "true", "Jobs__StartDelaySeconds": "5", "Jobs__IntervalMinutes": "1"}
    with smoke.running_app(url, directory, overrides):
        register(directory, url, PARENT, "Report parent")
        register(directory, url, OTHER, "Report other")
        register(directory, url, ADMIN, "Report admin")
        admin_id, parent_id = (sql(f"SELECT Id FROM dbo.Users WHERE Email=N'{email}';") for email in (ADMIN, PARENT))
        sql(f"DELETE FROM dbo.UserRoles WHERE UserId=N'{admin_id}';")
        sql(f"INSERT dbo.UserRoles(UserId, RoleId) SELECT N'{admin_id}', Id FROM dbo.Roles WHERE NormalizedName=N'ADMIN';")
        sql(f"INSERT dbo.Subjects(Code, Name, IconKey, ColorKey, SortOrder, IsActive) VALUES (N'{CODE}', N'Mon bao cao {TAG}', N'star', N'blue', 91, 1);")
        sql(f"INSERT dbo.SubjectGrades(SubjectId, GradeLevelId) SELECT s.Id, g.Id FROM dbo.Subjects s CROSS JOIN dbo.GradeLevels g WHERE s.Code=N'{CODE}' AND g.Level=2;")
        for name, title in (("one", f"Bai mot {TAG}"), ("two", f"=Bai hai {TAG}")):
            sql("INSERT dbo.Lessons(SubjectId, GradeLevelId, Title, LessonType, DurationMin, SortOrder, Status, CreatedById, CreatedAt, UpdatedAt, PublishedAt) "
                f"SELECT s.Id, g.Id, N'{title}', N'Reading', 5, 1, N'Published', N'{admin_id}', SYSUTCDATETIME(), SYSUTCDATETIME(), SYSUTCDATETIME() "
                f"FROM dbo.Subjects s CROSS JOIN dbo.GradeLevels g WHERE s.Code=N'{CODE}' AND g.Level=2;")
        one = sql(f"SELECT Id FROM dbo.Lessons WHERE Title=N'Bai mot {TAG}';")
        two = sql(f"SELECT Id FROM dbo.Lessons WHERE Title=N'=Bai hai {TAG}';")
        for lesson in (one, two):
            sql(f"INSERT dbo.LessonContents(LessonId, ContentKind, Title, Body, SortOrder) VALUES ({lesson}, N'Reading', N'Doc', N'Noi dung', 1);")

        parent, other, admin = smoke.Client(url), smoke.Client(url), smoke.Client(url)
        for client, email in ((parent, PARENT), (other, OTHER), (admin, ADMIN)):
            check(client.post_form("/login", {"Email": email, "Password": PASSWORD})[0] == 302, f"{email.split('-')[3]} signs in")
        kid = add_child(parent, "Be Bao Cao")
        other_kid = add_child(other, "Be Khac")

        # US-50: child mode locks the parent pages on the server
        check(form(parent, "/kids-picker", f"/kids-picker/{kid}")[0] == 302, "the parent opens child mode")
        check(location(parent, "/parent-home").endswith("/kids-home") and location(parent, "/settings").endswith("/kids-home"),
              "in child mode every parent page sends the child back to learning")
        check(form(parent, "/kids-home", "/notifications/read-all")[0] == 403, "parent actions are refused in child mode")
        check(parent.request("/kids-picker")[0] == 200 and parent.request("/kids-home")[0] == 200, "the picker keeps child mode on")
        check("mật khẩu" in parent.request("/kids-exit")[2], "without a PIN the exit screen asks for the account password")
        check(exit_child(parent, password="wrong")[0] == 400 and location(parent, "/parent-home").endswith("/kids-home"), "a wrong password keeps child mode")
        check(exit_child(parent, password=PASSWORD)[0] == 302 and parent.request("/parent-home")[0] == 200, "the right password ends child mode")
        check(form(other, "/kids-picker", f"/kids-picker/{other_kid}")[0] == 302, "another parent opens child mode")
        for _ in range(4):
            exit_child(other, password="wrong")
        status, _, body = exit_child(other, password="wrong")
        check(status == 400 and "Sai quá nhiều lần" in body, "five wrong attempts lock the exit screen")
        check(exit_child(other, password=PASSWORD)[0] == 400 and location(other, "/parent-home").endswith("/kids-home"), "while locked even the right password waits")
        # From here the other parent uses a second device, so the checks below test ownership, not the lock.
        other = smoke.Devices(other)

        # US-50: server-side PIN
        check(parent.request("/pin")[0] == 200, "the parent opens the PIN page")
        pin_hash = lambda: sql(f"SELECT ISNULL(ParentPinHash, N'-') FROM dbo.UserSettings WHERE UserId=N'{parent_id}';")
        check(form(parent, "/pin", "/pin", [("Pin", "2468"), ("ConfirmPin", "2468"), ("Password", "wrong")])[0] == 400 and pin_hash() in ("", "-"),
              "setting a PIN needs the account password")
        check(form(parent, "/pin", "/pin", [("Pin", "2468"), ("ConfirmPin", "1357"), ("Password", PASSWORD)])[0] == 400 and pin_hash() in ("", "-"),
              "the two PINs must match")
        check(form(parent, "/pin", "/pin", [("Pin", "12a4"), ("ConfirmPin", "12a4"), ("Password", PASSWORD)])[0] == 400, "a PIN is exactly four digits")
        check(form(parent, "/pin", "/pin", [("Pin", "2468"), ("ConfirmPin", "2468"), ("Password", PASSWORD)])[0] == 302, "the parent sets a PIN")
        stored = pin_hash()
        check(stored.startswith("AQAAAA") and "2468" not in stored, "the PIN is stored hashed")
        check(sql(f"SELECT COUNT(*) FROM dbo.AuditLogs WHERE ActorId=N'{parent_id}' AND Action=N'ParentPinChanged';") == "1", "changing the PIN is audited")
        form(parent, "/kids-picker", f"/kids-picker/{kid}")
        check("Nhập PIN" in parent.request("/kids-exit")[2], "with a PIN the exit screen shows the keypad")
        check(exit_child(parent, pin="1111")[0] == 400 and location(parent, "/settings").endswith("/kids-home"), "a wrong PIN keeps child mode")
        check(exit_child(parent, pin="2468")[0] == 302 and parent.request("/settings")[0] == 200, "the right PIN ends child mode")
        form(parent, "/kids-picker", f"/kids-picker/{kid}")
        check(exit_child(parent, password=PASSWORD)[0] == 302, "a parent who forgot the PIN can still leave with the password")
        parent = smoke.Devices(parent)

        # US-50: settings and notifications
        # A ticked box posts "true" then its hidden "false"; an unticked one posts only "false".
        check(form(parent, "/settings", "/settings", [("Theme", "Dark"), ("WeeklyEmail", "false"), ("NotifyTimeRequest", "true"),
                                                      ("NotifyTimeRequest", "false"), ("NotifyContentReport", "false")])[0] == 302, "the parent saves settings")
        check(sql(f"SELECT Theme + N'|' + CAST(WeeklyEmail AS varchar) + CAST(NotifyTimeRequest AS varchar) + CAST(NotifyContentReport AS varchar) FROM dbo.UserSettings WHERE UserId=N'{parent_id}';")
              == "Dark|010", "theme and alert choices are stored")
        check('window.SL_THEME = "dark"' in parent.request("/settings", raw=True)[2], "the saved theme reaches every page")
        form(parent, "/kids-picker", f"/kids-picker/{kid}")
        form(parent, f"/kids-lesson?id={one}", f"/kids-lesson/{one}/report", [("reason", "Ads")])
        reports = lambda: sql(f"SELECT COUNT(*) FROM dbo.Notifications WHERE UserId=N'{parent_id}' AND NotificationType=N'ContentReport';")
        check(sql(f"SELECT COUNT(*) FROM dbo.ContentReports WHERE ChildProfileId={kid};") == "1" and reports() == "0", "a switched-off alert is not sent")
        form(parent, "/settings", "/settings", [("Theme", "Dark"), ("WeeklyEmail", "false"), ("NotifyTimeRequest", "true"),
                                                ("NotifyTimeRequest", "false"), ("NotifyContentReport", "true"), ("NotifyContentReport", "false")])
        form(parent, f"/kids-lesson?id={two}", f"/kids-lesson/{two}/report", [("reason", "Wrong")])
        check(reports() == "1", "switching it back on sends it again")
        for title, link in (("Thong bao A", "/history"), ("Thong bao B", "https://evil.example/"), ("Thong bao C", None)):
            link_sql = "NULL" if link is None else f"N'{link}'"
            sql(f"INSERT dbo.Notifications(UserId, NotificationType, Title, Body, LinkUrl, IsRead, CreatedAt) VALUES (N'{parent_id}', N'System', N'{title} {TAG}', N'Noi dung', {link_sql}, 0, SYSUTCDATETIME());")
        unread = lambda: sql(f"SELECT COUNT(*) FROM dbo.Notifications WHERE UserId=N'{parent_id}' AND IsRead=0;")
        check(f"Thong bao A {TAG}" in parent.request("/notifications")[2] and f"window.SL_UNREAD = {unread()}" in parent.request("/notifications", raw=True)[2],
              "the parent sees notifications and the unread count")
        a, b = (sql(f"SELECT Id FROM dbo.Notifications WHERE Title=N'Thong bao {letter} {TAG}';") for letter in "AB")
        status, headers, _ = form(parent, "/notifications", f"/notifications/{a}/open")
        check(status == 302 and headers.get("Location") == "/history" and sql(f"SELECT CAST(IsRead AS varchar) FROM dbo.Notifications WHERE Id={a};") == "1",
              "opening a notification marks it read and follows its link")
        status, headers, _ = form(parent, "/notifications", f"/notifications/{b}/open")
        check(status == 302 and not headers.get("Location", "").startswith("http"), "a link to another site is never followed")
        check(form(other, "/notifications", f"/notifications/{a}/open")[0] == 404, "another account cannot open it")
        check(f"Thong bao C {TAG}" in parent.request("/notifications?unread=true")[2] and f"Thong bao A {TAG}" not in parent.request("/notifications?unread=true")[2],
              "the unread filter hides read notifications")
        check(form(parent, "/notifications", "/notifications/read-all")[0] == 302 and unread() == "0", "the parent marks everything read")
        check(form(parent, "/notifications", f"/notifications/{a}/delete")[0] == 302 and sql(f"SELECT COUNT(*) FROM dbo.Notifications WHERE Id={a};") == "0",
              "the parent deletes a notification")

        # US-47: weekly report
        for offset, seconds in ((0, 600), (2, 1200)):
            sql(f"INSERT dbo.DailyUsages(ChildProfileId, UsageDate, Seconds) VALUES ({kid}, '{(LAST_MONDAY + timedelta(days=offset)).isoformat()}', {seconds});")
        completed = f"DATEADD(hour, 10, CAST('{LAST_MONDAY.isoformat()}' AS datetime2))"
        # Opening the lesson to report it already created the activity row; finish it last Monday.
        sql(f"UPDATE dbo.LessonActivities SET Status=N'Completed', ProgressPercent=100, WatchSeconds=300, StartedAt={completed}, CompletedAt={completed}, "
            f"UpdatedAt=SYSUTCDATETIME() WHERE ChildProfileId={kid} AND LessonId={one};")
        weekly = f"/weekly?child={kid}&week={LAST_MONDAY.isoformat()}"
        page = parent.request(weekly)[2]
        check("30" in page and "Be Bao Cao học 30 phút trong 2 ngày, hoàn thành 1 bài" in page, "the weekly page sums last week's minutes and lessons")
        check(sql(f"SELECT CAST(TotalMinutes AS varchar) + N'|' + CAST(LessonsCompleted AS varchar) FROM dbo.WeeklyReports WHERE ChildProfileId={kid} AND WeekStart='{LAST_MONDAY.isoformat()}';") == "30|1",
              "opening a finished week stores its report")
        check("tuần này" in parent.request(f"/weekly?child={kid}&week={TODAY.isoformat()}")[2], "the current week is shown live")
        check("Be Bao Cao" not in other.request(f"/weekly?child={kid}")[2], "another family never sees this child's report")
        status, headers, body = parent.request(f"/weekly/export?child={kid}")
        check(status == 200 and "text/csv" in headers.get("Content-Type", "") and LAST_MONDAY.isoformat() in body, "the weekly reports export as CSV")
        check(other.request(f"/weekly/export?child={kid}")[0] == 404, "another family cannot export them")

        # US-47/48: background jobs (weekly report with notification and email, daily numbers)
        sql(f"DELETE FROM dbo.WeeklyReports WHERE ChildProfileId={kid};")
        sql(f"UPDATE dbo.ChildProfiles SET CreatedAt=DATEADD(day, -30, SYSUTCDATETIME()) WHERE Id={kid};")
        # The job runs every minute in this test; the first run may have started before the child was backdated.
        deadline = time.monotonic() + 150
        while time.monotonic() < deadline and sql(f"SELECT COUNT(*) FROM dbo.WeeklyReports WHERE ChildProfileId={kid};") == "0":
            time.sleep(2)
        report = sql(f"SELECT CAST(TotalMinutes AS varchar) + N'|' + CASE WHEN EmailedAt IS NULL THEN N'no' ELSE N'mailed' END FROM dbo.WeeklyReports WHERE ChildProfileId={kid};")
        check(report == "30|no", f"the job writes last week's report (no email: the parent switched weekly email off), got {report!r}")
        check(sql(f"SELECT COUNT(*) FROM dbo.Notifications WHERE UserId=N'{parent_id}' AND NotificationType=N'WeeklyReport' AND LinkUrl LIKE N'/weekly?child={kid}%';") == "1",
              "the parent is told the report is ready")
        check(sql(f"SELECT COUNT(*) FROM dbo.SystemMetricsDaily WHERE MetricDate=CAST(DATEADD(day, -1, GETDATE()) AS date);") == "1", "the job records yesterday's numbers")
        check(sql(f"SELECT COUNT(*) FROM dbo.SystemMetricsDaily WHERE MetricDate='{(LAST_MONDAY + timedelta(days=2)).isoformat()}';") == "1", "the job fills the last 30 days")

        # US-48/49: admin dashboard and content statistics
        check(parent.request("/admin-home")[0] != 200 and parent.request("/admin-stats")[0] != 200, "parents cannot open admin statistics")
        page = admin.request("/admin-home")[2]
        check("Việc cần làm" in page and "Phụ huynh" in page and "Số phút học theo ngày" in page, "the admin dashboard shows totals, trend and work to do")
        sql(f"INSERT dbo.SearchLogs(ChildProfileId, Query, ResultCount, WasBlocked, CreatedAt) VALUES ({kid}, N'khung long {TAG}', 0, 0, SYSUTCDATETIME()), ({kid}, N'khung long {TAG}', 0, 0, SYSUTCDATETIME());")
        status, _, page = admin.request("/admin-stats?days=30")
        found = (status, f"Bai mot {TAG}" in page, f"khung long {TAG}" in page, "2 không có kết quả" in page)
        check(found == (200, True, True, True), f"content statistics list lessons and searches with no result {found}")
        status, headers, body = admin.request("/admin-stats/export?days=30")
        check(status == 200 and f"\"'=Bai hai {TAG}\"" in body, "the CSV export neutralises formula-like titles")

        # US-51: logs
        bad = smoke.Client(url)
        bad.post_form("/login", {"Email": PARENT, "Password": "wrong-password"})
        page = admin.request(f"/admin-audit?tab=logins&q={PARENT}&result=failed")[2]
        check(PARENT in page and "Thất bại" in page, "failed sign-ins are listed and filterable")
        status, _, page = admin.request(f"/admin-audit?type=ParentPinChanged&q={PARENT}")
        found = (status, PARENT in page, "ParentPinChanged" in page, "1 dòng" in page)
        check(found == (200, True, True, True), f"actions can be filtered by type and show who did them {found}")
        status, _, body = admin.request(f"/admin-audit/export?tab=logins&q={PARENT}")
        check(status == 200 and PARENT in body, "the logs export as CSV")
        check("Thất bại" in parent.request("/settings")[2], "a parent sees their own sign-in history")
        check(parent.request("/admin-audit")[0] != 200, "parents cannot read the logs")

        # US-52: export and deletion
        status, headers, body = form(parent, "/privacy", f"/privacy/{kid}/export")
        check(status == 200 and "application/json" in headers.get("Content-Type", "") and json.loads(body)["profile"]["displayName"] == "Be Bao Cao"
              and any(row["lesson"] == f"Bai mot {TAG}" for row in json.loads(body)["lessons"]), "the parent downloads the child's data as JSON")
        check(sql(f"SELECT Status FROM dbo.DataRequests WHERE ChildProfileId={kid} AND RequestType=N'Export';") == "Done", "the export is recorded")
        check(form(other, "/privacy", f"/privacy/{kid}/export")[0] == 404, "another family cannot download it")
        deletes = lambda status: sql(f"SELECT COUNT(*) FROM dbo.DataRequests WHERE ChildProfileId={kid} AND RequestType=N'Delete' AND Status=N'{status}';")
        form(parent, "/privacy", f"/privacy/{kid}/delete", [("confirmName", "Ten sai")])
        check(deletes("Pending") == "0", "a deletion request needs the child's name")
        check(form(parent, "/privacy", f"/privacy/{kid}/delete", [("confirmName", "be bao cao")])[0] == 302 and deletes("Pending") == "1", "the parent asks to delete the child's data")
        form(parent, "/privacy", f"/privacy/{kid}/delete", [("confirmName", "Be Bao Cao")])
        check(deletes("Pending") == "1", "only one deletion request waits at a time")
        request = sql(f"SELECT Id FROM dbo.DataRequests WHERE ChildProfileId={kid} AND RequestType=N'Delete' AND Status=N'Pending';")
        check(form(parent, "/privacy", f"/privacy/requests/{request}/cancel")[0] == 302 and deletes("Rejected") == "1", "the parent can cancel before it is handled")
        form(parent, "/privacy", f"/privacy/{kid}/delete", [("confirmName", "Be Bao Cao")])
        request = sql(f"SELECT Id FROM dbo.DataRequests WHERE ChildProfileId={kid} AND RequestType=N'Delete' AND Status=N'Pending';")
        check(parent.request("/admin-data-requests")[0] != 200 and "Be Bao Cao" in admin.request("/admin-data-requests")[2], "admins see the request, parents cannot handle it")
        form(admin, "/admin-data-requests", f"/admin-data-requests/{request}", [("decision", "reject")])
        check(deletes("Pending") == "1", "declining needs a reason")
        form(admin, "/admin-data-requests", f"/admin-data-requests/{request}", [("decision", "reject"), ("note", "Can xac minh them")])
        check(deletes("Rejected") == "2" and sql(f"SELECT COUNT(*) FROM dbo.Notifications WHERE UserId=N'{parent_id}' AND NotificationType=N'System' AND LinkUrl=N'/privacy';") == "1",
              "the admin declines with a reason and the parent is told")
        form(parent, "/privacy", f"/privacy/{kid}/delete", [("confirmName", "Be Bao Cao")])
        request = sql(f"SELECT Id FROM dbo.DataRequests WHERE ChildProfileId={kid} AND RequestType=N'Delete' AND Status=N'Pending';")
        check(form(admin, "/admin-data-requests", f"/admin-data-requests/{request}", [("decision", "approve")])[0] == 302, "the admin carries out the deletion")
        check(sql(f"SELECT COUNT(*) FROM dbo.ChildProfiles WHERE Id={kid};") == "0" and sql(f"SELECT COUNT(*) FROM dbo.DailyUsages WHERE ChildProfileId={kid};") == "0"
              and sql(f"SELECT COUNT(*) FROM dbo.LessonActivities WHERE ChildProfileId={kid};") == "0" and sql(f"SELECT COUNT(*) FROM dbo.WeeklyReports WHERE ChildProfileId={kid};") == "0",
              "the profile and its learning data are gone")
        check(deletes("Done") == "1" and sql(f"SELECT COUNT(*) FROM dbo.AuditLogs WHERE ActorId=N'{admin_id}' AND Action=N'ChildDataDeleted';") == "1", "the deletion is recorded and audited")


def main():
    metric_days = set(sql("SELECT ISNULL(STRING_AGG(CONVERT(varchar(10), MetricDate, 23), ','), N'') FROM dbo.SystemMetricsDaily;").split(","))
    try:
        with tempfile.TemporaryDirectory(prefix="safelearn-private-mail-") as temporary:
            checks(free_url(), Path(temporary))
    finally:
        emails = f"N'{PARENT}', N'{OTHER}', N'{ADMIN}'"
        ids = f"SELECT Id FROM dbo.Users WHERE Email IN ({emails})"
        lessons = f"SELECT l.Id FROM dbo.Lessons l JOIN dbo.Subjects s ON s.Id=l.SubjectId WHERE s.Code=N'{CODE}'"
        stamp = STARTED.strftime("%Y-%m-%d %H:%M:%S")
        # What the background job wrote for other children during this run.
        sql(f"DELETE FROM dbo.WeeklyReports WHERE GeneratedAt >= '{stamp}';")
        sql(f"DELETE FROM dbo.Notifications WHERE NotificationType=N'WeeklyReport' AND CreatedAt >= '{stamp}';")
        for day in sql("SELECT ISNULL(STRING_AGG(CONVERT(varchar(10), MetricDate, 23), ','), N'') FROM dbo.SystemMetricsDaily;").split(","):
            if day and day not in metric_days:
                sql(f"DELETE FROM dbo.SystemMetricsDaily WHERE MetricDate='{day}';")
        sql(f"DELETE FROM dbo.DataRequests WHERE ParentId IN ({ids});")
        sql(f"DELETE FROM dbo.ContentReports WHERE LessonId IN ({lessons});")
        for table in ("LessonActivities", "Favorites", "LessonContents", "Assignments"):
            sql(f"DELETE FROM dbo.{table} WHERE LessonId IN ({lessons});")
        sql(f"DELETE FROM dbo.SearchLogs WHERE ChildProfileId IN (SELECT Id FROM dbo.ChildProfiles WHERE ParentId IN ({ids}));")
        sql(f"DELETE FROM dbo.Lessons WHERE Id IN ({lessons});")
        sql(f"DELETE FROM dbo.Notifications WHERE UserId IN ({ids});")
        sql(f"DELETE FROM dbo.UserSettings WHERE UserId IN ({ids});")
        sql(f"DELETE FROM dbo.ChildProfiles WHERE ParentId IN ({ids});")
        sql(f"DELETE FROM dbo.SubjectGrades WHERE SubjectId IN (SELECT Id FROM dbo.Subjects WHERE Code=N'{CODE}');")
        sql(f"DELETE FROM dbo.Subjects WHERE Code=N'{CODE}';")
        sql(f"DELETE FROM dbo.AuditLogs WHERE ActorId IN ({ids}) OR EntityId IN ({ids});")
        sql(f"DELETE FROM dbo.LoginHistories WHERE UserNameTried IN ({emails});")
        sql(f"DELETE FROM dbo.Users WHERE Email IN ({emails});")
        check(sql(f"SELECT COUNT(*) FROM dbo.Users WHERE Email IN ({emails});") == "0" and sql(f"SELECT COUNT(*) FROM dbo.Subjects WHERE Code=N'{CODE}';") == "0",
              "generated report data is cleaned up")
    print(f"RESULT {smoke.checks}/{smoke.checks} passed (child lock + PIN + settings + notifications + weekly + jobs + admin stats + logs + child data)")


if __name__ == "__main__":
    main()
