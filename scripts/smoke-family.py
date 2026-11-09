"""Exercise family lessons (A), lesson requests (B) and the community library (C) on real SQL.

Uses the existing smoke harness, disposable UUID accounts and private email pickup. Build first.
The app adds the new columns and tables at startup (Services/SchemaUpdater.cs). Removes everything it creates.
"""

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
# sqlcmd prints in the console code page, so text compared through SQL stays ASCII.

RUN = uuid.uuid4().hex
AUTHOR = f"sql-smoke-fam-author-{RUN}@example.invalid"
OTHER = f"sql-smoke-fam-other-{RUN}@example.invalid"
ADMIN = f"sql-smoke-fam-admin-{RUN}@example.invalid"
PASSWORD = f"FamTest1{RUN}"
CODE = f"FM{RUN[:8].upper()}"
KEY = CODE.lower()
TAG = RUN[:6]
KEYWORD = f"cam{RUN[:8]}"
VIDEO, PENDING_VIDEO = f"f{RUN[1:11]}", f"p{RUN[3:13]}"


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


def lesson_form(subject, title, grade="2", status=None):
    pairs = [("Title", title), ("Summary", "Muc tieu"), ("SubjectId", subject), ("Grade", grade), ("LessonType", "Reading"), ("DurationMin", "10")]
    return pairs + ([("Status", status)] if status else [])


def question(text, options, correct, kind="SingleChoice", explanation="Giai thich"):
    padded = list(options) + [""] * (4 - len(options))
    return [("Text", text), ("Type", kind), ("Explanation", explanation), ("Correct", str(correct))] + [(f"Options[{i}]", value) for i, value in enumerate(padded)]


def lesson_id(title):
    return sql(f"SELECT Id FROM dbo.Lessons WHERE Title=N'{title}';")


def share_status(lesson):
    return sql(f"SELECT ISNULL(ShareStatus, N'-') FROM dbo.Lessons WHERE Id={lesson};")


