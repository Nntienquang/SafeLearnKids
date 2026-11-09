"""Exercise Sprint 7 (US-41 to US-46): subject progress, learning paths, badges, streaks, weekly goals and review, on real SQL.

Uses the existing smoke harness, disposable UUID accounts and private email pickup.
Build first. Removes only the subject, lessons, quiz, paths, badges, children and accounts it creates.
"""

from datetime import date, timedelta
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
PARENT = f"sql-smoke-prog-parent-{RUN}@example.invalid"
OTHER = f"sql-smoke-prog-other-{RUN}@example.invalid"
ADMIN = f"sql-smoke-prog-admin-{RUN}@example.invalid"
PASSWORD = f"ProgTest1{RUN}"
CODE = f"PG{RUN[:8].upper()}"
KEY = CODE.lower()
TAG = RUN[:6]
BADGE = f"T{RUN[:8].upper()}"
PATH = f"Lo trinh {TAG}"
TODAY = date.today()
MONDAY = TODAY - timedelta(days=TODAY.weekday())


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
    request = urllib.request.Request(client.url + "/children", data=json.dumps({"name": name, "cls": 2, "subjects": [KEY], "consent": True}).encode(),
                                     headers={"Content-Type": "application/json", "RequestVerificationToken": token})
    with client.opener.open(request, timeout=10) as response:
        return json.loads(response.read())["id"]


def steps(lessons, picks):
    """lessons: ids in page order; picks: {lesson_id: (order, required)}"""
    pairs = []
    for i, lesson in enumerate(lessons):
        include = lesson in picks
        order, required = picks.get(lesson, (i + 1, True))
        pairs += [(f"Steps[{i}].LessonId", str(lesson)), (f"Steps[{i}].Title", "x"), (f"Steps[{i}].Include", "true" if include else "false"),
                  (f"Steps[{i}].Required", "true" if required else "false"), (f"Steps[{i}].Order", str(order))]
    return pairs


def badge(code, name, criteria, threshold, icon="star"):
    return [("Code", code), ("Name", name), ("Description", f"Mo ta {name}"), ("IconKey", icon), ("CriteriaType", criteria), ("Threshold", str(threshold)), ("IsActive", "true")]


def learn(client, lesson):
    page = f"/kids-lesson?id={lesson}"
    form(client, page, f"/kids-lesson/{lesson}/next")
    return form(client, page, f"/kids-lesson/{lesson}/complete")


def beat(client, lesson, seconds=60):
    status, _, body = form(client, f"/kids-lesson?id={lesson}", f"/kids-lesson/{lesson}/heartbeat", [("seconds", str(seconds))])
    return json.loads(body) if status == 200 else {"status": status}


