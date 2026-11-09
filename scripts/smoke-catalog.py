"""Exercise the Sprint 2 catalog (US-11 to US-16) against the built root app and real SQL.

Uses the existing smoke harness, disposable UUID accounts and private email pickup.
Build first. Removes only the subjects, lessons, tags, files and accounts it creates.
"""

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

RUN = uuid.uuid4().hex
ADMIN = f"sql-smoke-catalog-admin-{RUN}@example.invalid"
PARENT = f"sql-smoke-catalog-parent-{RUN}@example.invalid"
PASSWORD = f"CatalogTest1{RUN}"
CODE = f"SM{RUN[:8].upper()}"
KEY = CODE.lower()
# sqlcmd prints in the console code page, so names compared through SQL stay ASCII.
SUBJECT = f"Mon thu {RUN[:6]}"
TAGS = (f"smoke-{RUN[:10]}", f"smoke2-{RUN[:10]}")
TITLE = f"<script>alert(1)</script> {RUN[:6]}"
DRAFT = f"Bài nháp {RUN[:6]}"
PDF = b"%PDF-1.4\n" + b"0" * 64
PNG = bytes.fromhex("89504E470D0A1A0A") + b"\0" * 32
saved_files = []


def free_url():
    with socket.socket() as listener:
        listener.bind(("127.0.0.1", 0))
        return f"http://127.0.0.1:{listener.getsockname()[1]}"


def register(directory, url, email, name):
    client = smoke.Client(url)
    status, _, _ = client.post_form("/register", {
        "FullName": name, "Email": email, "Password": PASSWORD, "ConfirmPassword": PASSWORD, "Agree": "true"})
    check(status == 302, f"registers {name}")
    check(smoke.confirm(client, smoke.mail_link(directory, email, "/confirm-email"))[0] == 302, f"confirms {name}")


def form(client, page, path, pairs):
    """Post repeated fields (such as grade checkboxes) with the page's antiforgery token."""
    token = smoke.token_from(client.request(page)[2])
    return client.request(path, list(pairs) + [("__RequestVerificationToken", token)])


def multipart(client, page, path, fields, name=None, content=None):
    token = smoke.token_from(client.request(page)[2])
    boundary = uuid.uuid4().hex
    body = "".join(f'--{boundary}\r\nContent-Disposition: form-data; name="{key}"\r\n\r\n{value}\r\n'
                   for key, value in {"__RequestVerificationToken": token, **fields}.items()).encode()
    if content is not None:
        body += (f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="{name}"\r\n'
                 "Content-Type: application/octet-stream\r\n\r\n").encode() + content + b"\r\n"
    body += f"--{boundary}--\r\n".encode()
    request = urllib.request.Request(client.url + path, data=body, headers={"Content-Type": f"multipart/form-data; boundary={boundary}"})
    try:
        response = client.opener.open(request, timeout=10)
    except urllib.error.HTTPError as error:
        response = error
    return response.code


def child_post(client, path, values):
    body = client.request("/profiles", raw=True)[2]
    token = json.loads(re.search(r'window\.SL_CSRF = ("[^"]+")', body).group(1))
    request = urllib.request.Request(client.url + path, data=json.dumps(values).encode(),
                                     headers={"Content-Type": "application/json", "RequestVerificationToken": token})
    try:
        response = client.opener.open(request, timeout=10)
    except urllib.error.HTTPError as error:
        response = error
    return response.code


def subject_id():
    return sql(f"SELECT Id FROM dbo.Subjects WHERE Code=N'{CODE}';")


def subject_fields(name=SUBJECT, grades=(2, 3), active=True, code=KEY, color="teal"):
    return [("Name", name), ("Code", code), ("Description", "Môn tạo bởi smoke test"), ("IconKey", "star"), ("ColorKey", color),
            ("SortOrder", "90"), ("IsActive", "true" if active else "false")] + [("Grades", str(grade)) for grade in grades]


def grades_of(sid):
    return sql(f"SELECT g.Level FROM dbo.SubjectGrades sg JOIN dbo.GradeLevels g ON g.Id=sg.GradeLevelId WHERE sg.SubjectId={sid} ORDER BY g.Level;").split()


def lesson_value(lesson, column):
    return sql(f"SELECT ISNULL(CAST({column} AS nvarchar(400)), N'') FROM dbo.Lessons WHERE Id={lesson};")


def row_version(client, lesson):
    return re.search(r'name="RowVersion" value="([^"]*)"', client.request(f"/admin-lesson/{lesson}")[2]).group(1)


