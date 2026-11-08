"""Exercise Sprint 6 (US-35 to US-40): question bank, quizzes, quiz assignments, automatic scoring, results and suggestions, on real SQL.

Uses the existing smoke harness, disposable UUID accounts and private email pickup.
Build first. Removes only the subject, lessons, questions, quizzes, children and accounts it creates.
Runs without Ai:ApiKey, so suggestions come from the offline generator.
"""

from datetime import datetime, timedelta
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
PARENT = f"sql-smoke-quiz-parent-{RUN}@example.invalid"
OTHER = f"sql-smoke-quiz-other-{RUN}@example.invalid"
ADMIN = f"sql-smoke-quiz-admin-{RUN}@example.invalid"
PASSWORD = f"QuizTest1{RUN}"
CODE = f"QZ{RUN[:8].upper()}"
KEY = CODE.lower()
TITLE = f"Bai quiz {RUN[:8]}"
OTHER_TITLE = f"Bai lop bon {RUN[:8]}"
EXPLAIN = f"Vi 2 cong 2 bang 4 {RUN[:6]}"
READING = ("Con meo nha em co 4 chan va mot cai duoi dai. Moi sang con meo thuong ngoi phoi nang ngoai hien. "
           "Em cho meo an ca vao buoi toi sau khi hoc bai xong. Lop em co 25 ban cung nuoi meo o nha.")


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
    # Pages without a form still carry the token the scripts use.
    token = smoke.token_from(body) if 'name="__RequestVerificationToken"' in body else json.loads(found.group(1))
    return client.request(path, list(pairs) + [("__RequestVerificationToken", token)])


def add_child(client, name):
    body = client.request("/profiles", raw=True)[2]
    token = json.loads(re.search(r'window\.SL_CSRF = ("[^"]+")', body).group(1))
    request = urllib.request.Request(client.url + "/children", data=json.dumps({"name": name, "cls": 2, "subjects": [KEY]}).encode(),
                                     headers={"Content-Type": "application/json", "RequestVerificationToken": token})
    with client.opener.open(request, timeout=10) as response:
        return json.loads(response.read())["id"]


def question(lesson, text, options, correct, status="Published", kind="SingleChoice", explanation=""):
    padded = list(options) + [""] * (4 - len(options))
    return ([("LessonId", str(lesson)), ("Text", text), ("Type", kind), ("Difficulty", "1"), ("Explanation", explanation), ("Status", status), ("Correct", str(correct))]
            + [(f"Options[{i}]", value) for i, value in enumerate(padded)])


def quiz(lesson, title, ids, status="Published", limit="", pass_percent="50", shuffle="false"):
    return ([("LessonId", str(lesson)), ("Title", title), ("PassPercent", pass_percent), ("TimeLimitMinutes", limit), ("Shuffle", shuffle), ("Status", status)]
            + [("QuestionIds", str(item)) for item in ids])


def option(question_id, text):
    return sql(f"SELECT Id FROM dbo.QuestionOptions WHERE QuestionId={question_id} AND Text=N'{text}';")


def location(client, path):
    return client.request(path)[1].get("Location", "") or ""


