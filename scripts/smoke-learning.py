"""Exercise Sprint 4 child learning (US-23 to US-28) against the built root app and real SQL.

Uses the existing smoke harness, disposable UUID accounts and private email pickup.
Build first. Removes only the subject, lessons, children, keyword and accounts it creates.
"""

import http.cookiejar
import importlib.util
import json
from pathlib import Path
import re
import socket
import sys
import tempfile
import urllib.error
import urllib.request
import uuid


sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location("auth_smoke", Path(__file__).with_name("smoke-sql-auth.py"))
smoke = importlib.util.module_from_spec(spec)
spec.loader.exec_module(smoke)
check, sql = smoke.check, smoke.sql
# sqlcmd prints in the console code page, so names compared through SQL stay ASCII.

RUN = uuid.uuid4().hex
PARENT = f"sql-smoke-learn-parent-{RUN}@example.invalid"
OTHER = f"sql-smoke-learn-other-{RUN}@example.invalid"
PASSWORD = f"LearnTest1{RUN}"
CODE = f"LS{RUN[:8].upper()}"
KEY = CODE.lower()
KEYWORD = f"cam{RUN[:8]}"
TITLES = {name: f"{name} {RUN[:8]}" for name in ("Alpha", "Beta", "Gamma", "Draft", "Epsilon")}


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


def add_child(client, name, grade):
    body = client.request("/profiles", raw=True)[2]
    token = json.loads(re.search(r'window\.SL_CSRF = ("[^"]+")', body).group(1))
    request = urllib.request.Request(client.url + "/children", data=json.dumps({"name": name, "cls": grade, "subjects": [KEY], "consent": True}).encode(),
                                     headers={"Content-Type": "application/json", "RequestVerificationToken": token})
    with client.opener.open(request, timeout=10) as response:
        return json.loads(response.read())["id"]


def lesson_id(name):
    return sql(f"SELECT Id FROM dbo.Lessons WHERE Title=N'{TITLES[name]}';")


def activity(child, lesson, column):
    return sql(f"SELECT ISNULL(CAST({column} AS nvarchar(40)), N'') FROM dbo.LessonActivities WHERE ChildProfileId={child} AND LessonId={lesson};")


def assignment(child, lesson):
    return sql(f"SELECT TOP 1 Status FROM dbo.Assignments WHERE ChildProfileId={child} AND LessonId={lesson} ORDER BY Id DESC;")


def seed(owner):
    sql(f"INSERT dbo.Subjects(Code, Name, Description, IconKey, ColorKey, SortOrder, IsActive) VALUES (N'{CODE}', N'Mon hoc thu {RUN[:6]}', NULL, N'star', N'teal', 95, 1);")
    sql(f"INSERT dbo.SubjectGrades(SubjectId, GradeLevelId) SELECT s.Id, g.Id FROM dbo.Subjects s CROSS JOIN dbo.GradeLevels g WHERE s.Code=N'{CODE}' AND g.Level IN (2, 3);")
    for name, grade, status in (("Alpha", 2, "Published"), ("Beta", 2, "Published"), ("Gamma", 3, "Published"), ("Draft", 2, "Draft"), ("Epsilon", 3, "Published")):
        sql("INSERT dbo.Lessons(SubjectId, GradeLevelId, Title, Summary, LessonType, DurationMin, SortOrder, Status, CreatedById, CreatedAt, UpdatedAt, PublishedAt) "
            f"SELECT s.Id, g.Id, N'{TITLES[name]}', N'Muc tieu {name}', N'Reading', 8, 1, N'{status}', N'{owner}', SYSUTCDATETIME(), SYSUTCDATETIME(), SYSUTCDATETIME() "
            f"FROM dbo.Subjects s CROSS JOIN dbo.GradeLevels g WHERE s.Code=N'{CODE}' AND g.Level={grade};")
        sql(f"INSERT dbo.LessonContents(LessonId, ContentKind, Title, Body, SortOrder) VALUES ({lesson_id(name)}, N'Reading', N'Doc 1', N'Noi dung {name} phan mot', 1);")
    alpha = lesson_id("Alpha")
    sql(f"INSERT dbo.LessonContents(LessonId, ContentKind, Title, Body, SortOrder) VALUES ({alpha}, N'Reading', N'Doc 2', N'Noi dung Alpha phan hai', 2);")
    video = sql("SELECT TOP 1 v.Id FROM dbo.SourceVideos v WHERE v.Status=N'Approved' AND v.Platform=N'YouTube' "
                "AND EXISTS (SELECT 1 FROM dbo.SourceVideoSources l JOIN dbo.ContentSources c ON c.Id=l.SourceId WHERE l.SourceVideoId=v.Id AND c.Status=N'Approved') ORDER BY v.Id;")
    sql(f"INSERT dbo.LessonContents(LessonId, ContentKind, Title, SourceVideoId, SortOrder) VALUES ({alpha}, N'Video', N'Video', {video}, 3);")
    sql(f"INSERT dbo.BlockedKeywords(Keyword, CreatedById, CreatedAt) VALUES (N'{KEYWORD}', N'{owner}', SYSUTCDATETIME());")
    return sql(f"SELECT VideoId FROM dbo.SourceVideos WHERE Id={video};")