def checks(url, directory):
    with smoke.running_app(url, directory):
        register(directory, url, PARENT, "Progress parent")
        register(directory, url, OTHER, "Progress other")
        register(directory, url, ADMIN, "Progress admin")
        admin_id = sql(f"SELECT Id FROM dbo.Users WHERE Email=N'{ADMIN}';")
        sql(f"DELETE FROM dbo.UserRoles WHERE UserId=N'{admin_id}';")
        sql(f"INSERT dbo.UserRoles(UserId, RoleId) SELECT N'{admin_id}', Id FROM dbo.Roles WHERE NormalizedName=N'ADMIN';")
        sql(f"INSERT dbo.Subjects(Code, Name, IconKey, ColorKey, SortOrder, IsActive) VALUES (N'{CODE}', N'Mon tien do {TAG}', N'star', N'blue', 93, 1);")
        sql(f"INSERT dbo.SubjectGrades(SubjectId, GradeLevelId) SELECT s.Id, g.Id FROM dbo.Subjects s CROSS JOIN dbo.GradeLevels g WHERE s.Code=N'{CODE}' AND g.Level IN (2, 4);")
        for name, level, order in (("A", 2, 1), ("B", 2, 2), ("C", 2, 3), ("D", 4, 1)):
            sql("INSERT dbo.Lessons(SubjectId, GradeLevelId, Title, LessonType, DurationMin, SortOrder, Status, CreatedById, CreatedAt, UpdatedAt, PublishedAt) "
                f"SELECT s.Id, g.Id, N'Bai {name} {TAG}', N'Reading', 5, {order}, N'Published', N'{admin_id}', SYSUTCDATETIME(), SYSUTCDATETIME(), SYSUTCDATETIME() "
                f"FROM dbo.Subjects s CROSS JOIN dbo.GradeLevels g WHERE s.Code=N'{CODE}' AND g.Level={level};")
        a, b, c, d = (sql(f"SELECT Id FROM dbo.Lessons WHERE Title=N'Bai {name} {TAG}';") for name in "ABCD")
        for lesson in (a, b, c, d):
            sql(f"INSERT dbo.LessonContents(LessonId, ContentKind, Title, Body, SortOrder) VALUES ({lesson}, N'Reading', N'Doc', N'Noi dung', 1);")
        subject = sql(f"SELECT Id FROM dbo.Subjects WHERE Code=N'{CODE}';")

        parent, other, admin = smoke.Client(url), smoke.Client(url), smoke.Client(url)
        for client, email in ((parent, PARENT), (other, OTHER), (admin, ADMIN)):
            check(client.post_form("/login", {"Email": email, "Password": PASSWORD})[0] == 302, f"{email.split('-')[3]} signs in")
        # Child mode locks the parent pages on the server: /kids-* pages run on the child's device, the rest on the parent's.
        parent, other = smoke.Devices(parent), smoke.Devices(other)
        kid = add_child(parent, "Be Tien")
        other_kid = add_child(other, "Be Khac")

        # US-42 (admin): paths
        new_path = f"/admin-paths/new?subject={subject}&grade=2"
        check(admin.request("/admin-paths")[0] == 200 and parent.request("/admin-paths")[0] != 200, "only admins manage paths")
        page = admin.request(new_path)[2]
        check(f"Bai A {TAG}" in page and f"Bai D {TAG}" not in page, "the path form lists the subject's published lessons for the grade")
        base = [("SubjectId", subject), ("Grade", "2"), ("Name", PATH), ("Description", "Muc tieu"), ("IsActive", "true")]
        paths = lambda: sql(f"SELECT COUNT(*) FROM dbo.LearningPaths WHERE Name=N'{PATH}';")
        check(form(admin, new_path, "/admin-paths/new", base + steps([a, b, c], {}))[0] == 200 and paths() == "0", "a path needs at least one lesson")
        check(form(admin, new_path, "/admin-paths/new", base + steps([a, d], {a: (1, True), d: (2, True)}))[0] == 200 and paths() == "0",
              "a lesson from another grade cannot be added")
        check(form(admin, new_path, "/admin-paths/new", base + steps([a, b], {a: (1, False), b: (2, False)}))[0] == 200 and paths() == "0",
              "a path needs at least one required lesson")
        check(form(admin, new_path, "/admin-paths/new", base + steps([a, b, c], {b: (1, True), a: (2, True)}))[0] == 302 and paths() == "1", "the admin creates a path")
        path = sql(f"SELECT Id FROM dbo.LearningPaths WHERE Name=N'{PATH}';")
        order = lambda: sql(f"SELECT STRING_AGG(CAST(LessonId AS varchar) + N':' + CAST(IsRequired AS varchar), N',') WITHIN GROUP (ORDER BY SortOrder) FROM dbo.LearningPathItems WHERE PathId={path};")
        check(order() == f"{b}:1,{a}:1", "lessons are stored in the chosen order")
        form(admin, f"/admin-paths/{path}", f"/admin-paths/{path}", base + steps([b, a, c], {b: (1, True), a: (2, True), c: (3, False)}))
        check(order() == f"{b}:1,{a}:1,{c}:0", "the admin adds an optional lesson at the end")
        sql(f"INSERT dbo.LearningPaths(Name, SubjectId, GradeLevelId, IsActive) SELECT N'Lop bon {TAG}', {subject}, Id, 1 FROM dbo.GradeLevels WHERE Level=4;")
        far_path = sql(f"SELECT Id FROM dbo.LearningPaths WHERE Name=N'Lop bon {TAG}';")
        sql(f"INSERT dbo.LearningPathItems(PathId, LessonId, SortOrder, IsRequired) VALUES ({far_path}, {d}, 1, 1);")
        check(sql(f"SELECT COUNT(*) FROM dbo.AuditLogs WHERE ActorId=N'{admin_id}' AND Action IN (N'PathCreate', N'PathUpdate');") == "2", "path changes are audited")

        # US-43 (admin): badges. The demo badges stay active; their thresholds are above what this run reaches.
        badges = "/admin-badges/new"
        check(form(admin, badges, badges, badge(BADGE + "L", "Bai dau", "LessonsCompleted", 1, icon="nope"))[0] == 200, "a badge needs a known icon")
        for suffix, criteria, threshold in (("L", "LessonsCompleted", 1), ("M", "MinutesTotal", 1), ("S", "StreakDays", 2), ("Q", "QuizPerfect", 1)):
            check(form(admin, badges, badges, badge(BADGE + suffix, f"Huy {suffix} {TAG}", criteria, threshold))[0] == 302, f"the admin adds a {criteria} badge")
        check(form(admin, badges, badges, badge(BADGE + "L", "Trung", "LessonsCompleted", 3))[0] == 200
              and sql(f"SELECT COUNT(*) FROM dbo.Badges WHERE Code LIKE N'{BADGE}%';") == "4", "badge codes are unique")

        # US-42/45 (parent): follow the path, set a goal
        progress = f"/progress?child={kid}"
        page = parent.request(progress)[2]
        check(PATH in page and f"Lop bon {TAG}" not in page and "Chọn cho bé" in page, "the parent sees paths for the child's grade and subjects")
        check(form(other, "/progress", f"/progress/{kid}/paths/{path}")[0] == 404, "another family cannot choose a path for this child")
        check(form(parent, progress, f"/progress/{kid}/paths/{far_path}")[0] == 404, "a path for another grade cannot be chosen")
        check(form(parent, progress, f"/progress/{kid}/paths/{path}")[0] == 302 and sql(f"SELECT COUNT(*) FROM dbo.Streaks WHERE ChildProfileId={kid};") == "0"
              and sql(f"SELECT Status + N'|' + CAST(CurrentItemId AS varchar) FROM dbo.ChildPaths WHERE ChildProfileId={kid};")
              == "InProgress|" + sql(f"SELECT Id FROM dbo.LearningPathItems WHERE PathId={path} AND LessonId={b};"), "the parent chooses the path; the first lesson is next and no study day is counted")
        goal = f"/progress/{kid}/goal"
        check(form(parent, progress, goal, [("TargetLessons", ""), ("TargetMinutes", "")])[0] == 302
              and sql(f"SELECT COUNT(*) FROM dbo.LearningGoals WHERE ChildProfileId={kid};") == "0", "an empty goal is not saved")
        check(form(parent, progress, goal, [("TargetLessons", "500")])[0] == 302 and sql(f"SELECT COUNT(*) FROM dbo.LearningGoals WHERE ChildProfileId={kid};") == "0",
              "goal numbers are limited")
        check(form(other, "/progress", goal, [("TargetLessons", "2")])[0] == 404, "another family cannot set this child's goal")
        check(form(parent, progress, goal, [("TargetLessons", "2"), ("TargetMinutes", "1")])[0] == 302
              and sql(f"SELECT CONVERT(varchar, WeekStart, 23) + N'|' + CAST(TargetLessons AS varchar) + N'|' + CAST(TargetMinutes AS varchar) FROM dbo.LearningGoals WHERE ChildProfileId={kid};")
              == f"{MONDAY.isoformat()}|2|1", "the parent sets this week's goal starting on Monday")
        form(parent, progress, goal, [("TargetLessons", "2"), ("TargetMinutes", "")])
        check(sql(f"SELECT CAST(TargetLessons AS varchar) + N'|' + ISNULL(CAST(TargetMinutes AS varchar), N'-') FROM dbo.LearningGoals WHERE ChildProfileId={kid};") == "2|-",
              "the goal can be changed during the week")

        # Child: paths, lessons, badges, streak
        check(form(parent, "/kids-picker", f"/kids-picker/{kid}")[0] == 302, "the parent opens child mode")
        home = parent.request("/kids-home")[2]
        check("Lộ trình của con" in home and f"Bai B {TAG}" in home and "Mục tiêu tuần này" in home, "the child sees the path's next lesson and the weekly goal")
        check(learn(parent, b)[0] == 302, "the child finishes the first lesson")
        check(sql(f"SELECT CAST(CurrentDays AS varchar) + N'|' + CAST(LongestDays AS varchar) + N'|' + CONVERT(varchar, LastActiveDate, 23) FROM dbo.Streaks WHERE ChildProfileId={kid};")
              == f"1|1|{TODAY.isoformat()}", "studying starts a streak")
        check(sql(f"SELECT CAST(CompletedLessons AS varchar) + N'|' + CAST(TotalLessons AS varchar) FROM dbo.SubjectProgresses WHERE ChildProfileId={kid} AND SubjectId={subject};") == "1|3",
              "subject progress counts lessons of the child's grade")
        check(sql(f"SELECT CAST(CurrentItemId AS varchar) FROM dbo.ChildPaths WHERE ChildProfileId={kid};") == sql(f"SELECT Id FROM dbo.LearningPathItems WHERE PathId={path} AND LessonId={a};"),
              "the path moves to the next lesson")
        earned = lambda: sql(f"SELECT COUNT(*) FROM dbo.ChildBadges cb JOIN dbo.Badges b ON b.Id=cb.BadgeId WHERE cb.ChildProfileId={kid} AND b.Code LIKE N'{BADGE}%';")
        check(earned() == "1" and "Huy hiệu mới" in parent.request(f"/kids-lesson?id={b}")[2], "the first finished lesson earns the lesson badge and the child is told")
        check(sql(f"SELECT COUNT(*) FROM dbo.Notifications n JOIN dbo.Users u ON u.Id=n.UserId WHERE u.Email=N'{PARENT}' AND n.NotificationType=N'Badge';") == "1", "the parent is told about the badge")
        result = beat(parent, b)
        check(f"Huy M {TAG}" in result.get("badges", []) and earned() == "2", "a minute of study earns the minutes badge through the heartbeat")
        check(beat(parent, b).get("badges") == [] and earned() == "2", "a badge is awarded only once")
        learn(parent, a)
        check(sql(f"SELECT Status FROM dbo.ChildPaths WHERE ChildProfileId={kid};") == "Completed", "the path is complete when every required lesson is done")
        check(sql(f"SELECT CASE WHEN CurrentItemId IS NULL AND CompletedAt IS NOT NULL THEN 1 ELSE 0 END FROM dbo.ChildPaths WHERE ChildProfileId={kid};") == "1", "a completed path has no next step")

        # US-44: streak rules
        sql(f"UPDATE dbo.Streaks SET CurrentDays=1, LongestDays=1, LastActiveDate=DATEADD(day, -1, CAST(GETDATE() AS date)) WHERE ChildProfileId={kid};")
        beat(parent, a)
        check(sql(f"SELECT CAST(CurrentDays AS varchar) + N'|' + CAST(LongestDays AS varchar) FROM dbo.Streaks WHERE ChildProfileId={kid};") == "2|2" and earned() == "3",
              "studying the next day grows the streak and earns the streak badge")
        beat(parent, a)
        check(sql(f"SELECT CurrentDays FROM dbo.Streaks WHERE ChildProfileId={kid};") == "2", "the same day counts once")
        sql(f"UPDATE dbo.Streaks SET LastActiveDate=DATEADD(day, -3, CAST(GETDATE() AS date)) WHERE ChildProfileId={kid};")
        check("Chưa có chuỗi ngày học" in parent.request("/kids-badges")[2], "a missed day shows the streak as broken")
        beat(parent, a)
        check(sql(f"SELECT CAST(CurrentDays AS varchar) + N'|' + CAST(LongestDays AS varchar) FROM dbo.Streaks WHERE ChildProfileId={kid};") == "1|2", "after a gap the streak restarts and the record stays")

        # US-46: review wrong answers
        sql("INSERT dbo.Questions(LessonId, Text, Explanation, QuestionType, Difficulty, Origin, Status, CreatedById, CreatedAt, ApprovedById, ApprovedAt) "
            f"VALUES ({a}, N'Cau mot {TAG}', N'Giai thich mot {TAG}', N'SingleChoice', 1, N'Manual', N'Published', N'{admin_id}', SYSUTCDATETIME(), N'{admin_id}', SYSUTCDATETIME()), "
            f"({a}, N'Cau hai {TAG}', N'Giai thich hai', N'SingleChoice', 1, N'Manual', N'Published', N'{admin_id}', SYSUTCDATETIME(), N'{admin_id}', SYSUTCDATETIME());")
        q1, q2 = (sql(f"SELECT Id FROM dbo.Questions WHERE Text=N'Cau {word} {TAG}';") for word in ("mot", "hai"))
        for question in (q1, q2):
            sql(f"INSERT dbo.QuestionOptions(QuestionId, Text, IsCorrect, SortOrder) VALUES ({question}, N'dung', 1, 1), ({question}, N'sai', 0, 2);")
        sql(f"INSERT dbo.Quizzes(LessonId, Title, PassPercent, ShuffleQuestions, Status, CreatedAt) VALUES ({a}, N'Quiz {TAG}', 70, 0, N'Published', SYSUTCDATETIME());")
        quiz = sql(f"SELECT Id FROM dbo.Quizzes WHERE Title=N'Quiz {TAG}';")
        sql(f"INSERT dbo.QuizQuestions(QuizId, QuestionId, SortOrder, Points) VALUES ({quiz}, {q1}, 1, 1), ({quiz}, {q2}, 2, 1);")
        option = lambda question, text: sql(f"SELECT Id FROM dbo.QuestionOptions WHERE QuestionId={question} AND Text=N'{text}';")
        check(parent.request("/kids-review")[0] == 200 and "không còn câu nào" in parent.request("/kids-review")[2], "nothing to review before any quiz")
        parent.request(f"/kids-quiz?id={quiz}")
        attempt = sql(f"SELECT MAX(Id) FROM dbo.QuizAttempts WHERE QuizId={quiz};")
        form(parent, "/kids-home", f"/kids-quiz/{attempt}/submit", [(f"q{q1}", option(q1, "sai")), (f"q{q2}", option(q2, "dung"))])
        review = parent.request("/kids-review")[2]
        check(f"Cau mot {TAG}" in review and f"Cau hai {TAG}" not in review, "the wrong answer is listed for review, the right one is not")
        check("Ôn lại câu chưa đúng" in parent.request("/kids-home")[2], "the child home points to the review")
        check(form(parent, "/kids-review", f"/kids-review/{q1}", [("option", option(q1, "sai"))])[0] == 302 and "Chưa đúng" in parent.request("/kids-review")[2],
              "checking a wrong answer says so")
        check(form(parent, "/kids-review", f"/kids-review/{q1}", [("option", option(q1, "dung"))])[0] == 302 and f"Giai thich mot {TAG}" in parent.request("/kids-review")[2],
              "checking the right answer shows the explanation")
        check(form(parent, "/kids-review", f"/kids-review/{q2}", [("option", option(q2, "dung"))])[0] == 404, "a question not in the review list cannot be checked there")
        parent.request(f"/kids-quiz?id={quiz}")
        attempt = sql(f"SELECT MAX(Id) FROM dbo.QuizAttempts WHERE QuizId={quiz};")
        form(parent, "/kids-home", f"/kids-quiz/{attempt}/submit", [(f"q{q1}", option(q1, "dung")), (f"q{q2}", option(q2, "dung"))])
        check(f"Cau mot {TAG}" not in parent.request("/kids-review")[2], "answering right on a retake removes the question from review")
        check(earned() == "4" and sql(f"SELECT CAST(CAST(AvgQuizPercent AS int) AS varchar) FROM dbo.SubjectProgresses WHERE ChildProfileId={kid} AND SubjectId={subject};") == "100",
              "a perfect quiz earns the quiz badge and the subject average uses the best score")
        page = parent.request("/kids-badges")[2]
        check(f"Huy Q {TAG}" in page and "5 sao" in page, "the badge page shows earned badges and stars")

        # Parent view
        page = parent.request(progress)[2]
        check("2 / 3" in page and "Đã hoàn thành" in page and f"Huy S {TAG}" in page and "Bé đã đạt mục tiêu" in page,
              "the parent sees subject progress, the finished path, badges and the reached goal")
        check("Be Tien" not in other.request(f"/progress?child={kid}")[2], "another family cannot see this child's progress")

        # Rules after use
        awarded = sql(f"SELECT Id FROM dbo.Badges WHERE Code=N'{BADGE}L';")
        check(form(admin, "/admin-badges", f"/admin-badges/{awarded}/delete")[0] == 302 and sql(f"SELECT COUNT(*) FROM dbo.Badges WHERE Id={awarded};") == "1",
              "an awarded badge cannot be deleted")
        form(admin, f"/admin-badges/{awarded}", f"/admin-badges/{awarded}", [pair for pair in badge("IGNORED", "Doi ten", "LessonsCompleted", 50) if pair[0] != "IsActive"] + [("IsActive", "false")])
        check(sql(f"SELECT Code + N'|' + CAST(Threshold AS varchar) + N'|' + CAST(IsActive AS varchar) FROM dbo.Badges WHERE Id={awarded};") == f"{BADGE}L|50|0" and earned() == "4",
              "editing a badge keeps its code and what children already earned")
        check(form(admin, "/admin-paths", f"/admin-paths/{path}/delete")[0] == 302 and paths() == "1", "a path children follow cannot be deleted")
        check(form(parent, progress, f"/progress/{kid}/paths/{path}/stop")[0] == 302 and sql(f"SELECT COUNT(*) FROM dbo.ChildPaths WHERE ChildProfileId={kid};") == "0",
              "the parent can stop a path")