def checks(url, directory):
    with smoke.running_app(url, directory):
        register(directory, url, PARENT, "Quiz parent")
        register(directory, url, OTHER, "Quiz other")
        register(directory, url, ADMIN, "Quiz admin")
        admin_id = sql(f"SELECT Id FROM dbo.Users WHERE Email=N'{ADMIN}';")
        sql(f"DELETE FROM dbo.UserRoles WHERE UserId=N'{admin_id}';")
        sql(f"INSERT dbo.UserRoles(UserId, RoleId) SELECT N'{admin_id}', Id FROM dbo.Roles WHERE NormalizedName=N'ADMIN';")
        sql(f"INSERT dbo.Subjects(Code, Name, IconKey, ColorKey, SortOrder, IsActive) VALUES (N'{CODE}', N'Mon quiz {RUN[:6]}', N'star', N'blue', 95, 1);")
        sql(f"INSERT dbo.SubjectGrades(SubjectId, GradeLevelId) SELECT s.Id, g.Id FROM dbo.Subjects s CROSS JOIN dbo.GradeLevels g WHERE s.Code=N'{CODE}' AND g.Level IN (2, 4);")
        for title, level in ((TITLE, 2), (OTHER_TITLE, 4)):
            sql("INSERT dbo.Lessons(SubjectId, GradeLevelId, Title, LessonType, DurationMin, SortOrder, Status, CreatedById, CreatedAt, UpdatedAt, PublishedAt) "
                f"SELECT s.Id, g.Id, N'{title}', N'Reading', 5, 1, N'Published', N'{admin_id}', SYSUTCDATETIME(), SYSUTCDATETIME(), SYSUTCDATETIME() "
                f"FROM dbo.Subjects s CROSS JOIN dbo.GradeLevels g WHERE s.Code=N'{CODE}' AND g.Level={level};")
        lesson = sql(f"SELECT Id FROM dbo.Lessons WHERE Title=N'{TITLE}';")
        far = sql(f"SELECT Id FROM dbo.Lessons WHERE Title=N'{OTHER_TITLE}';")
        sql(f"INSERT dbo.LessonContents(LessonId, ContentKind, Title, Body, SortOrder) VALUES ({lesson}, N'Reading', N'Doc', N'{READING}', 1);")

        parent, other, admin = smoke.Client(url), smoke.Client(url), smoke.Client(url)
        for client, email in ((parent, PARENT), (other, OTHER), (admin, ADMIN)):
            check(client.post_form("/login", {"Email": email, "Password": PASSWORD})[0] == 302, f"{email.split('-')[3]} signs in")
        # Child mode locks the parent pages on the server: /kids-* pages run on the child's device, the rest on the parent's.
        parent, other = smoke.Devices(parent), smoke.Devices(other)
        kid = add_child(parent, "Be Quiz")
        other_kid = add_child(other, "Be Khac")
        bank = f"/admin-questions?lesson={lesson}"
        new_question = f"/admin-questions/new?lesson={lesson}"

        # US-35: question bank
        check(admin.request(bank)[0] == 200 and TITLE in admin.request(bank)[2], "the admin opens the lesson's question bank")
        check(parent.request(bank)[0] != 200, "parents cannot open the question bank")
        check(admin.request("/admin-questions/new", question(lesson, "Thieu token", ["1", "2"], 0))[0] == 400, "creating a question requires CSRF")
        count = lambda: sql(f"SELECT COUNT(*) FROM dbo.Questions WHERE LessonId={lesson};")
        check(form(admin, new_question, "/admin-questions/new", question(lesson, "Mot dap an", ["1"], 0))[0] == 200 and count() == "0", "a question needs at least two answers")
        check(form(admin, new_question, "/admin-questions/new", question(lesson, "Trung dap an", ["4", "4"], 0))[0] == 200 and count() == "0", "answers cannot repeat")
        check(form(admin, new_question, "/admin-questions/new", question(lesson, "Dap an rong", ["1", "2"], 3))[0] == 200 and count() == "0", "the correct answer must be one that was filled in")
        check(form(admin, new_question, "/admin-questions/new", question(lesson, "Hai cong hai bang may", ["3", "", "4", "5"], 2, explanation=EXPLAIN))[0] == 302, "the admin adds a multiple-choice question")
        q1 = sql(f"SELECT Id FROM dbo.Questions WHERE LessonId={lesson} AND Text=N'Hai cong hai bang may';")
        check(sql(f"SELECT COUNT(*) FROM dbo.QuestionOptions WHERE QuestionId={q1};") == "3"
              and sql(f"SELECT Text FROM dbo.QuestionOptions WHERE QuestionId={q1} AND IsCorrect=1;") == "4"
              and sql(f"SELECT CASE WHEN ApprovedById IS NOT NULL AND Origin=N'Manual' THEN 1 ELSE 0 END FROM dbo.Questions WHERE Id={q1};") == "1",
              "empty rows are dropped, the correct mark follows its answer and publishing records the approver")
        check(form(admin, new_question, "/admin-questions/new", question(lesson, "Meo co bon chan", ["x", "y"], 1, status="Draft", kind="TrueFalse"))[0] == 302, "the admin adds a true/false draft")
        q2 = sql(f"SELECT Id FROM dbo.Questions WHERE LessonId={lesson} AND Text=N'Meo co bon chan';")
        check(sql(f"SELECT COUNT(*) FROM dbo.QuestionOptions WHERE QuestionId={q2};") == "2"
              and sql(f"SELECT SortOrder FROM dbo.QuestionOptions WHERE QuestionId={q2} AND IsCorrect=1;") == "2"
              and sql(f"SELECT Status FROM dbo.Questions WHERE Id={q2};") == "Draft", "true/false questions get the fixed answers Dung and Sai")
        check(form(admin, bank, f"/admin-questions/{q2}/status", [("action", "approve")])[0] == 302 and sql(f"SELECT Status FROM dbo.Questions WHERE Id={q2};") == "Published",
              "the admin approves the draft")
        check(sql(f"SELECT COUNT(*) FROM dbo.AuditLogs WHERE ActorId=N'{admin_id}' AND Action IN (N'QuestionCreate', N'QuestionStatus');") == "3", "question changes are audited")

        # US-40: suggestions wait for approval
        check(form(admin, bank, "/admin-questions/suggest", [("lesson", lesson), ("count", "3")])[0] == 302, "the admin asks for suggested questions")
        suggested = sql(f"SELECT COUNT(*) FROM dbo.Questions WHERE LessonId={lesson} AND Origin=N'AiSuggested' AND Status=N'InReview' AND AiModelName=N'offline-rule-v1';")
        check(suggested == "3", "without an API key the offline generator suggests questions that wait for review")
        check(sql(f"SELECT COUNT(*) FROM dbo.QuestionOptions o JOIN dbo.Questions q ON q.Id=o.QuestionId WHERE q.LessonId={lesson} AND q.Origin=N'AiSuggested' AND o.IsCorrect=1;") == "3",
              "every suggestion has exactly one correct answer")
        s1, s2 = sql(f"SELECT STRING_AGG(Id, N',') WITHIN GROUP (ORDER BY Id) FROM dbo.Questions WHERE LessonId={lesson} AND Origin=N'AiSuggested';").split(",")[:2]
        check("Chờ duyệt" in admin.request("/admin-questions?pending=true")[2], "suggestions are listed for review")
        check(form(admin, "/admin-questions/new?lesson=" + lesson, "/admin-quizzes/new", quiz(lesson, "Co cau chua duyet", [q1, s1]))[0] == 200
              and sql(f"SELECT COUNT(*) FROM dbo.Quizzes WHERE LessonId={lesson};") == "0", "a quiz cannot use a question still in review")
        check(form(admin, bank, f"/admin-questions/{s1}/status", [("action", "reject")])[0] == 302 and sql(f"SELECT Status FROM dbo.Questions WHERE Id={s1};") == "Archived",
              "the admin rejects a suggestion")
        check(form(admin, bank, f"/admin-questions/{s2}/status", [("action", "approve")])[0] == 302 and sql(f"SELECT Status FROM dbo.Questions WHERE Id={s2};") == "Published",
              "the admin approves a suggestion")

        # US-36: quizzes from the bank
        new_quiz = f"/admin-quizzes/new?lesson={lesson}"
        check(form(admin, new_quiz, "/admin-quizzes/new", quiz(lesson, "Rong", []))[0] == 200 and sql(f"SELECT COUNT(*) FROM dbo.Quizzes WHERE LessonId={lesson};") == "0",
              "an open quiz needs at least one question")
        check(form(admin, new_quiz, "/admin-quizzes/new", quiz(lesson, "Qua thoi gian", [q1], limit="90"))[0] == 200, "the time limit is capped at 60 minutes")
        check(form(admin, new_quiz, "/admin-quizzes/new", quiz(lesson, f"Kiem tra {RUN[:6]}", [q1, q2], limit="5"))[0] == 302, "the admin builds a quiz from approved questions")
        quiz_id = sql(f"SELECT Id FROM dbo.Quizzes WHERE LessonId={lesson};")
        check(sql(f"SELECT CAST(COUNT(*) AS varchar) + N'|' + CAST(MAX(z.TimeLimitSec) AS varchar) + N'|' + CAST(MAX(z.PassPercent) AS varchar) "
                  f"FROM dbo.QuizQuestions qq JOIN dbo.Quizzes z ON z.Id=qq.QuizId WHERE qq.QuizId={quiz_id};") == "2|300|50", "questions, time limit and pass mark are stored")
        check(form(admin, bank, f"/admin-questions/{q1}/delete")[0] == 302 and count() != "0" and sql(f"SELECT COUNT(*) FROM dbo.Questions WHERE Id={q1};") == "1",
              "a question used in a quiz cannot be deleted")

        # US-37: assigning with a due date
        detail = f"/lesson-detail/{lesson}"
        due = (datetime.now() + timedelta(days=2)).strftime("%Y-%m-%d")
        page = parent.request(detail)[2]
        check(f"Kiem tra {RUN[:6]}" in page and "Giao bài tập" in page, "the parent sees the quiz on the lesson page")
        assigned = lambda: sql(f"SELECT COUNT(*) FROM dbo.Assignments WHERE QuizId={quiz_id} AND Status IN (N'Assigned', N'InProgress');")
        check(form(parent, detail, f"{detail}/assign-quiz", [("quizId", quiz_id), ("childIds", str(kid))])[0] == 302 and assigned() == "0", "a quiz assignment needs a due date")
        check(form(parent, detail, f"{detail}/assign-quiz", [("quizId", quiz_id), ("childIds", str(other_kid)), ("dueDate", due)])[0] == 404 and assigned() == "0",
              "a parent cannot assign to another family's child")
        check(form(parent, detail, f"{detail}/assign-quiz", [("quizId", quiz_id), ("childIds", str(kid)), ("dueDate", "2020-01-01")])[0] == 302 and assigned() == "0",
              "the due date cannot be in the past")
        check(form(parent, detail, f"{detail}/assign-quiz", [("quizId", quiz_id), ("childIds", str(kid)), ("dueDate", due), ("note", "Lam truoc thu bay")])[0] == 302
              and assigned() == "1" and sql(f"SELECT CONVERT(varchar, DueDate, 23) FROM dbo.Assignments WHERE QuizId={quiz_id};") == due, "the parent assigns the quiz with a due date")
        check(form(parent, detail, f"{detail}/assign-quiz", [("quizId", quiz_id), ("childIds", str(kid)), ("dueDate", due)])[0] == 302 and assigned() == "1",
              "the same quiz is not assigned twice while open")
        assignment = sql(f"SELECT Id FROM dbo.Assignments WHERE QuizId={quiz_id};")

        # US-38: taking the quiz
        check(form(parent, "/kids-picker", f"/kids-picker/{kid}")[0] == 302, "the parent opens child mode")
        check(f"Kiem tra {RUN[:6]}" in parent.request("/kids-home")[2], "the assigned quiz shows on the child's home page")
        check(f"Kiem tra {RUN[:6]}" in parent.request(f"/kids-lesson?id={lesson}")[2], "the quiz shows on its lesson page")
        status, _, body = parent.request(f"/kids-quiz?id={quiz_id}")
        attempt = sql(f"SELECT Id FROM dbo.QuizAttempts WHERE QuizId={quiz_id} AND ChildProfileId={kid};")
        check(status == 200 and "Hai cong hai bang may" in body and f'name="q{q1}"' in body and "IsCorrect" not in body, "the child sees the questions without the answers")
        check(sql(f"SELECT CAST(AssignmentId AS varchar) FROM dbo.QuizAttempts WHERE Id={attempt};") == assignment
              and sql(f"SELECT Status FROM dbo.Assignments WHERE Id={assignment};") == "InProgress", "starting links the attempt to the assignment")
        parent.request(f"/kids-quiz?id={quiz_id}")
        check(sql(f"SELECT COUNT(*) FROM dbo.QuizAttempts WHERE QuizId={quiz_id} AND ChildProfileId={kid};") == "1", "a reload resumes the same attempt")
        beat = form(parent, "/kids-home", f"/kids-quiz/{attempt}/heartbeat", [("seconds", "30")])
        check(beat[0] == 200 and json.loads(beat[2]).get("allowed") is True and sql(f"SELECT Seconds FROM dbo.DailyUsages WHERE ChildProfileId={kid};") == "30",
              "time on the quiz page counts as study time")
        check(form(other, "/profiles", f"/kids-picker/{other_kid}")[0] == 302 and form(other, "/kids-home", f"/kids-quiz/{attempt}/submit", [(f"q{q1}", option(q1, "4"))])[0] == 404,
              "another family cannot submit this child's attempt")
        check(parent.request(f"/kids-quiz/{attempt}/submit", [(f"q{q1}", option(q1, "4"))])[0] == 400, "submitting requires CSRF")
        wrong = sql(f"SELECT Id FROM dbo.QuestionOptions WHERE QuestionId={q2} AND IsCorrect=0;")
        result = form(parent, "/kids-home", f"/kids-quiz/{attempt}/submit", [(f"q{q1}", option(q1, "4")), (f"q{q2}", wrong)])
        check(result[0] == 302 and f"/kids-result?id={attempt}" in result[1].get("Location", ""), "the child submits and goes to the result")
        scored = sql(f"SELECT CAST(CorrectCount AS varchar) + N'|' + CAST(TotalCount AS varchar) + N'|' + CAST(CAST(ScorePercent AS int) AS varchar) + N'|' + CAST(Passed AS varchar) "
                     f"FROM dbo.QuizAttempts WHERE Id={attempt};")
        check(scored == "1|2|50|1", f"the server scores the attempt (1 of 2 right, pass mark 50%), got {scored}")
        check(sql(f"SELECT COUNT(*) FROM dbo.QuizAttemptAnswers WHERE AttemptId={attempt};") == "2", "every question gets an answer row")
        check(sql(f"SELECT Status FROM dbo.Assignments WHERE Id={assignment};") == "Completed"
              and sql(f"SELECT COUNT(*) FROM dbo.Notifications n JOIN dbo.Users u ON u.Id=n.UserId WHERE u.Email=N'{PARENT}' AND n.NotificationType=N'Assignment' AND n.LinkUrl LIKE N'/quiz-result/%';") == "1",
              "submitting completes the assignment and tells the parent")
        check(form(parent, "/kids-home", f"/kids-quiz/{attempt}/submit", [(f"q{q2}", option(q2, "Đúng") or wrong)])[0] == 302
              and sql(f"SELECT COUNT(*) FROM dbo.QuizAttemptAnswers WHERE AttemptId={attempt};") == "2"
              and sql(f"SELECT CorrectCount FROM dbo.QuizAttempts WHERE Id={attempt};") == "1", "a second submit does not change the score")
        page = parent.request(f"/kids-result?id={attempt}")[2]
        check(EXPLAIN in page and "Con chọn" in page and "1/2" in page, "the child sees the score, the chosen answers and the explanations")

        # Tampered answers and the clock
        parent.request(f"/kids-quiz?id={quiz_id}")
        second = sql(f"SELECT MAX(Id) FROM dbo.QuizAttempts WHERE QuizId={quiz_id} AND ChildProfileId={kid};")
        check(second != attempt and sql(f"SELECT CASE WHEN AssignmentId IS NULL THEN 1 ELSE 0 END FROM dbo.QuizAttempts WHERE Id={second};") == "1",
              "a retake after the assignment is practice, not linked to it")
        form(parent, "/kids-home", f"/kids-quiz/{second}/submit", [(f"q{q1}", wrong), (f"q{q2}", "999999999")])
        check(sql(f"SELECT COUNT(*) FROM dbo.QuizAttemptAnswers WHERE AttemptId={second} AND SelectedOptionId IS NULL;") == "2"
              and sql(f"SELECT CorrectCount FROM dbo.QuizAttempts WHERE Id={second};") == "0", "an option from another question counts as unanswered")
        parent.request(f"/kids-quiz?id={quiz_id}")
        third = sql(f"SELECT MAX(Id) FROM dbo.QuizAttempts WHERE QuizId={quiz_id} AND ChildProfileId={kid};")
        sql(f"UPDATE dbo.QuizAttempts SET StartedAt=DATEADD(minute, -10, SYSUTCDATETIME()) WHERE Id={third};")
        form(parent, "/kids-home", f"/kids-quiz/{third}/submit", [(f"q{q1}", option(q1, "4"))])
        check(sql(f"SELECT CorrectCount FROM dbo.QuizAttempts WHERE Id={third};") == "0" and "Hết giờ" in parent.request(f"/kids-result?id={third}")[2],
              "answers sent long after the time limit are not counted")
        parent.request(f"/kids-quiz?id={quiz_id}")
        fourth = sql(f"SELECT MAX(Id) FROM dbo.QuizAttempts WHERE QuizId={quiz_id} AND ChildProfileId={kid};")
        sql(f"UPDATE dbo.QuizAttempts SET StartedAt=DATEADD(minute, -10, SYSUTCDATETIME()) WHERE Id={fourth};")
        parent.request(f"/kids-quiz?id={quiz_id}")
        check(sql(f"SELECT CASE WHEN SubmittedAt IS NOT NULL THEN 1 ELSE 0 END FROM dbo.QuizAttempts WHERE Id={fourth};") == "1"
              and sql(f"SELECT COUNT(*) FROM dbo.QuizAttempts WHERE QuizId={quiz_id} AND ChildProfileId={kid} AND SubmittedAt IS NULL;") == "1",
              "an expired open attempt is closed instead of restarting the clock")
        sql(f"UPDATE dbo.DailyUsages SET Seconds=36000 WHERE ChildProfileId={kid};")
        check("/kids-time-up" in location(parent, f"/kids-quiz?id={quiz_id}"), "when today's minutes are used up the quiz page sends the child to time-up")
        open_attempt = sql(f"SELECT Id FROM dbo.QuizAttempts WHERE QuizId={quiz_id} AND ChildProfileId={kid} AND SubmittedAt IS NULL;")
        check(form(parent, "/kids-time-up", f"/kids-quiz/{open_attempt}/submit", [(f"q{q1}", option(q1, "4"))])[0] == 302
              and sql(f"SELECT CorrectCount FROM dbo.QuizAttempts WHERE Id={open_attempt};") == "1", "an answer sheet is still accepted when time runs out")
        sql(f"DELETE FROM dbo.DailyUsages WHERE ChildProfileId={kid};")

        # US-39: parents see the answers
        check(EXPLAIN in parent.request(f"/quiz-result/{attempt}")[2] and "Bé chọn" in parent.request(f"/quiz-result/{attempt}")[2], "the parent sees the child's answers and explanations")
        check(other.request(f"/quiz-result/{attempt}")[0] == 404, "another family cannot see the result")
        check(f"Kiem tra {RUN[:6]}" in parent.request(f"/history?child={kid}")[2], "the history lists the quiz attempts")
        check("50%" in parent.request(detail)[2], "the lesson page shows the assigned quiz's score")

        # Access rules
        form(admin, f"/admin-questions/new?lesson={far}", "/admin-questions/new", question(far, "Cau lop bon", ["a", "b"], 0))
        q_far = sql(f"SELECT Id FROM dbo.Questions WHERE LessonId={far};")
        form(admin, f"/admin-quizzes/new?lesson={far}", "/admin-quizzes/new", quiz(far, f"Lop bon {RUN[:6]}", [q_far]))
        quiz_far = sql(f"SELECT Id FROM dbo.Quizzes WHERE LessonId={far};")
        check(parent.request(f"/kids-quiz?id={quiz_far}")[0] == 404, "a quiz for another grade stays closed")
        form(parent, f"/lesson-detail/{far}", f"/lesson-detail/{far}/assign-quiz", [("quizId", quiz_far), ("childIds", str(kid)), ("dueDate", due)])
        form(parent, "/kids-picker", f"/kids-picker/{kid}")
        check(parent.request(f"/kids-quiz?id={quiz_far}")[0] == 200, "assigning the quiz opens it for the child")
        form(admin, f"/admin-quizzes/{quiz_far}", f"/admin-quizzes/{quiz_far}", quiz(far, f"Lop bon {RUN[:6]}", [q_far], status="Draft"))
        check(parent.request(f"/kids-quiz?id={quiz_far}")[0] == 404, "a quiz moved back to draft is closed")

        # Editing after children answered
        form(admin, f"/admin-questions/{q1}", f"/admin-questions/{q1}", question(lesson, "Hai cong hai bang may nhi", ["7", "8"], 0, explanation=EXPLAIN))
        check(sql(f"SELECT Text FROM dbo.Questions WHERE Id={q1};") == "Hai cong hai bang may nhi"
              and sql(f"SELECT Text FROM dbo.QuestionOptions WHERE QuestionId={q1} AND IsCorrect=1;") == "4"
              and sql(f"SELECT COUNT(*) FROM dbo.QuestionOptions WHERE QuestionId={q1};") == "3", "an answered question keeps its answers; the wording can change")
        form(admin, f"/admin-quizzes/{quiz_id}", f"/admin-quizzes/{quiz_id}", quiz(lesson, f"Kiem tra {RUN[:6]}", [q1], limit="5"))
        check(sql(f"SELECT COUNT(*) FROM dbo.QuizQuestions WHERE QuizId={quiz_id};") == "2", "a quiz with attempts keeps its question set")
        check(form(admin, bank, f"/admin-quizzes/{quiz_id}/delete")[0] == 302 and sql(f"SELECT COUNT(*) FROM dbo.Quizzes WHERE Id={quiz_id};") == "1", "a quiz with attempts cannot be deleted")
        form(admin, bank, f"/admin-questions/{q2}/status", [("action", "archive")])
        parent.request(f"/kids-quiz?id={quiz_id}")
        last = sql(f"SELECT MAX(Id) FROM dbo.QuizAttempts WHERE QuizId={quiz_id} AND ChildProfileId={kid};")
        form(parent, "/kids-home", f"/kids-quiz/{last}/submit", [(f"q{q1}", option(q1, "4"))])
        check(sql(f"SELECT CAST(TotalCount AS varchar) + N'|' + CAST(CAST(ScorePercent AS int) AS varchar) FROM dbo.QuizAttempts WHERE Id={last};") == "1|100",
              "an archived question is left out of new attempts")


