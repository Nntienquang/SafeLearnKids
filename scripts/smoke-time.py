"""Exercise Sprint 5 (US-29 to US-34): study time, extra-time requests, history and content reports, on real SQL.

Uses the existing smoke harness, disposable UUID accounts and private email pickup.
Build first. Removes only the subject, lessons, video, children, reports and accounts it creates.
"""

from datetime import datetime, timedelta
from html import unescape
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
# sqlcmd prints in the console code page, so names compared through SQL stay ASCII.

RUN = uuid.uuid4().hex
PARENT = f"sql-smoke-time-parent-{RUN}@example.invalid"
OTHER = f"sql-smoke-time-other-{RUN}@example.invalid"
ADMIN = f"sql-smoke-time-admin-{RUN}@example.invalid"
PASSWORD = f"TimeTest1{RUN}"
CODE = f"TS{RUN[:8].upper()}"
KEY = CODE.lower()
TITLE = f"Bai thoi gian {RUN[:8]}"
VIDEO = f"t{RUN[:10]}"
TODAY = datetime.now().isoweekday() - 1


def free_url():
    with socket.socket() as listener:
        listener.bind(("127.0.0.1", 0))
        return f"http://127.0.0.1:{listener.getsockname()[1]}"


def register(directory, url, email, name):
    client = smoke.Client(url)
    status, _, _ = client.post_form("/register", {"FullName": name, "Email": email, "Password": PASSWORD, "ConfirmPassword": PASSWORD, "Agree": "true"})
    check(status == 302 and smoke.confirm(client, smoke.mail_link(directory, email, "/confirm-email"))[0] == 302, f"registers {name}")


def form(client, page, path, pairs=()):
    token = smoke.token_from(client.request(page)[2])
    return client.request(path, list(pairs) + [("__RequestVerificationToken", token)])


def add_child(client, name):
    body = client.request("/profiles", raw=True)[2]
    token = json.loads(re.search(r'window\.SL_CSRF = ("[^"]+")', body).group(1))
    request = urllib.request.Request(client.url + "/children", data=json.dumps({"name": name, "cls": 2, "subjects": [KEY], "consent": True}).encode(),
                                     headers={"Content-Type": "application/json", "RequestVerificationToken": token})
    with client.opener.open(request, timeout=10) as response:
        return json.loads(response.read())["id"]


def limits(daily, today=None, start="", end="", rest=""):
    days = [("Form.Weekdays[%d]" % i, str(today) if i == TODAY and today is not None else "") for i in range(7)]
    return [("Form.DailyMinutes", str(daily)), ("Form.StudyStart", start), ("Form.StudyEnd", end), ("Form.BreakEveryMinutes", rest)] + days


def beat(client, lesson):
    status, _, body = form(client, f"/kids-lesson?id={lesson}", f"/kids-lesson/{lesson}/heartbeat", [("seconds", "60")])
    return json.loads(body) if status == 200 else {"status": status}


def location(client, path):
    return client.request(path)[1].get("Location", "") or ""