def lesson_fields(sid, grade, title, rowversion=None):
    fields = [("Title", title), ("SubjectId", sid), ("Grade", str(grade)), ("LessonType", "Mixed"), ("DurationMin", "12"),
              ("SortOrder", "1"), ("Summary", "Bé luyện tập " + RUN[:6]), ("Tags", ", ".join(TAGS))]
    return fields + ([("RowVersion", rowversion)] if rowversion is not None else [])


def set_status(admin, lesson, to):
    page = f"/admin-lesson/{lesson}"
    return form(admin, page, f"{page}/status", [("to", to), ("rowVersion", row_version(admin, lesson))])[0]


def subject_checks(admin, parent, anonymous):
    check(anonymous.request("/admin-catalog")[0] == 302, "anonymous users cannot open the catalog")
    status, headers, _ = parent.request("/admin-catalog")
    check(status == 302 and "/error/403" in headers.get("Location", ""), "parents cannot open the catalog")
    status, _, body = admin.request("/admin-catalog")
    check(status == 200 and "Toán" in body, "Admin sees the subject list")

    new = "/admin-catalog/subjects/new"
    check(admin.request(new, subject_fields())[0] == 400, "creating a subject requires CSRF")
    check(form(admin, new, new, subject_fields(grades=()))[0] == 200 and subject_id() == "", "a subject needs a grade")
    check(form(admin, new, new, subject_fields(code="toan"))[0] == 200 and sql("SELECT COUNT(*) FROM dbo.Subjects WHERE Code=N'TOAN';") == "0",
          "a code that would reuse a built-in key is refused")
    check(form(admin, new, new, subject_fields(color="pink"))[0] == 200 and subject_id() == "", "unknown colours are refused")
    status, _, _ = form(admin, new, new, subject_fields())
    sid = subject_id()
    check(status == 302 and sid != "", "Admin adds a subject and its code is stored in capitals")
    check(grades_of(sid) == ["2", "3"], "the subject is offered for the chosen grades")
    check(form(admin, new, new, subject_fields())[0] == 200 and sql(f"SELECT COUNT(*) FROM dbo.Subjects WHERE Code=N'{CODE}';") == "1",
          "duplicate subject codes are refused")
    check(sql(f"SELECT COUNT(*) FROM dbo.AuditLogs WHERE EntityType=N'Subject' AND EntityId=N'{sid}' AND Action=N'CreateSubject';") == "1",
          "adding a subject is audited")
    edit = f"/admin-catalog/subjects/{sid}/edit"
    check(form(admin, edit, edit, subject_fields(name=SUBJECT + " moi", code="OTHER"))[0] == 302
          and sql(f"SELECT Name + N'|' + Code FROM dbo.Subjects WHERE Id={sid};") == f"{SUBJECT} moi|{CODE}", "editing renames the subject and keeps its code")
    return sid, edit