def main():
    try:
        with tempfile.TemporaryDirectory(prefix="safelearn-private-mail-") as temporary:
            checks(free_url(), Path(temporary))
    finally:
        emails = f"N'{PARENT}', N'{OTHER}', N'{ADMIN}'"
        ids = f"SELECT Id FROM dbo.Users WHERE Email IN ({emails})"
        lessons = f"SELECT l.Id FROM dbo.Lessons l JOIN dbo.Subjects s ON s.Id=l.SubjectId WHERE s.Code=N'{CODE}'"
        quizzes = f"SELECT Id FROM dbo.Quizzes WHERE LessonId IN ({lessons})"
        sql(f"DELETE FROM dbo.QuizAttemptAnswers WHERE QuizId IN ({quizzes});")
        sql(f"DELETE FROM dbo.QuizAttempts WHERE QuizId IN ({quizzes});")
        sql(f"DELETE FROM dbo.Assignments WHERE QuizId IN ({quizzes}) OR LessonId IN ({lessons});")
        sql(f"DELETE FROM dbo.QuizQuestions WHERE QuizId IN ({quizzes});")
        sql(f"DELETE FROM dbo.Quizzes WHERE LessonId IN ({lessons});")
        sql(f"DELETE FROM dbo.QuestionOptions WHERE QuestionId IN (SELECT Id FROM dbo.Questions WHERE LessonId IN ({lessons}));")
        sql(f"DELETE FROM dbo.Questions WHERE LessonId IN ({lessons});")
        for table in ("LessonActivities", "Favorites", "LessonContents"):
            sql(f"DELETE FROM dbo.{table} WHERE LessonId IN ({lessons});")
        sql(f"DELETE FROM dbo.Lessons WHERE Id IN ({lessons});")
        sql(f"DELETE FROM dbo.Notifications WHERE UserId IN ({ids});")
        sql(f"DELETE FROM dbo.ChildProfiles WHERE ParentId IN ({ids});")
        sql(f"DELETE FROM dbo.SubjectGrades WHERE SubjectId IN (SELECT Id FROM dbo.Subjects WHERE Code=N'{CODE}');")
        sql(f"DELETE FROM dbo.Subjects WHERE Code=N'{CODE}';")
        sql(f"DELETE FROM dbo.AuditLogs WHERE ActorId IN ({ids}) OR EntityId IN ({ids});")
        sql(f"DELETE FROM dbo.LoginHistories WHERE UserNameTried IN ({emails});")
        sql(f"DELETE FROM dbo.Users WHERE Email IN ({emails});")
        check(sql(f"SELECT COUNT(*) FROM dbo.Users WHERE Email IN ({emails});") == "0" and sql(f"SELECT COUNT(*) FROM dbo.Subjects WHERE Code=N'{CODE}';") == "0",
              "generated quiz data is cleaned up")
    print(f"RESULT {smoke.checks}/{smoke.checks} passed (question bank + suggestions + quizzes + assignments + scoring + results)")


if __name__ == "__main__":
    main()