def checks(url, directory):
    with smoke.running_app(url, directory):
        check(sql("SELECT CASE WHEN COL_LENGTH(N'dbo.Lessons', N'OwnerParentId') IS NOT NULL AND OBJECT_ID(N'dbo.LessonRequests') IS NOT NULL "
                  "AND OBJECT_ID(N'dbo.LessonRequestVotes') IS NOT NULL THEN 1 ELSE 0 END;") == "1", "the app adds the new columns and tables at startup")
        register(directory, url, AUTHOR, "Family author")
        register(directory, url, OTHER, "Family other")
        register(directory, url, ADMIN, "Family admin")
        admin_id, author_id, other_id = (sql(f"SELECT Id FROM dbo.Users WHERE Email=N'{email}';") for email in (ADMIN, AUTHOR, OTHER))
        sql(f"DELETE FROM dbo.UserRoles WHERE UserId=N'{admin_id}';")
        sql(f"INSERT dbo.UserRoles(UserId, RoleId) SELECT N'{admin_id}', Id FROM dbo.Roles WHERE NormalizedName=N'ADMIN';")
        sql(f"INSERT dbo.Subjects(Code, Name, IconKey, ColorKey, SortOrder, IsActive) VALUES (N'{CODE}', N'Mon gia dinh {TAG}', N'star', N'blue', 90, 1);")
        sql(f"INSERT dbo.SubjectGrades(SubjectId, GradeLevelId) SELECT s.Id, g.Id FROM dbo.Subjects s CROSS JOIN dbo.GradeLevels g WHERE s.Code=N'{CODE}' AND g.Level=2;")
        subject = sql(f"SELECT Id FROM dbo.Subjects WHERE Code=N'{CODE}';")
        sql(f"INSERT dbo.SourceVideos(Platform, VideoId, Title, Status) VALUES (N'YouTube', N'{VIDEO}', N'Video duyet {TAG}', N'Approved'), (N'YouTube', N'{PENDING_VIDEO}', N'Video cho {TAG}', N'Pending');")
        video, pending_video = (sql(f"SELECT Id FROM dbo.SourceVideos WHERE VideoId=N'{item}';") for item in (VIDEO, PENDING_VIDEO))
        sql(f"INSERT dbo.BlockedKeywords(Keyword, CreatedById, CreatedAt) VALUES (N'{KEYWORD}', N'{admin_id}', SYSUTCDATETIME());")
        # A standard lesson, to answer a request with.
        sql("INSERT dbo.Lessons(SubjectId, GradeLevelId, Title, LessonType, DurationMin, SortOrder, Status, CreatedById, CreatedAt, UpdatedAt, PublishedAt) "
            f"SELECT {subject}, g.Id, N'Bai chuan {TAG}', N'Reading', 5, 1, N'Published', N'{admin_id}', SYSUTCDATETIME(), SYSUTCDATETIME(), SYSUTCDATETIME() FROM dbo.GradeLevels g WHERE g.Level=2;")
        standard = lesson_id(f"Bai chuan {TAG}")
        sql(f"INSERT dbo.LessonContents(LessonId, ContentKind, Title, Body, SortOrder) VALUES ({standard}, N'Reading', N'Doc', N'Noi dung', 1);")

        author, other, admin = smoke.Client(url), smoke.Client(url), smoke.Client(url)
        for client, email in ((author, AUTHOR), (other, OTHER), (admin, ADMIN)):
            check(client.post_form("/login", {"Email": email, "Password": PASSWORD})[0] == 302, f"{email.split('-')[3]} signs in")
        author_kid, other_kid = add_child(author, "Be Tac Gia"), add_child(other, "Be Khac")
        author, other = smoke.Devices(author), smoke.Devices(other)

        # ---------- A: family lessons ----------
        new = "/family-lessons/new"
        check(author.request("/family-lessons")[0] == 200 and admin.request("/family-lessons")[0] != 200, "parents have a family lesson space, admins do not")
        check(form(author, new, new, lesson_form(subject, f"Bai {KEYWORD}"))[0] == 200 and lesson_id(f"Bai {KEYWORD}") == "", "a blocked keyword in the title is refused")
        check(form(author, new, new, lesson_form(subject, f"Lop sai {TAG}", grade="4"))[0] == 200 and lesson_id(f"Lop sai {TAG}") == "", "the subject must offer the chosen grade")
        title = f"Bai gia dinh {TAG}"
        status, headers, _ = form(author, new, new, lesson_form(subject, title))
        lesson = lesson_id(title)
        check(status == 302 and headers.get("Location", "").endswith(f"/family-lessons/{lesson}")
              and sql(f"SELECT Status + N'|' + CASE WHEN OwnerParentId=N'{author_id}' THEN N'mine' ELSE N'?' END FROM dbo.Lessons WHERE Id={lesson};") == "Draft|mine",
              "a parent creates a family lesson as a draft they own")
        editor = f"/family-lessons/{lesson}"
        form(author, editor, editor, lesson_form(subject, title, status="Published"))
        check(sql(f"SELECT Status FROM dbo.Lessons WHERE Id={lesson};") == "Draft", "a lesson without content cannot be given to children")
        contents = lambda: sql(f"SELECT COUNT(*) FROM dbo.LessonContents WHERE LessonId={lesson};")
        form(author, editor, f"{editor}/contents", [("Kind", "Reading"), ("Title", "Phan doc"), ("Body", f"Co {KEYWORD} o day")])
        check(contents() == "0", "blocked keywords are checked in the reading text")
        form(author, editor, f"{editor}/contents", [("Kind", "Reading"), ("Title", "Phan doc"), ("Body", "Hom nay con hoc dem den muoi.")])
        form(author, editor, f"{editor}/contents", [("Kind", "Video"), ("VideoId", pending_video)])
        check(contents() == "1", "a video not yet approved cannot be added")
        form(author, editor, f"{editor}/contents", [("Kind", "Video"), ("VideoId", video)])
        check(contents() == "2", "the parent adds a reading and an approved video")
        form(author, editor, editor, lesson_form(subject, title, status="Published"))
        check(sql(f"SELECT Status FROM dbo.Lessons WHERE Id={lesson};") == "Published", "the parent gives the lesson to the children")
        form(author, editor, f"{editor}/questions", question("Mot cau", ["A"], 0))
        check(sql(f"SELECT COUNT(*) FROM dbo.Questions WHERE LessonId={lesson};") == "0", "a question needs at least two answers")
        form(author, editor, f"{editor}/questions", question("Hai cong hai", ["3", "4", "5"], 1))
        form(author, editor, f"{editor}/questions", question("Meo co bon chan", [], 0, kind="TrueFalse"))
        quiz = sql(f"SELECT Id FROM dbo.Quizzes WHERE LessonId={lesson};")
        check(quiz != "" and sql(f"SELECT Status + N'|' + CAST((SELECT COUNT(*) FROM dbo.QuizQuestions WHERE QuizId={quiz}) AS varchar) FROM dbo.Quizzes WHERE Id={quiz};") == "Published|2",
              "the lesson's questions become its quiz automatically")

        # Privacy: only the family sees it.
        check(title in author.request("/library?source=family")[2] and "Bài của gia đình" in author.request(f"/lesson-detail/{lesson}")[2],
              "the family finds its lesson in the library, marked as its own")
        check(title not in other.request("/library")[2] and other.request(f"/lesson-detail/{lesson}")[0] == 404, "another family can neither list nor open it")
        check(admin.request(f"/admin-lesson/{lesson}")[0] == 404 and title not in admin.request(f"/admin-catalog?q={TAG}")[2]
              and title not in admin.request("/admin-stats?days=30")[2], "admins do not see private family lessons in the catalog or statistics")
        check(other.request(editor)[0] == 404 and form(other, "/family-lessons", editor, lesson_form(subject, "Chiem quyen", status="Draft"))[0] == 404,
              "another family cannot open or change it")
        form(author, "/kids-picker", f"/kids-picker/{author_kid}")
        form(other, "/kids-picker", f"/kids-picker/{other_kid}")
        check(title in author.request(f"/kids-subject?s={KEY}")[2] and author.request(f"/kids-lesson?id={lesson}")[0] == 200, "the family's child sees and opens the lesson")
        check(other.request(f"/kids-lesson?id={lesson}")[0] == 404 and other.request(f"/kids-quiz?id={quiz}")[0] == 404, "another child cannot open the lesson or its quiz")
        author.request(f"/kids-quiz?id={quiz}")
        attempt = sql(f"SELECT MAX(Id) FROM dbo.QuizAttempts WHERE QuizId={quiz};")
        q1 = sql(f"SELECT Id FROM dbo.Questions WHERE LessonId={lesson} AND Text=N'Hai cong hai';")
        right = sql(f"SELECT Id FROM dbo.QuestionOptions WHERE QuestionId={q1} AND IsCorrect=1;")
        form(author, "/kids-home", f"/kids-quiz/{attempt}/submit", [(f"q{q1}", right)])
        check(sql(f"SELECT CorrectCount FROM dbo.QuizAttempts WHERE Id={attempt};") == "1", "the child takes the family quiz and is scored")
        form(author, editor, f"{editor}/questions/{q1}/delete")
        check(sql(f"SELECT Status FROM dbo.Questions WHERE Id={q1};") == "Archived", "a question the child answered is archived, not deleted")
        form(author, f"/kids-lesson?id={lesson}", f"/kids-lesson/{lesson}/report", [("reason", "Wrong")])
        check(sql(f"SELECT COUNT(*) FROM dbo.ContentReports WHERE LessonId={lesson};") == "1" and title not in admin.request("/admin-reports")[2],
              "a child's report on a private family lesson goes to the family, not the admins")

        # ---------- B: lesson requests ----------
        requests = "/lesson-requests"
        topic = f"Phan so {TAG}"
        made = lambda: sql(f"SELECT COUNT(*) FROM dbo.LessonRequests WHERE Topic=N'{topic}';")
        check(form(other, requests, requests, [("SubjectId", subject), ("Grade", "2"), ("Topic", f"Hoc {KEYWORD}")])[0] == 400, "a blocked keyword in a request is refused")
        check(form(other, requests, requests, [("SubjectId", subject), ("Grade", "2"), ("Topic", topic), ("Note", "Con con yeu")])[0] == 302 and made() == "1",
              "a parent asks for a lesson")
        check(form(other, requests, requests, [("SubjectId", subject), ("Grade", "2"), ("Topic", topic)])[0] == 400 and made() == "1", "the same request is not sent twice")
        request = sql(f"SELECT Id FROM dbo.LessonRequests WHERE Topic=N'{topic}';")
        check(topic in author.request(requests)[2], "other families see the request")
        votes = lambda: sql(f"SELECT COUNT(*) FROM dbo.LessonRequestVotes WHERE RequestId={request};")
        form(author, requests, f"{requests}/{request}/vote")
        check(votes() == "1", "another family adds 'we need this too'")
        form(author, requests, f"{requests}/{request}/vote")
        check(votes() == "0", "voting again takes it back")
        form(author, requests, f"{requests}/{request}/vote")
        check(form(other, requests, f"{requests}/{request}/vote")[0] == 404, "a family cannot vote for its own request")
        check(author.request("/admin-lesson-requests")[0] != 200 and topic in admin.request("/admin-lesson-requests")[2] and "2 gia đình cần" in admin.request("/admin-lesson-requests")[2],
              "admins see requests ranked by how many families need them")
        handle = f"/admin-lesson-requests/{request}"
        form(admin, "/admin-lesson-requests", handle, [("status", "Declined")])
        check(sql(f"SELECT Status FROM dbo.LessonRequests WHERE Id={request};") == "Open", "declining needs a reason")
        form(admin, "/admin-lesson-requests", handle, [("status", "Done"), ("lessonId", lesson)])
        check(sql(f"SELECT Status FROM dbo.LessonRequests WHERE Id={request};") == "Open", "a private family lesson cannot answer a request")
        form(admin, "/admin-lesson-requests", handle, [("status", "Done"), ("lessonId", standard), ("reply", "Da co bai")])
        check(sql(f"SELECT Status + N'|' + CAST(LessonId AS varchar) FROM dbo.LessonRequests WHERE Id={request};") == f"Done|{standard}", "the admin answers with a lesson")
        check(sql(f"SELECT COUNT(DISTINCT UserId) FROM dbo.Notifications WHERE UserId IN (N'{author_id}', N'{other_id}') AND NotificationType=N'System' AND LinkUrl=N'/lesson-detail/{standard}';") == "2",
              "the family that asked and the family that voted are both told")
        form(other, requests, f"{requests}/{request}/withdraw")
        check(made() == "1", "a handled request cannot be withdrawn")
        form(other, requests, requests, [("SubjectId", subject), ("Grade", "2"), ("Topic", f"Rut {TAG}")])
        mine = sql(f"SELECT Id FROM dbo.LessonRequests WHERE Topic=N'Rut {TAG}';")
        form(other, requests, f"{requests}/{mine}/withdraw")
        check(sql(f"SELECT COUNT(*) FROM dbo.LessonRequests WHERE Id={mine};") == "0", "an open request can be withdrawn")

        # ---------- C: community library ----------
        share = f"{editor}/share"
        form(author, editor, share)
        check(share_status(lesson) == "-", "sharing needs the parent's agreement")
        form(author, editor, share, [("agree", "true")])
        check(share_status(lesson) == "Pending" and other.request(f"/lesson-detail/{lesson}")[0] == 404, "a shared lesson waits for review and stays private")
        check(title in admin.request("/admin-community")[2] and "Hom nay con hoc dem den muoi." in admin.request(f"/admin-community/{lesson}")[2],
              "the admin sees the queue and previews the lesson")
        form(admin, f"/admin-community/{lesson}", f"/admin-community/{lesson}", [("decision", "reject")])
        check(share_status(lesson) == "Pending", "rejecting needs a reason")
        form(admin, f"/admin-community/{lesson}", f"/admin-community/{lesson}", [("decision", "reject"), ("note", "Them giai thich")])
        check(share_status(lesson) == "Rejected" and sql(f"SELECT COUNT(*) FROM dbo.Notifications WHERE UserId=N'{author_id}' AND LinkUrl=N'/family-lessons/{lesson}#share';") == "1",
              "the admin rejects with a reason and the author is told")
        form(author, editor, share, [("agree", "true")])
        form(admin, f"/admin-community/{lesson}", f"/admin-community/{lesson}", [("decision", "approve")])
        check(share_status(lesson) == "Approved", "after another try the admin approves it")
        page = other.request("/library?source=community")[2]
        check(title in page and "Family author" in page and other.request(f"/lesson-detail/{lesson}")[0] == 200, "every family now finds it in the library with the author's name")
        check(other.request(f"/kids-lesson?id={lesson}")[0] == 200 and other.request(f"/kids-quiz?id={quiz}")[0] == 200, "other children can open it and take its quiz")
        form(author, editor, editor, lesson_form(subject, title + " v2", status="Published"))
        check(share_status(lesson) == "Pending" and other.request(f"/lesson-detail/{lesson}")[0] == 404, "changing an approved lesson sends it back for review")
        form(admin, f"/admin-community/{lesson}", f"/admin-community/{lesson}", [("decision", "approve")])
        form(other, f"/kids-lesson?id={lesson}", f"/kids-lesson/{lesson}/report", [("reason", "Ads")])
        report = sql(f"SELECT Id FROM dbo.ContentReports WHERE LessonId={lesson} AND ChildProfileId={other_kid};")
        check(f"{title} v2" in admin.request("/admin-reports")[2], "reports on a community lesson reach the admins")
        form(admin, "/admin-reports", f"/admin-reports/{report}", [("action", "remove"), ("resolution", "Go khoi cong dong")])
        check(share_status(lesson) == "Rejected" and sql(f"SELECT Status FROM dbo.Lessons WHERE Id={lesson};") == "Published",
              "removing a reported community lesson takes it out of the community; the family keeps it")
        check(author.request(f"/kids-lesson?id={lesson}")[0] == 200 and other.request(f"/kids-lesson?id={lesson}")[0] == 404, "the author's child still learns it, other children do not")
        form(author, editor, share, [("agree", "true")])
        form(admin, f"/admin-community/{lesson}", f"/admin-community/{lesson}", [("decision", "approve")])
        form(admin, f"/admin-community/{lesson}", f"/admin-community/{lesson}", [("decision", "withdraw")])
        check(share_status(lesson) == "Approved", "withdrawing needs a reason")
        form(admin, f"/admin-community/{lesson}", f"/admin-community/{lesson}", [("decision", "withdraw"), ("note", "Noi dung cu")])
        check(share_status(lesson) == "Rejected", "the admin can withdraw an approved lesson")
        form(author, editor, share, [("agree", "true")])
        form(author, editor, f"{editor}/unshare")
        check(share_status(lesson) == "-", "the author can stop sharing")

        # Deleting: used lessons are hidden, unused ones removed.
        form(author, editor, f"{editor}/delete")
        check(sql(f"SELECT Status FROM dbo.Lessons WHERE Id={lesson};") == "Archived", "a lesson children used is hidden instead of deleted")
        form(author, new, new, lesson_form(subject, f"Bai nhap {TAG}"))
        draft = lesson_id(f"Bai nhap {TAG}")
        form(author, f"/family-lessons/{draft}", f"/family-lessons/{draft}/delete")
        check(lesson_id(f"Bai nhap {TAG}") == "", "an unused lesson is deleted")
        check(sql(f"SELECT COUNT(*) FROM dbo.AuditLogs WHERE ActorId=N'{author_id}' AND Action LIKE N'FamilyLesson%';") != "0"
              and sql(f"SELECT COUNT(*) FROM dbo.AuditLogs WHERE ActorId=N'{admin_id}' AND Action LIKE N'Community%';") != "0", "family and community actions are audited")