def lesson_checks(admin, sid, edit):
    create = "/admin-lesson/new"
    check(form(admin, create, create, lesson_fields(sid, 4, "Bài lớp sai"))[0] == 200
          and sql(f"SELECT COUNT(*) FROM dbo.Lessons WHERE SubjectId={sid};") == "0", "a lesson needs a grade the subject is offered for")
    check(form(admin, create, create, lesson_fields(sid, 2, "ab"))[0] == 200, "a too-short title is refused")
    status, headers, _ = form(admin, create, create, lesson_fields(sid, 2, "Bài thử " + RUN[:6]))
    lesson = sql(f"SELECT Id FROM dbo.Lessons WHERE SubjectId={sid};")
    check(status == 302 and headers.get("Location", "").endswith(f"/admin-lesson/{lesson}"), "Admin creates a lesson")
    check(lesson_value(lesson, "Status") == "Draft"
          and lesson_value(lesson, "CreatedById") == sql(f"SELECT Id FROM dbo.Users WHERE Email=N'{ADMIN}';"), "new lessons start as drafts owned by their author")
    check(sql(f"SELECT COUNT(*) FROM dbo.LessonTags lt JOIN dbo.Tags t ON t.Id=lt.TagId WHERE lt.LessonId={lesson} AND t.Name IN (N'{TAGS[0]}', N'{TAGS[1]}');") == "2",
          "tags are created and attached")
    page = f"/admin-lesson/{lesson}"
    check(set_status(admin, lesson, "Published") == 302 and lesson_value(lesson, "Status") == "Draft"
          and "Cần ít nhất 1 nội dung" in admin.request(page)[2], "a lesson without content cannot be published")

    contents = f"{page}/contents"
    count = lambda kind: sql(f"SELECT COUNT(*) FROM dbo.LessonContents WHERE LessonId={lesson} AND ContentKind=N'{kind}';")
    multipart(admin, page, contents, {"Kind": "Reading", "Title": "Đọc", "Body": " "})
    check(count("Reading") == "0", "an empty reading is refused")
    multipart(admin, page, contents, {"Kind": "Reading", "Title": "Đọc hiểu", "Body": f"Dòng một {RUN[:6]}\nDòng hai"})
    check(count("Reading") == "1", "Admin adds a reading")
    multipart(admin, page, contents, {"Kind": "Document"}, "fake.pdf", b"<script>")
    check(count("Document") == "0", "a fake PDF is refused")
    multipart(admin, page, contents, {"Kind": "Image"}, "pdf.png", PDF)
    check(count("Image") == "0", "a PDF sent as an image is refused")
    multipart(admin, page, contents, {"Kind": "Document", "Title": "Phiếu bài tập"}, "phieu.pdf", PDF)
    multipart(admin, page, contents, {"Kind": "Image", "Title": "Hình minh họa"}, "hinh.png", PNG)
    for url in sql(f"SELECT FileUrl FROM dbo.LessonContents WHERE LessonId={lesson} AND FileUrl IS NOT NULL;").split():
        saved_files.append(smoke.ROOT / "wwwroot" / url.lstrip("/"))
    check(len(saved_files) == 2 and all(path.exists() for path in saved_files), "PDF and image files are stored")
    blocked = sql("SELECT TOP 1 Id FROM dbo.SourceVideos WHERE Status=N'Blocked';")
    if blocked:
        multipart(admin, page, contents, {"Kind": "Video", "VideoId": blocked})
        check(count("Video") == "0", "a blocked video cannot be attached")
    video = sql("SELECT TOP 1 Id FROM dbo.SourceVideos WHERE Status=N'Approved' AND Platform=N'YouTube' ORDER BY Id;")
    multipart(admin, page, contents, {"Kind": "Video", "VideoId": video})
    check(count("Video") == "1", "Admin attaches an approved video")
    last = sql(f"SELECT TOP 1 Id FROM dbo.LessonContents WHERE LessonId={lesson} ORDER BY SortOrder DESC;")
    form(admin, page, f"{contents}/{last}/move", [("up", "true")])
    check(sql(f"SELECT ContentKind FROM dbo.LessonContents WHERE LessonId={lesson} ORDER BY SortOrder;").split() == ["Reading", "Document", "Video", "Image"],
          "contents can be reordered")

    old = row_version(admin, lesson)
    check(form(admin, page, page, lesson_fields(sid, 3, TITLE, old))[0] == 302 and lesson_value(lesson, "Title") == TITLE, "Admin edits the lesson")
    check(form(admin, page, page, lesson_fields(sid, 3, "Ghi đè cũ", old))[0] == 302 and lesson_value(lesson, "Title") == TITLE
          and "thay đổi ở nơi khác" in admin.request(page)[2], "a save based on an old copy is refused")
    check("<script>alert(1)" not in admin.request(page, raw=True)[2], "lesson titles are encoded on admin pages")
    check(set_status(admin, lesson, "Published") == 302 and lesson_value(lesson, "Status") == "Published"
          and lesson_value(lesson, "PublishedAt") != "" and lesson_value(lesson, "ApprovedById") != "", "Admin publishes the lesson and the approver is recorded")
    check(form(admin, page, f"{page}/delete", [])[0] == 302 and lesson_value(lesson, "Status") == "Published", "a published lesson cannot be deleted")

    check(form(admin, edit, edit, subject_fields(name=SUBJECT + " moi", grades=(3,)))[0] == 302 and grades_of(sid) == ["3"], "an unused grade can be dropped")
    check(form(admin, edit, edit, subject_fields(name=SUBJECT + " moi", grades=(2,)))[0] == 200 and grades_of(sid) == ["3"],
          "a grade that still has lessons cannot be dropped")
    form(admin, edit, edit, subject_fields(name=SUBJECT + " moi"))
    check(form(admin, edit, f"/admin-catalog/subjects/{sid}/delete", [])[0] == 302 and subject_id() == sid, "a subject with lessons cannot be deleted")
    status, headers, _ = form(admin, create, create, lesson_fields(sid, 2, DRAFT))
    draft = headers.get("Location", "").rsplit("/", 1)[-1]
    check(status == 302 and lesson_value(draft, "Status") == "Draft", "a second lesson stays a draft")
    check(sql(f"SELECT COUNT(*) FROM dbo.AuditLogs WHERE EntityType=N'Lesson' AND EntityId=N'{lesson}';") != "0", "lesson changes are audited")
    return lesson, draft