def main():
    try:
        with tempfile.TemporaryDirectory(prefix="safelearn-private-mail-") as temporary:
            checks(free_url(), Path(temporary))
    finally:
        emails = f"N'{PARENT}', N'{OTHER}', N'{ADMIN}'"
        ids = f"SELECT Id FROM dbo.Users WHERE Email IN ({emails})"
        kids = f"SELECT Id FROM dbo.ChildProfiles WHERE ParentId IN ({ids})"
        lessons = f"SELECT l.Id FROM dbo.Lessons l JOIN dbo.Subjects s ON s.Id=l.SubjectId WHERE s.Code=N'{CODE}'"
        quizzes = f"SELECT Id FROM dbo.Quizzes WHERE LessonId IN ({lessons})"
        sql(f"DELETE FROM dbo.QuizAttemptAnswers WHERE QuizId IN ({quizzes});")
        sql(f"DELETE FROM dbo.QuizAttempts WHERE QuizId IN ({quizzes});")
        sql(f"DELETE FROM dbo.QuizQuestions WHERE QuizId IN ({quizzes});")
        sql(f"DELETE FROM dbo.Quizzes WHERE Id IN ({quizzes});")
        sql(f"DELETE FROM dbo.QuestionOptions WHERE QuestionId IN (SELECT Id FROM dbo.Questions WHERE LessonId IN ({lessons}));")
        sql(f"DELETE FROM dbo.Questions WHERE LessonId IN ({lessons});")
        for table in ("ChildPaths", "ChildBadges", "Streaks", "SubjectProgresses", "LearningGoals", "DailyUsages", "LearningSessions"):
            sql(f"DELETE FROM dbo.{table} WHERE ChildProfileId IN ({kids});")
        sql(f"DELETE FROM dbo.LearningPathItems WHERE PathId IN (SELECT Id FROM dbo.LearningPaths WHERE SubjectId IN (SELECT Id FROM dbo.Subjects WHERE Code=N'{CODE}'));")
        sql(f"DELETE FROM dbo.LearningPaths WHERE SubjectId IN (SELECT Id FROM dbo.Subjects WHERE Code=N'{CODE}');")
        sql(f"DELETE FROM dbo.ChildBadges WHERE BadgeId IN (SELECT Id FROM dbo.Badges WHERE Code LIKE N'{BADGE}%');")
        sql(f"DELETE FROM dbo.Badges WHERE Code LIKE N'{BADGE}%';")
        for table in ("Assignments", "LessonActivities", "Favorites", "LessonContents"):
            sql(f"DELETE FROM dbo.{table} WHERE LessonId IN ({lessons});")
        sql(f"DELETE FROM dbo.Lessons WHERE Id IN ({lessons});")
        sql(f"DELETE FROM dbo.Notifications WHERE UserId IN ({ids});")
        sql(f"DELETE FROM dbo.ChildProfiles WHERE ParentId IN ({ids});")
        sql(f"DELETE FROM dbo.SubjectGrades WHERE SubjectId IN (SELECT Id FROM dbo.Subjects WHERE Code=N'{CODE}');")
        sql(f"DELETE FROM dbo.Subjects WHERE Code=N'{CODE}';")
        sql(f"DELETE FROM dbo.AuditLogs WHERE ActorId IN ({ids}) OR EntityId IN ({ids});")
        sql(f"DELETE FROM dbo.LoginHistories WHERE UserNameTried IN ({emails});")
        sql(f"DELETE FROM dbo.Users WHERE Email IN ({emails});")
        check(sql(f"SELECT COUNT(*) FROM dbo.Users WHERE Email IN ({emails});") == "0" and sql(f"SELECT COUNT(*) FROM dbo.Subjects WHERE Code=N'{CODE}';") == "0"
              and sql(f"SELECT COUNT(*) FROM dbo.Badges WHERE Code LIKE N'{BADGE}%';") == "0", "generated progress data is cleaned up")
    print(f"RESULT {smoke.checks}/{smoke.checks} passed (paths + badges + streaks + goals + progress + review)")


if __name__ == "__main__":
    main()