def checks(url, directory):
    with smoke.running_app(url, directory):
        register(directory, url, PARENT, "Learn parent")
        register(directory, url, OTHER, "Learn other")
        parent_id = sql(f"SELECT Id FROM dbo.Users WHERE Email=N'{PARENT}';")
        video = seed(parent_id)
        parent, other = smoke.Client(url), smoke.Client(url)
        for client, email in ((parent, PARENT), (other, OTHER)):
            check(client.post_form("/login", {"Email": email, "Password": PASSWORD})[0] == 302, f"{email.split('-')[3]} signs in")
        # Child mode locks the parent pages on the server: /kids-* pages run on the child's device, the rest on the parent's.
        parent, other = smoke.Devices(parent), smoke.Devices(other)
        kid = add_child(parent, "Be Hoc", 2)
        stranger = add_child(other, "Be Khac", 2)
        alpha, beta, gamma, draft, epsilon = (lesson_id(name) for name in ("Alpha", "Beta", "Gamma", "Draft", "Epsilon"))

        status, headers, _ = parent.request("/kids-home")
        check(status == 302 and "/kids-picker" in headers.get("Location", ""), "child mode starts at the picker")
        body = parent.request("/kids-picker")[2]
        check("Be Hoc" in body and "Be Khac" not in body, "the picker lists only this family's children")
        check(form(parent, "/kids-picker", f"/kids-picker/{stranger}")[0] == 404, "another family's child cannot be chosen")
        check(parent.request(f"/kids-picker/{kid}", {})[0] == 400, "choosing a child requires CSRF")
        status, headers, _ = form(parent, "/kids-picker", f"/kids-picker/{kid}")
        check(status == 302 and headers.get("Location", "").endswith("/kids-home"), "a parent opens child mode for their child")
        forged = smoke.clone(parent)
        for cookie in list(forged.cookies):
            if cookie.name == ".SafeLearnKids.Kid":
                forged.cookies.clear(cookie.domain, cookie.path, cookie.name)
        forged.cookies.set_cookie(http.cookiejar.Cookie(0, ".SafeLearnKids.Kid", str(stranger), None, False, "127.0.0.1", False, False, "/", True, False, None, False, None, None, {}))
        check("/kids-picker" in forged.request("/kids-home")[1].get("Location", ""), "a forged child cookie is refused")

        status, _, body = parent.request("/kids-home")
        check(status == 200 and f"Mon hoc thu {RUN[:6]}" in body and "0/2 bài" in body, "the home page shows the child's subjects with lesson counts")
        check(f'window.SL_KID = "{kid}"' in parent.request("/kids-home", raw=True)[2], "the page tells the scripts which child is learning")
        body = parent.request(f"/kids-subject?s={KEY}")[2]
        check(TITLES["Alpha"] in body and TITLES["Beta"] in body and TITLES["Draft"] not in body and TITLES["Gamma"] not in body,
              "a subject lists published lessons of the child's grade only")
        check(parent.request(f"/kids-lesson?id={draft}")[0] == 404 and parent.request(f"/kids-lesson?id={gamma}")[0] == 404,
              "drafts and other grades cannot be opened")

        detail = f"/lesson-detail/{gamma}"
        check("Be Hoc" in parent.request(detail)[2], "the library offers the parent's children for assignment")
        check(parent.request(f"{detail}/assign", {"childIds": kid})[0] == 400, "assigning requires CSRF")
        form(parent, detail, f"{detail}/assign", [("childIds", kid), ("dueDate", "2020-01-01")])
        check(assignment(kid, gamma) == "", "a due date in the past is refused")
        check(form(parent, detail, f"{detail}/assign", [("childIds", stranger)])[0] == 404 and assignment(stranger, gamma) == "", "parents cannot assign to another family's child")
        check(form(parent, detail, f"{detail}/assign", [("childIds", kid), ("dueDate", "2099-01-01")])[0] == 302 and assignment(kid, gamma) == "", "a due date more than a year away is refused")
        form(parent, detail, f"{detail}/assign", [("childIds", kid), ("dueDate", ""), ("note", "Con hoc truoc thu Bay nhe")])
        check(assignment(kid, gamma) == "Assigned", "a parent assigns a lesson from another grade")
        form(parent, detail, f"{detail}/assign", [("childIds", kid)])
        check(sql(f"SELECT COUNT(*) FROM dbo.Assignments WHERE ChildProfileId={kid} AND LessonId={gamma};") == "1", "assigning twice keeps one active assignment")
        check("Đã giao 1 bé" in parent.request("/library?q=" + RUN[:8])[2], "the library marks assigned lessons")
        check(TITLES["Gamma"] in parent.request("/kids-home")[2], "the assigned lesson appears for the child")
        status, _, body = parent.request(f"/kids-lesson?id={gamma}")
        check(status == 200 and "Con hoc truoc thu Bay nhe" in body, "the assigned lesson opens with the parent's note")
        check(assignment(kid, gamma) == "InProgress" and activity(kid, gamma, "Status") == "Started", "opening it records the start and moves the assignment along")

        page = f"/kids-lesson?id={alpha}"
        raw = parent.request(page, raw=True)[2]
        check("Noi dung Alpha phan mot" in raw and "Noi dung Alpha phan hai" not in raw and f"embed/{video}" not in raw, "the lesson opens on the first part and keeps later parts closed")
        check(form(parent, page, f"/kids-lesson/{alpha}/complete")[0] == 302 and activity(kid, alpha, "Status") == "Started", "a lesson cannot be completed before every part")
        form(parent, page, f"/kids-lesson/{alpha}/next", [("sections", "1")])
        check(activity(kid, alpha, "ProgressPercent") == "33", "finishing a part saves progress, ignoring a tampered part count")
        check("Đã xong 1/3 phần" in parent.request(page)[2] and "Noi dung Alpha phan hai" in parent.request(page)[2], "coming back resumes at the next part")
        form(parent, page, f"/kids-lesson/{alpha}/next")
        check(activity(kid, alpha, "ProgressPercent") == "67" and f"embed/{video}" in parent.request(page, raw=True)[2], "the approved video opens as the third part")
        form(parent, page, f"/kids-lesson/{alpha}/next")
        form(parent, page, f"/kids-lesson/{alpha}/next")
        check(activity(kid, alpha, "ProgressPercent") == "100", "progress never passes 100%")
        form(parent, page, f"/kids-lesson/{alpha}/heartbeat", [("seconds", "500")])
        form(parent, page, f"/kids-lesson/{alpha}/heartbeat", [("seconds", "30")])
        check(activity(kid, alpha, "WatchSeconds") == "90", "study time is added and each report is capped at a minute")
        check(form(parent, page, f"/kids-lesson/{alpha}/complete")[0] == 302 and activity(kid, alpha, "Status") == "Completed"
              and activity(kid, alpha, "CompletedAt") != "", "the child completes the lesson")
        check("1/2 bài" in parent.request("/kids-home")[2], "the subject count includes the finished lesson")
        gamma_page = f"/kids-lesson?id={gamma}"
        form(parent, gamma_page, f"/kids-lesson/{gamma}/next")
        form(parent, gamma_page, f"/kids-lesson/{gamma}/complete")
        check(assignment(kid, gamma) == "Completed" and sql(f"SELECT COUNT(*) FROM dbo.Assignments WHERE ChildProfileId={kid} AND LessonId={gamma} AND CompletedAt IS NOT NULL;") == "1",
              "completing an assigned lesson completes the assignment")
        check("Đã xong" in parent.request(f"/lesson-detail/{gamma}")[2], "the parent sees the assignment finished")

        beta_page = f"/kids-lesson?id={beta}"
        status, headers, _ = form(parent, beta_page, f"/kids-lesson/{beta}/favorite", [("back", "https://evil.example/")])
        check(status == 302 and not headers.get("Location", "").startswith("http") and sql(f"SELECT COUNT(*) FROM dbo.Favorites WHERE ChildProfileId={kid} AND LessonId={beta};") == "1",
              "the child saves a favorite and is never sent off-site")
        check("Bài con yêu thích" in parent.request("/kids-home")[2], "favorites appear on the home page")
        form(parent, beta_page, f"/kids-lesson/{beta}/favorite")
        check(sql(f"SELECT COUNT(*) FROM dbo.Favorites WHERE ChildProfileId={kid} AND LessonId={beta};") == "0", "tapping again removes the favorite")
        check(form(parent, beta_page, f"/kids-lesson/{draft}/favorite")[0] == 404,
              "lessons the child cannot open cannot be favorited")

        body = parent.request(f"/kids-search?q={RUN[:8]}")[2]
        check(TITLES["Alpha"] in body and TITLES["Gamma"] in body and TITLES["Draft"] not in body and TITLES["Epsilon"] not in body,
              "search finds only lessons the child may open")
        check(sql(f"SELECT COUNT(*) FROM dbo.SearchLogs WHERE ChildProfileId={kid} AND WasBlocked=0 AND ResultCount=3;") == "1", "searches are logged with their result count")
        body = parent.request(f"/kids-search?q=xem%20{KEYWORD}")[2]
        check("không dùng để tìm được" in body and sql(f"SELECT COUNT(*) FROM dbo.SearchLogs WHERE ChildProfileId={kid} AND WasBlocked=1;") == "1",
              "a blocked keyword returns nothing and is logged")

        epsilon_detail = f"/lesson-detail/{epsilon}"
        form(parent, epsilon_detail, f"{epsilon_detail}/assign", [("childIds", kid)])
        check(parent.request(f"/kids-lesson?id={epsilon}")[0] == 200, "an assigned lesson can be opened")
        cancel = sql(f"SELECT Id FROM dbo.Assignments WHERE ChildProfileId={kid} AND LessonId={epsilon};")
        check(form(other, "/library", f"/assignments/{cancel}/cancel")[0] == 404 and assignment(kid, epsilon) == "InProgress", "another family cannot cancel the assignment")
        check(form(parent, epsilon_detail, f"/assignments/{cancel}/cancel")[0] == 302 and assignment(kid, epsilon) == "Cancelled", "the parent cancels an assignment")
        check(parent.request(f"/kids-lesson?id={epsilon}")[0] == 404, "a cancelled lesson from another grade closes again")
        check(parent.request(f"/kids-reading?id={beta}")[1].get("Location", "").endswith(f"/kids-lesson?id={beta}"), "old reading links open the lesson page")
        check(parent.request("/kids-picker")[0] == 200 and parent.request("/kids-home")[0] == 200, "switching child at the picker keeps child mode on")
        token = smoke.token_from(parent.request("/kids-exit")[2])
        check(parent.request("/kids-exit", [("password", "wrong"), ("__RequestVerificationToken", token)])[0] == 400 and parent.request("/kids-home")[0] == 200,
              "a wrong password does not end child mode")
        token = smoke.token_from(parent.request("/kids-exit")[2])
        status, headers, _ = parent.request("/kids-exit", [("password", PASSWORD), ("__RequestVerificationToken", token)])
        check(status == 302 and headers.get("Location", "").endswith("/parent-home") and "/kids-picker" in parent.request("/kids-home")[1].get("Location", ""),
              "the account password ends child mode when no PIN is set")