def library_checks(admin, parent, sid, edit, lesson, draft):
    status, _, body = parent.request("/library")
    check(status == 200 and TITLE in body and DRAFT not in body, "the library lists published lessons only")
    check("<script>alert(1)" not in parent.request("/library", raw=True)[2], "lesson titles are encoded in the library")
    check(TITLE in parent.request(f"/library?subject={KEY}")[2] and TITLE not in parent.request("/library?subject=toan")[2], "the subject filter works")
    check(TITLE in parent.request("/library?grade=3")[2] and TITLE not in parent.request("/library?grade=1")[2], "the grade filter works")
    check(TITLE in parent.request("/library?type=Mixed")[2] and TITLE not in parent.request("/library?type=Video")[2], "the type filter works")
    tag = sql(f"SELECT Id FROM dbo.Tags WHERE Name=N'{TAGS[0]}';")
    check(TITLE in parent.request(f"/library?tag={tag}")[2], "the tag filter works")
    check(TITLE in parent.request(f"/library?q={TAGS[1]}")[2] and TITLE not in parent.request("/library?q=khongcobai" + RUN)[2], "search covers titles and tags")
    status, _, body = parent.request(f"/lesson-detail/{lesson}")
    raw = parent.request(f"/lesson-detail/{lesson}", raw=True)[2]
    video_id = sql(f"SELECT v.VideoId FROM dbo.LessonContents c JOIN dbo.SourceVideos v ON v.Id=c.SourceVideoId WHERE c.LessonId={lesson};")
    check(status == 200 and f"Dòng một {RUN[:6]}" in body and f"youtube-nocookie.com/embed/{video_id}" in raw, "the detail page shows the reading and the approved video")
    check(all(path.name in raw for path in saved_files), "the detail page links the PDF and shows the image")
    check(parent.request(f"/lesson-detail/{draft}")[0] == 404, "drafts are hidden from parents")
    check(parent.request("/lesson-detail")[0] == 302, "the old detail address returns to the library")
    video = f"(SELECT SourceVideoId FROM dbo.LessonContents WHERE LessonId={lesson} AND ContentKind=N'Video')"
    sql(f"UPDATE dbo.SourceVideos SET Status=N'Blocked' WHERE Id={video};")
    try:
        check("youtube-nocookie" not in parent.request(f"/lesson-detail/{lesson}", raw=True)[2], "a video blocked after publishing is no longer embedded")
    finally:
        sql(f"UPDATE dbo.SourceVideos SET Status=N'Approved' WHERE Id={video};")

    check(f'"key":"{KEY}"' in parent.request("/profiles", raw=True)[2], "parent pages receive the subject catalog")
    check(child_post(parent, "/children", {"name": "Bé thử", "cls": 2, "subjects": [KEY, "toan"], "consent": True}) == 200, "parents can choose the new subject for a child")
    child = sql(f"SELECT c.Id FROM dbo.ChildProfiles c JOIN dbo.Users u ON u.Id=c.ParentId WHERE u.Email=N'{PARENT}';")
    check(sql(f"SELECT COUNT(*) FROM dbo.ChildSubjects WHERE ChildProfileId={child} AND SubjectId={sid};") == "1", "the child's new subject is stored")
    check(child_post(parent, "/children", {"name": "Bé lạ", "cls": 2, "subjects": ["khongcomon"], "consent": True}) == 409, "unknown subjects are refused")

    check(form(admin, edit, edit, subject_fields(name=SUBJECT + " moi", active=False))[0] == 302
          and sql(f"SELECT IsActive FROM dbo.Subjects WHERE Id={sid};") == "0", "Admin hides the subject")
    check(TITLE not in parent.request("/library")[2] and parent.request(f"/lesson-detail/{lesson}")[0] == 404, "lessons of a hidden subject leave the library")
    check(child_post(parent, "/children", {"name": "Bé ẩn", "cls": 2, "subjects": [KEY], "consent": True}) == 409, "a hidden subject cannot be added to a child")
    check(child_post(parent, f"/children/{child}", {"name": "Bé thử", "cls": 2, "subjects": [KEY, "toan"]}) == 200, "a child keeps a subject hidden later")
    check(child_post(parent, f"/children/{child}", {"name": "Bé thử", "cls": 2, "subjects": ["toan"]}) == 200
          and sql(f"SELECT COUNT(*) FROM dbo.ChildSubjects WHERE ChildProfileId={child} AND SubjectId={sid};") == "0", "parents can drop a hidden subject")
    page = f"/admin-lesson/{lesson}"
    check(set_status(admin, lesson, "Draft") == 302 and lesson_value(lesson, "Status") == "Draft", "Admin unpublishes the lesson")
    check(set_status(admin, lesson, "Published") == 302 and lesson_value(lesson, "Status") == "Draft" and "Môn của bài đang ẩn" in admin.request(page)[2],
          "lessons of a hidden subject cannot be published")
    set_status(admin, lesson, "Archived")
    check(form(admin, page, f"{page}/delete", [])[0] == 302 and sql(f"SELECT COUNT(*) FROM dbo.Lessons WHERE Id={lesson};") == "0"
          and sql(f"SELECT COUNT(*) FROM dbo.LessonContents WHERE LessonId={lesson};") == "0", "Admin deletes an archived lesson and its contents")
    check(not any(path.exists() for path in saved_files), "deleting a lesson removes its files")
    form(admin, f"/admin-lesson/{draft}", f"/admin-lesson/{draft}/delete", [])
    check(form(admin, edit, f"/admin-catalog/subjects/{sid}/delete", [])[0] == 302 and subject_id() == "", "an unused subject can be deleted")