def main():
    try:
        with tempfile.TemporaryDirectory(prefix="safelearn-private-mail-") as temporary:
            checks(free_url(), Path(temporary))
    finally:
        emails = f"N'{AUTHOR}', N'{OTHER}', N'{ADMIN}'"
        ids = f"SELECT Id FROM dbo.Users WHERE Email IN ({emails})"
        lessons = f"SELECT Id FROM dbo.Lessons WHERE SubjectId IN (SELECT Id FROM dbo.Subjects WHERE Code=N'{CODE}')"
        quizzes = f"SELECT Id FROM dbo.Quizzes WHERE LessonId IN ({lessons})"
        for statement in (
                f"DELETE FROM dbo.LessonRequestVotes WHERE ParentId IN ({ids});", f"DELETE FROM dbo.LessonRequests WHERE ParentId IN ({ids});",
                f"DELETE FROM dbo.QuizAttemptAnswers WHERE QuizId IN ({quizzes});", f"DELETE FROM dbo.QuizAttempts WHERE QuizId IN ({quizzes});",
                f"DELETE FROM dbo.Assignments WHERE QuizId IN ({quizzes}) OR LessonId IN ({lessons});", f"DELETE FROM dbo.QuizQuestions WHERE QuizId IN ({quizzes});",
                f"DELETE FROM dbo.Quizzes WHERE Id IN ({quizzes});",
                f"DELETE FROM dbo.QuestionOptions WHERE QuestionId IN (SELECT Id FROM dbo.Questions WHERE LessonId IN ({lessons}));",
                f"DELETE FROM dbo.Questions WHERE LessonId IN ({lessons});",
                *[f"DELETE FROM dbo.{table} WHERE LessonId IN ({lessons});" for table in ("ContentReports", "LessonActivities", "Favorites", "LessonContents")],
                f"DELETE FROM dbo.Lessons WHERE Id IN ({lessons});",
                f"DELETE FROM dbo.SourceVideos WHERE VideoId IN (N'{VIDEO}', N'{PENDING_VIDEO}');", f"DELETE FROM dbo.BlockedKeywords WHERE Keyword=N'{KEYWORD}';",
                f"DELETE FROM dbo.Notifications WHERE UserId IN ({ids});",
                *[f"DELETE FROM dbo.{table} WHERE ChildProfileId IN (SELECT Id FROM dbo.ChildProfiles WHERE ParentId IN ({ids}));"
                  for table in ("DailyUsages", "LearningSessions", "Streaks", "SubjectProgresses")],
                f"DELETE FROM dbo.ChildProfiles WHERE ParentId IN ({ids});",
                f"DELETE FROM dbo.SubjectGrades WHERE SubjectId IN (SELECT Id FROM dbo.Subjects WHERE Code=N'{CODE}');", f"DELETE FROM dbo.Subjects WHERE Code=N'{CODE}';",
                f"DELETE FROM dbo.AuditLogs WHERE ActorId IN ({ids}) OR EntityId IN ({ids});", f"DELETE FROM dbo.LoginHistories WHERE UserNameTried IN ({emails});",
                f"DELETE FROM dbo.Users WHERE Email IN ({emails});"):
            sql(statement)
        check(sql(f"SELECT COUNT(*) FROM dbo.Users WHERE Email IN ({emails});") == "0" and sql(f"SELECT COUNT(*) FROM dbo.Subjects WHERE Code=N'{CODE}';") == "0",
              "generated family data is cleaned up")
    print(f"RESULT {smoke.checks}/{smoke.checks} passed (family lessons + lesson requests + community library)")


if __name__ == "__main__":
    main()