def checks(url, directory):
    with smoke.running_app(url, directory):
        register(directory, url, PARENT, "Time parent")
        register(directory, url, OTHER, "Time other")
        register(directory, url, ADMIN, "Time admin")
        parent_id, admin_id = (sql(f"SELECT Id FROM dbo.Users WHERE Email=N'{email}';") for email in (PARENT, ADMIN))
        sql(f"DELETE FROM dbo.UserRoles WHERE UserId=N'{admin_id}';")
        sql(f"INSERT dbo.UserRoles(UserId, RoleId) SELECT N'{admin_id}', Id FROM dbo.Roles WHERE NormalizedName=N'ADMIN';")
        sql(f"INSERT dbo.Subjects(Code, Name, IconKey, ColorKey, SortOrder, IsActive) VALUES (N'{CODE}', N'Mon gio {RUN[:6]}', N'star', N'blue', 96, 1);")
        sql(f"INSERT dbo.SubjectGrades(SubjectId, GradeLevelId) SELECT s.Id, g.Id FROM dbo.Subjects s CROSS JOIN dbo.GradeLevels g WHERE s.Code=N'{CODE}' AND g.Level=2;")
        sql("INSERT dbo.Lessons(SubjectId, GradeLevelId, Title, LessonType, DurationMin, SortOrder, Status, CreatedById, CreatedAt, UpdatedAt, PublishedAt) "
            f"SELECT s.Id, g.Id, N'{TITLE}', N'Reading', 5, 1, N'Published', N'{admin_id}', SYSUTCDATETIME(), SYSUTCDATETIME(), SYSUTCDATETIME() "
            f"FROM dbo.Subjects s CROSS JOIN dbo.GradeLevels g WHERE s.Code=N'{CODE}' AND g.Level=2;")
        lesson = sql(f"SELECT Id FROM dbo.Lessons WHERE Title=N'{TITLE}';")
        sql(f"INSERT dbo.LessonContents(LessonId, ContentKind, Title, Body, SortOrder) VALUES ({lesson}, N'Reading', N'Doc', N'Noi dung', 1);")
        sql(f"INSERT dbo.SourceVideos(Platform, VideoId, Title, Status) VALUES (N'YouTube', N'{VIDEO}', N'Video bao cao {RUN[:6]}', N'Approved');")
        video = sql(f"SELECT Id FROM dbo.SourceVideos WHERE VideoId=N'{VIDEO}';")

        parent, other, admin = smoke.Client(url), smoke.Client(url), smoke.Client(url)
        for client, email in ((parent, PARENT), (other, OTHER), (admin, ADMIN)):
            check(client.post_form("/login", {"Email": email, "Password": PASSWORD})[0] == 302, f"{email.split('-')[3]} signs in")
        # Child mode locks the parent pages on the server: /kids-* pages run on the child's device, the rest on the parent's.
        parent, other = smoke.Devices(parent), smoke.Devices(other)
        kid = add_child(parent, "Be Gio")
        add_child(other, "Be Khac")
        time_page = f"/time?child={kid}"
        save = f"/time/{kid}"

        # US-30: limits
        check("Be Gio" in parent.request(time_page)[2] and "Còn 60 phút" in parent.request(time_page)[2], "a child starts with the default 60 minutes")
        check(form(parent, time_page, save, limits(700))[0] == 200 and sql(f"SELECT COUNT(*) FROM dbo.TimeLimitPolicies WHERE ChildProfileId={kid};") == "0", "more than 600 minutes a day is refused")
        check(form(parent, time_page, save, limits(30, start="18:00"))[0] == 200 and sql(f"SELECT COUNT(*) FROM dbo.TimeLimitPolicies WHERE ChildProfileId={kid};") == "0", "a study window needs both ends")
        check(form(other, "/time", save, limits(30))[0] == 404, "another family cannot change the limits")
        check(parent.request(save, limits(30))[0] == 400, "saving limits requires CSRF")
        check(form(parent, time_page, save, limits(30, rest="5"))[0] == 302
              and sql(f"SELECT CAST(DailyMinutes AS varchar) + N'|' + CAST(BreakEveryMinutes AS varchar) FROM dbo.TimeLimitPolicies WHERE ChildProfileId={kid};") == "30|5",
              "a parent sets the daily limit and break reminder")

        # US-31: counting and stopping
        check(form(parent, "/kids-picker", f"/kids-picker/{kid}")[0] == 302, "the parent opens child mode")
        raw = parent.request(f"/kids-lesson?id={lesson}", raw=True)[2]
        check("window.SL_KID_LEFT = 30" in raw, "the child page receives the minutes left from the server")
        results = [beat(parent, lesson) for _ in range(5)]
        check([result.get("breakDue") for result in results] == [False, False, False, False, True], "a break reminder is due every five minutes of study")
        check(results[-1].get("left") == 25 and sql(f"SELECT Seconds FROM dbo.DailyUsages WHERE ChildProfileId={kid};") == "300", "study time is counted on the server")
        check(sql(f"SELECT ActiveSeconds FROM dbo.LearningSessions WHERE ChildProfileId={kid} AND EndedAt IS NULL;") == "300", "the open session records the study time")
        form(parent, time_page, save, limits(30, today=5))
        check("/kids-time-up" in location(parent, "/kids-home") and "/kids-time-up" in location(parent, f"/kids-lesson?id={lesson}")
              and "/kids-time-up" in location(parent, "/kids-search?q=a"), "when today's minutes are used up every learning page stops")
        check("/kids-time-up" in form(parent, time_page, f"/kids-lesson/{lesson}/next")[1].get("Location", ""), "lesson actions stop too")
        check("hết giờ" in parent.request("/kids-time-up")[2].lower(), "the time-up page explains why")
        now = datetime.now()
        later = lambda hours: (now + timedelta(hours=hours)).strftime("%H:%M")
        form(parent, time_page, save, limits(30, today=120, start=later(2), end=later(3)))
        body = parent.request("/kids-time-up")[2]
        check("ngoài giờ học" in body and "/kids-time-up" in location(parent, "/kids-home"), "outside the study window learning stops")
        # The time-up page has no form outside the window, so the token comes from another page of the same session.
        form(parent, time_page, "/kids-time-up/request", [("minutes", "15")])
        check(sql(f"SELECT COUNT(*) FROM dbo.TimeExtensionRequests WHERE ChildProfileId={kid};") == "0", "extra minutes cannot be asked for outside the window")
        form(parent, time_page, save, limits(30, today=5))

        # US-32: extra time
        form(parent, time_page, "/kids-time-up/request", [("minutes", "7")])
        check(sql(f"SELECT COUNT(*) FROM dbo.TimeExtensionRequests WHERE ChildProfileId={kid};") == "0", "only the offered amounts can be asked for")
        form(parent, time_page, "/kids-time-up/request", [("minutes", "15"), ("reason", "Con muon hoc het bai")])
        check(sql(f"SELECT Status + N'|' + CAST(RequestedMinutes AS varchar) FROM dbo.TimeExtensionRequests WHERE ChildProfileId={kid};") == "Pending|15", "the child asks for more time")
        check(sql(f"SELECT COUNT(*) FROM dbo.Notifications WHERE UserId=N'{parent_id}' AND NotificationType=N'TimeRequest';") == "1", "the parent is notified")
        form(parent, time_page, "/kids-time-up/request", [("minutes", "10")])
        check(sql(f"SELECT COUNT(*) FROM dbo.TimeExtensionRequests WHERE ChildProfileId={kid};") == "1", "only one request waits at a time")
        request = sql(f"SELECT Id FROM dbo.TimeExtensionRequests WHERE ChildProfileId={kid};")
        body = parent.request(time_page, raw=True)[2]
        # The page is read raw, so the base64 row version still carries HTML entities such as &#x2B; for '+'.
        stale = unescape(re.search(rf'/time/requests/{request}"[^>]*>\s*<input type="hidden" name="decision" value="approve" /><input type="hidden" name="rowVersion" value="([^"]+)"', body).group(1))
        check(form(other, "/time", f"/time/requests/{request}", [("decision", "approve"), ("minutes", "15")])[0] == 404, "another family cannot answer the request")
        form(parent, time_page, f"/time/requests/{request}", [("decision", "approve"), ("minutes", "20"), ("rowVersion", stale)])
        check(sql(f"SELECT Status FROM dbo.TimeExtensionRequests WHERE Id={request};") == "Pending", "a parent cannot grant more than was asked")
        form(parent, time_page, f"/time/requests/{request}", [("decision", "approve"), ("minutes", "10"), ("rowVersion", stale)])
        check(sql(f"SELECT Status + N'|' + CAST(GrantedMinutes AS varchar) FROM dbo.TimeExtensionRequests WHERE Id={request};") == "Approved|10", "the parent grants part of the request")
        form(parent, time_page, f"/time/requests/{request}", [("decision", "deny"), ("rowVersion", stale)])
        check(sql(f"SELECT Status FROM dbo.TimeExtensionRequests WHERE Id={request};") == "Approved", "a request is answered only once")
        check(parent.request("/kids-home")[0] == 200 and "Ba mẹ cho con thêm 10 phút" in parent.request("/kids-time-up")[2], "the child can learn again and sees the answer")
        for _ in range(10):
            beat(parent, lesson)
        check("/kids-time-up" in location(parent, "/kids-home"), "the extra minutes run out too")
        form(parent, time_page, "/kids-time-up/request", [("minutes", "5")])
        second = sql(f"SELECT TOP 1 Id FROM dbo.TimeExtensionRequests WHERE ChildProfileId={kid} ORDER BY Id DESC;")
        form(parent, time_page, f"/time/requests/{second}", [("decision", "deny")])
        check(sql(f"SELECT Status FROM dbo.TimeExtensionRequests WHERE Id={second};") == "Denied" and "chưa đồng ý" in parent.request("/kids-time-up")[2], "a denied request is shown to the child")
        form(parent, time_page, "/kids-time-up/request", [("minutes", "5")])
        third = sql(f"SELECT TOP 1 Id FROM dbo.TimeExtensionRequests WHERE ChildProfileId={kid} ORDER BY Id DESC;")
        form(parent, time_page, f"/time/requests/{third}", [("decision", "deny")])
        form(parent, time_page, "/kids-time-up/request", [("minutes", "5")])
        check(sql(f"SELECT COUNT(*) FROM dbo.TimeExtensionRequests WHERE ChildProfileId={kid};") == "3", "a child asks at most three times a day")
        sql(f"UPDATE dbo.TimeExtensionRequests SET Status=N'Pending', ForDate=DATEADD(day, -1, ForDate) WHERE Id={third};")
        parent.request(time_page)
        check(sql(f"SELECT Status FROM dbo.TimeExtensionRequests WHERE Id={third};") == "Expired", "requests left from earlier days expire")
        form(parent, time_page, save, limits(30, today=120))

        # US-33: reports
        page = f"/kids-lesson?id={lesson}"
        form(parent, page, f"/kids-lesson/{lesson}/report", [("reason", "Hack")])
        check(sql(f"SELECT COUNT(*) FROM dbo.ContentReports WHERE LessonId={lesson};") == "0", "a report needs a known reason")
        form(parent, page, f"/kids-lesson/{lesson}/report", [("reason", "Ads"), ("comment", "Co quang cao")])
        form(parent, page, f"/kids-lesson/{lesson}/report", [("reason", "Scary")])
        check(sql(f"SELECT COUNT(*) FROM dbo.ContentReports WHERE LessonId={lesson} AND ChildProfileId={kid} AND Status=N'Open';") == "1", "the child reports a lesson once")
        check(sql(f"SELECT COUNT(*) FROM dbo.Notifications WHERE UserId=N'{parent_id}' AND NotificationType=N'ContentReport';") == "1", "the parent hears about the child's report")
        form(parent, f"/lesson-detail/{lesson}", f"/lesson-detail/{lesson}/report", [("reason", "Wrong"), ("comment", "Sai kien thuc")])
        check(sql(f"SELECT COUNT(*) FROM dbo.ContentReports WHERE LessonId={lesson} AND ReportedById=N'{parent_id}';") == "1", "the parent reports from the library")
        sql(f"INSERT dbo.ContentReports(ReportedById, SourceVideoId, Reason, Status, CreatedAt) VALUES (N'{parent_id}', {video}, N'Inappropriate', N'Open', SYSUTCDATETIME());")

        # US-29: history
        body = parent.request(f"/history?child={kid}")[2]
        check(TITLE in body and "Co quang cao" in body and "Có quảng cáo lạ" in body, "history lists the lessons and the child's reports")
        check(TITLE not in other.request(f"/history?child={kid}")[2], "another family never sees the child's history")
        status, headers, body = parent.request(f"/history/export?child={kid}")
        check(status == 200 and "text/csv" in headers.get("Content-Type", "") and TITLE in body, "history exports as CSV")
        check(other.request(f"/history/export?child={kid}")[0] == 404, "another family cannot export it")

        # US-34: admin handling
        check("/error/403" in location(parent, "/admin-reports"), "parents cannot open report handling")
        body = admin.request("/admin-reports")[2]
        check(TITLE in body and "Be Gio" in body and f"Video bao cao {RUN[:6]}" in body, "admins see reports from children and parents")
        child_report = sql(f"SELECT Id FROM dbo.ContentReports WHERE ChildProfileId={kid};")
        parent_report = sql(f"SELECT Id FROM dbo.ContentReports WHERE ReportedById=N'{parent_id}' AND LessonId={lesson};")
        video_report = sql(f"SELECT Id FROM dbo.ContentReports WHERE SourceVideoId={video};")
        check(form(admin, "/admin-reports", f"/admin-reports/{child_report}", [("action", "keep")])[0] == 302
              and sql(f"SELECT Status FROM dbo.ContentReports WHERE Id={child_report};") == "Open", "closing a report needs a written result")
        form(admin, "/admin-reports", f"/admin-reports/{child_report}", [("action", "review")])
        check(sql(f"SELECT Status FROM dbo.ContentReports WHERE Id={child_report};") == "InReview", "an admin takes a report into review")
        form(admin, "/admin-reports", f"/admin-reports/{child_report}", [("action", "keep"), ("resolution", "Da kiem tra, noi dung phu hop")])
        check(sql(f"SELECT Status FROM dbo.ContentReports WHERE Id={child_report} AND ResolvedAt IS NOT NULL;") == "Dismissed", "the admin keeps the content")
        check("Da kiem tra, noi dung phu hop" in parent.request(f"/history?child={kid}")[2]
              and sql(f"SELECT COUNT(*) FROM dbo.Notifications WHERE UserId=N'{parent_id}' AND NotificationType=N'ContentReport';") == "2", "the family sees the result")
        form(admin, "/admin-reports", f"/admin-reports/{video_report}", [("action", "remove"), ("resolution", "Da chan video")])
        check(sql(f"SELECT Status FROM dbo.SourceVideos WHERE Id={video};") == "Blocked" and sql(f"SELECT Status FROM dbo.ContentReports WHERE Id={video_report};") == "Resolved",
              "removing a reported video blocks it")
        form(admin, "/admin-reports", f"/admin-reports/{parent_report}", [("action", "remove"), ("resolution", "Da go bai")])
        check(sql(f"SELECT Status FROM dbo.Lessons WHERE Id={lesson};") == "Archived", "removing a reported lesson archives it")
        check(parent.request(f"/kids-lesson?id={lesson}")[0] == 404, "the removed lesson leaves child mode")
        check(form(admin, "/admin-reports", f"/admin-reports/{parent_report}", [("action", "keep"), ("resolution", "lai")])[0] == 302
              and sql(f"SELECT Status FROM dbo.ContentReports WHERE Id={parent_report};") == "Resolved", "a handled report cannot be changed again")
        check(sql(f"SELECT COUNT(*) FROM dbo.AuditLogs WHERE ActorId=N'{admin_id}' AND EntityType=N'ContentReport';") == "4", "report handling is audited")
        check(form(parent, "/kids-picker", f"/kids-picker/{kid}")[0] == 302, "child mode still opens after the checks")