def main():
    try:
        with tempfile.TemporaryDirectory(prefix="safelearn-private-mail-") as temporary:
            directory = Path(temporary)
            url = free_url()
            with smoke.running_app(url, directory) as anonymous:
                register(directory, url, ADMIN, "Catalog admin")
                register(directory, url, PARENT, "Catalog parent")
                admin_id = sql(f"SELECT Id FROM dbo.Users WHERE Email=N'{ADMIN}';")
                sql(f"DELETE FROM dbo.UserRoles WHERE UserId=N'{admin_id}';")
                sql(f"INSERT dbo.UserRoles(UserId, RoleId) SELECT N'{admin_id}', Id FROM dbo.Roles WHERE NormalizedName=N'ADMIN';")
                admin, parent = smoke.Client(url), smoke.Client(url)
                check(admin.post_form("/login", {"Email": ADMIN, "Password": PASSWORD})[0] == 302
                      and parent.post_form("/login", {"Email": PARENT, "Password": PASSWORD})[0] == 302, "admin and parent sign in")
                sid, edit = subject_checks(admin, parent, anonymous)
                lesson, draft = lesson_checks(admin, sid, edit)
                library_checks(admin, parent, sid, edit, lesson, draft)
    finally:
        emails = f"N'{ADMIN}', N'{PARENT}'"
        ids = f"SELECT Id FROM dbo.Users WHERE Email IN ({emails})"
        for url in sql(f"SELECT c.FileUrl FROM dbo.LessonContents c JOIN dbo.Lessons l ON l.Id=c.LessonId JOIN dbo.Subjects s ON s.Id=l.SubjectId "
                       f"WHERE s.Code=N'{CODE}' AND c.FileUrl IS NOT NULL;").split():
            (smoke.ROOT / "wwwroot" / url.lstrip("/")).unlink(missing_ok=True)
        for path in saved_files:
            path.unlink(missing_ok=True)
        sql(f"DELETE FROM dbo.Lessons WHERE SubjectId IN (SELECT Id FROM dbo.Subjects WHERE Code=N'{CODE}');")
        sql(f"DELETE FROM dbo.ChildProfiles WHERE ParentId IN ({ids});")
        sql(f"DELETE FROM dbo.Subjects WHERE Code=N'{CODE}';")
        sql(f"DELETE FROM dbo.Tags WHERE Name IN (N'{TAGS[0]}', N'{TAGS[1]}');")
        sql(f"DELETE FROM dbo.AuditLogs WHERE ActorId IN ({ids}) OR EntityId IN ({ids});")
        sql(f"DELETE FROM dbo.LoginHistories WHERE UserNameTried IN ({emails});")
        sql(f"DELETE FROM dbo.Users WHERE Email IN ({emails});")
        check(sql(f"SELECT COUNT(*) FROM dbo.Users WHERE Email IN ({emails});") == "0"
              and sql(f"SELECT COUNT(*) FROM dbo.Subjects WHERE Code=N'{CODE}';") == "0"
              and sql(f"SELECT COUNT(*) FROM dbo.Tags WHERE Name IN (N'{TAGS[0]}', N'{TAGS[1]}');") == "0", "generated catalog data is cleaned up")
    print(f"RESULT {smoke.checks}/{smoke.checks} passed (catalog + lessons + library + child subjects)")


if __name__ == "__main__":
    main()