def main():
    try:
        with tempfile.TemporaryDirectory(prefix="safelearn-private-mail-") as temporary:
            checks(free_url(), Path(temporary))
    finally:
        emails = f"N'{PARENT}', N'{OTHER}'"
        ids = f"SELECT Id FROM dbo.Users WHERE Email IN ({emails})"
        lessons = f"SELECT l.Id FROM dbo.Lessons l JOIN dbo.Subjects s ON s.Id=l.SubjectId WHERE s.Code=N'{CODE}'"
        for table in ("Assignments", "LessonActivities", "Favorites"):
            sql(f"DELETE FROM dbo.{table} WHERE LessonId IN ({lessons});")
        sql(f"DELETE FROM dbo.Lessons WHERE Id IN ({lessons});")
        sql(f"DELETE FROM dbo.ChildProfiles WHERE ParentId IN ({ids});")
        sql(f"DELETE FROM dbo.Subjects WHERE Code=N'{CODE}';")
        sql(f"DELETE FROM dbo.BlockedKeywords WHERE Keyword=N'{KEYWORD}';")
        sql(f"DELETE FROM dbo.AuditLogs WHERE ActorId IN ({ids}) OR EntityId IN ({ids});")
        sql(f"DELETE FROM dbo.LoginHistories WHERE UserNameTried IN ({emails});")
        sql(f"DELETE FROM dbo.Users WHERE Email IN ({emails});")
        check(sql(f"SELECT COUNT(*) FROM dbo.Users WHERE Email IN ({emails});") == "0" and sql(f"SELECT COUNT(*) FROM dbo.Subjects WHERE Code=N'{CODE}';") == "0",
              "generated learning data is cleaned up")
    print(f"RESULT {smoke.checks}/{smoke.checks} passed (child pages + assignments + progress + search + favorites)")


if __name__ == "__main__":
    main()