def main():
    try:
        with tempfile.TemporaryDirectory(prefix="safelearn-private-mail-") as temporary:
            checks(free_url(), Path(temporary))
    finally:
        emails = f"N'{PARENT}', N'{OTHER}', N'{ADMIN}'"
        ids = f"SELECT Id FROM dbo.Users WHERE Email IN ({emails})"
        lessons = f"SELECT l.Id FROM dbo.Lessons l JOIN dbo.Subjects s ON s.Id=l.SubjectId WHERE s.Code=N'{CODE}'"
        sql(f"DELETE FROM dbo.ContentReports WHERE LessonId IN ({lessons}) OR SourceVideoId IN (SELECT Id FROM dbo.SourceVideos WHERE VideoId=N'{VIDEO}') OR ReportedById IN ({ids});")
        for table in ("Assignments", "LessonActivities", "Favorites"):
            sql(f"DELETE FROM dbo.{table} WHERE LessonId IN ({lessons});")
        sql(f"DELETE FROM dbo.Lessons WHERE Id IN ({lessons});")
        sql(f"DELETE FROM dbo.SourceVideos WHERE VideoId=N'{VIDEO}';")
        sql(f"DELETE FROM dbo.TimeExtensionRequests WHERE DecidedById IN ({ids});")
        sql(f"DELETE FROM dbo.TimeLimitPolicies WHERE UpdatedById IN ({ids});")
        sql(f"DELETE FROM dbo.ChildProfiles WHERE ParentId IN ({ids});")
        sql(f"DELETE FROM dbo.Subjects WHERE Code=N'{CODE}';")
        sql(f"DELETE FROM dbo.AuditLogs WHERE ActorId IN ({ids}) OR EntityId IN ({ids});")
        sql(f"DELETE FROM dbo.LoginHistories WHERE UserNameTried IN ({emails});")
        sql(f"DELETE FROM dbo.Users WHERE Email IN ({emails});")
        check(sql(f"SELECT COUNT(*) FROM dbo.Users WHERE Email IN ({emails});") == "0" and sql(f"SELECT COUNT(*) FROM dbo.Subjects WHERE Code=N'{CODE}';") == "0",
              "generated time and report data is cleaned up")
    print(f"RESULT {smoke.checks}/{smoke.checks} passed (limits + counting + extra time + history + reports)")


if __name__ == "__main__":
    main()
