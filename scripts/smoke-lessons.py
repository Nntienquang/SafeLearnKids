"""Exercise lesson management against the built root app and real SQL.

Uses the existing smoke harness, disposable UUID accounts and private email pickup.
Build first. Removes only its generated data; seed lessons are never changed.
"""

from html import unescape
import importlib.util
from pathlib import Path
import re
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
ADMIN = f"sql-smoke-lesson-admin-{RUN}@example.invalid"
PARENT = f"sql-smoke-lesson-parent-{RUN}@example.invalid"
PASSWORD = f"LessonTest1{RUN}"
PREFIX = f"Smoke {RUN}"
CONFLICT = "Bài đã được người khác thay đổi"


def free_url():
    with socket.socket() as listener:
        listener.bind(("127.0.0.1", 0))
        return f"http://127.0.0.1:{listener.getsockname()[1]}"


def register(client, directory, email, name):
    status, _, _ = client.post_form("/register", {
        "FullName": name, "Email": email, "Password": PASSWORD, "ConfirmPassword": PASSWORD, "Agree": "true"})
    check(status == 302, f"registers {name}")
    check(smoke.confirm(client, smoke.mail_link(directory, email, "/confirm-email"))[0] == 302, f"confirms {name}")


def send(client, page, path, fields, token=True, raw=False):
    """Post repeated fields the way a browser does, with the page's antiforgery token."""
    pairs = list(fields.items()) if isinstance(fields, dict) else list(fields)
    if token:
        pairs.append(("__RequestVerificationToken", smoke.token_from(client.request(page)[2])))
    request = urllib.request.Request(client.url + path, data=urllib.parse.urlencode(pairs).encode())
    try:
        response = client.opener.open(request, timeout=10)
    except urllib.error.HTTPError as error:
        response = error
    with response:
        body = response.read().decode("utf-8", "replace")
        return response.code, response.headers, body if raw else unescape(body)


def texts(column, rest):
    """Read nvarchar values as UTF-16 hex, since sqlcmd output loses Vietnamese letters."""
    rows = sql(f"SELECT CONVERT(varchar(1000), CAST({column} AS varbinary(500)), 2) {rest};").split()
    return [bytes.fromhex(row).decode("utf-16-le") for row in rows]


def smoke_count():
    return sql(f"SELECT COUNT(*) FROM dbo.Lessons WHERE Title LIKE N'{PREFIX}%';")


def lesson(lesson_id, columns):
    return sql(f"SELECT {columns} FROM dbo.Lessons WHERE Id={lesson_id};")


def audit_count(lesson_id, action):
    return sql(f"SELECT COUNT(*) FROM dbo.AuditLogs WHERE EntityType=N'Lesson' AND EntityId=N'{lesson_id}' AND Action=N'{action}';")


def row_version(body):
    match = re.search(r'name="RowVersion"[^>]*value="([^"]*)"|value="([^"]*)"[^>]*name="RowVersion"', body)
    return (match.group(1) or match.group(2)) if match else ""


def grade_options(body):
    select = re.search(r'<select[^>]*name="GradeLevelId".*?</select>', body, re.S)
    return set(re.findall(r'<option value="(\d+)"', select.group(0))) if select else set()


def lesson_fields(subject, grade, title, **extra):
    return {"SubjectId": str(subject), "GradeLevelId": str(grade), "Title": title, "Summary": "Smoke summary",
            "SortOrder": "", **extra}


def create_lesson(admin, subject, grade, title):
    page = f"/admin-lessons/new?subject={subject}"
    status, headers, _ = send(admin, page, "/admin-lessons/new", lesson_fields(subject, grade, title))
    location = headers.get("Location", "")
    match = re.fullmatch(r"/admin-lessons/(\d+)", location)
    return status, (match.group(1) if match else "")


def us1_checks(url, anonymous, admin, parent, admin_id):
    math = sql("SELECT Id FROM dbo.Subjects WHERE Code=N'MATH';")
    grades = set(sql(f"SELECT GradeLevelId FROM dbo.SubjectGrades WHERE SubjectId={math};").split())
    grade = sorted(grades)[0]
    other_grade = sql(f"SELECT TOP 1 Id FROM dbo.GradeLevels WHERE Id NOT IN (SELECT GradeLevelId FROM dbo.SubjectGrades WHERE SubjectId={math});")
    new_page = f"/admin-lessons/new?subject={math}"
    seed = sql("SELECT MIN(Id) FROM dbo.Lessons;")

    for path in (new_page, f"/admin-lessons/{seed}"):
        status, headers, _ = anonymous.request(path)
        check(status == 302 and "/login" in headers.get("Location", ""), f"anonymous users are sent to login from {path}")
        status, headers, _ = parent.request(path)
        check(status == 302 and "/error/403" in headers.get("Location", ""), f"Parent is denied {path}")
    status, headers, _ = admin.request("/admin-lesson")
    check(status == 302 and headers.get("Location") == "/admin-catalog", "old lesson page redirects to the catalog")
    check(admin.request("/admin-lessons/new?subject=999999")[0] == 404, "unknown subject returns 404")

    status, _, body = admin.request(new_page, raw=True)
    check(status == 200 and grade_options(body) == grades, "new lesson form offers only the subject's grades")
    check(send(admin, new_page, "/admin-lessons/new", lesson_fields(math, grade, f"{PREFIX} no token"), token=False)[0] == 400
          and smoke_count() == "0", "creating a lesson requires CSRF")
    invalid = {
        "blank title": lesson_fields(math, grade, "   "),
        "long title": lesson_fields(math, grade, PREFIX + "x" * (201 - len(PREFIX))),
        "long summary": lesson_fields(math, grade, f"{PREFIX} long", Summary="s" * 501),
        "unassigned grade": lesson_fields(math, other_grade, f"{PREFIX} grade"),
    }
    for label, fields in invalid.items():
        status, _, _ = send(admin, new_page, "/admin-lessons/new", fields)
        check(status == 200 and smoke_count() == "0", f"{label} is refused without creating a lesson")

    expected_order = int(sql(f"SELECT ISNULL(MAX(SortOrder), 0) FROM dbo.Lessons WHERE SubjectId={math} AND GradeLevelId={grade};")) + 1
    status, created = create_lesson(admin, math, grade, f"{PREFIX} Bài một")
    check(status == 302 and created != "", "Admin creates a draft lesson")
    check(lesson(created, "Status + N'|' + LessonType + N'|' + CreatedById + N'|' + CAST(SortOrder AS nvarchar(10)) + N'|' + CAST(DurationMin AS nvarchar(10))")
          == f"Draft|Video|{admin_id}|{expected_order}|0", "new lesson is an empty Video draft owned by the admin at the end of its grade")
    check(audit_count(created, "CreateLesson") == "1", "creation is audited")
    catalog = admin.request(f"/admin-catalog?subject={math}", raw=True)[2]
    check(f'href="/admin-lessons/new?subject={math}"' in catalog and f'href="/admin-lessons/{created}"' in catalog,
          "catalog links to the new lesson form and the lesson editor")

    editor = f"/admin-lessons/{created}"
    status, _, body = admin.request(editor, raw=True)
    check(status == 200 and "Bản nháp" in unescape(body), "editor shows the draft")
    old_version = row_version(body)
    check(old_version != "", "editor carries the lesson RowVersion")
    edited = lesson_fields(math, grade, f"{PREFIX} Bài sửa", Summary="Đã sửa", DurationMin="999", SortOrder="7", RowVersion=old_version)
    status, headers, _ = send(admin, editor, editor, edited)
    check(status == 302 and texts("Title + N'|' + Summary", f"FROM dbo.Lessons WHERE Id={created}") == [f"{PREFIX} Bài sửa|Đã sửa"]
          and lesson(created, "CAST(DurationMin AS nvarchar(10)) + N'|' + CAST(SortOrder AS nvarchar(10))") == "0|7",
          "Admin edits the lesson information but cannot type a duration")
    check(audit_count(created, "UpdateLesson") == "1", "edits are audited")
    stale = lesson_fields(math, grade, f"{PREFIX} Bài cũ", RowVersion=old_version)
    status, _, body = send(admin, editor, editor, stale)
    check(status == 200 and CONFLICT in body and f"{PREFIX} Bài cũ" in body
          and texts("Title", f"FROM dbo.Lessons WHERE Id={created}") == [f"{PREFIX} Bài sửa"],
          "a stale save is refused, keeps the typed data and changes nothing")
    status, _, body = send(admin, editor, editor, {**edited, "RowVersion": row_version(admin.request(editor, raw=True)[2]),
                                                   "GradeLevelId": other_grade})
    check(status == 200 and texts("Title", f"FROM dbo.Lessons WHERE Id={created}") == [f"{PREFIX} Bài sửa"],
          "an edit cannot move the lesson to an unassigned grade")
    check(admin.request("/admin-lessons/999999")[0] == 404, "unknown lessons return 404")

    status, encoded = create_lesson(admin, math, grade, f"{PREFIX} <b>x</b>")
    raw = admin.request(f"/admin-lessons/{encoded}", raw=True)[2]
    check(status == 302 and "&lt;b&gt;x" in raw and "<b>x</b>" not in raw, "lesson titles are HTML encoded")
    return math, grade


def select_options(body, name):
    select = re.search(rf'<select[^>]*name="{name}".*?</select>', body, re.S)
    return set(re.findall(r'<option value="(\d+)"', select.group(0))) if select else set()


def minutes(seconds):
    return (seconds + 59) // 60


def contents_of(lesson_id):
    """Kind and title of each part in order, e.g. ['Video|Title', 'Reading|Title']."""
    return texts("ContentKind + N'|' + ISNULL(Title, N'')", f"FROM dbo.LessonContents WHERE LessonId={lesson_id} ORDER BY SortOrder")


def us2_checks(admin, math, grade):
    status, lesson_id = create_lesson(admin, math, grade, f"{PREFIX} Nội dung")
    check(status == 302 and lesson_id != "", "Admin creates a draft for content checks")
    editor = f"/admin-lessons/{lesson_id}"
    add = f"{editor}/contents"
    body = admin.request(editor, raw=True)[2]
    approved = set(sql("SELECT Id FROM dbo.SourceVideos WHERE Status=N'Approved';").split())
    blocked = sql("SELECT TOP 1 Id FROM dbo.SourceVideos WHERE Status<>N'Approved';")
    check(select_options(body, "SourceVideoId") == approved, "the video list offers only approved videos")
    check(blocked != "", "seed data has a video that is not approved")

    version = row_version(body)
    status, _, _ = send(admin, editor, add, {"RowVersion": version, "ContentKind": "Video", "SourceVideoId": blocked})
    check(status == 200 and contents_of(lesson_id) == [], "a video that is not approved cannot be added")
    video = sorted(approved)[0]
    video_title = texts("Title", f"FROM dbo.SourceVideos WHERE Id={video}")[0]
    status, _, _ = send(admin, editor, add, {"RowVersion": version, "ContentKind": "Video", "SourceVideoId": video, "Title": ""})
    video_seconds = int(sql(f"SELECT ISNULL(DurationSec, 0) FROM dbo.SourceVideos WHERE Id={video};"))
    check(status == 302 and contents_of(lesson_id) == [f"Video|{video_title[:150]}"]
          and lesson(lesson_id, "LessonType") == "Video", "Admin adds an approved video titled after the video")
    check(lesson(lesson_id, "DurationMin") == str(minutes(video_seconds)), "the lesson length follows the video length")
    video_key = sql(f"SELECT VideoId FROM dbo.SourceVideos WHERE Id={video};")
    preview = admin.request(editor, raw=True)[2]
    check(f'src="https://www.youtube-nocookie.com/embed/{video_key}"' in preview and 'loading="lazy"' in preview
          and 'referrerpolicy="strict-origin-when-cross-origin"' in preview, "a video part offers a lazy privacy-enhanced YouTube preview")
    status, _, page = send(admin, editor, add, {"RowVersion": version, "ContentKind": "Reading", "Title": "Đọc cũ", "Body": "Nội dung đang gõ"})
    check(status == 200 and CONFLICT in page and "Đọc cũ" in page and "Nội dung đang gõ" in page
          and len(contents_of(lesson_id)) == 1, "a stale add keeps the typed content and adds nothing")

    version = row_version(admin.request(editor, raw=True)[2])
    for label, fields in {"blank reading": {"Title": "Trống", "Body": "   "},
                          "over-long reading": {"Title": "Dài", "Body": "a" * 20001},
                          "over-long title": {"Title": "t" * 151, "Body": "ok"}}.items():
        status, _, _ = send(admin, editor, add, {"RowVersion": version, "ContentKind": "Reading", **fields})
        check(status == 200 and len(contents_of(lesson_id)) == 1, f"{label} is refused")
    script = "<script>alert(1)</script>"
    status, _, _ = send(admin, editor, add, {"RowVersion": version, "ContentKind": "Reading", "Title": "Bài đọc", "Body": script})
    raw = admin.request(editor, raw=True)[2]
    check(status == 302 and "&lt;script&gt;alert(1)" in raw and script not in raw, "reading text is shown encoded")
    check(lesson(lesson_id, "LessonType") == "Mixed", "lesson type becomes Mixed with a video and a reading")
    long_reading = " ".join(["chữ"] * 250)
    status, _, _ = send(admin, editor, add, {"RowVersion": row_version(raw), "ContentKind": "Reading", "Title": "Dài", "Body": long_reading})
    check(status == 302 and lesson(lesson_id, "DurationMin") == str(minutes(video_seconds + 250 * 60 // 100 + 1 * 60 // 100)),
          "a reading adds its estimated reading time at 100 words a minute")
    long_id = sql(f"SELECT Id FROM dbo.LessonContents WHERE LessonId={lesson_id} AND Title=N'Dài';")
    page = admin.request(editor, raw=True)[2]
    check(send(admin, editor, f"{editor}/contents/{long_id}/delete", {"RowVersion": row_version(page)})[0] == 302
          and lesson(lesson_id, "DurationMin") == str(minutes(video_seconds)), "removing a part shortens the lesson again")
    raw = admin.request(editor, raw=True)[2]

    reading = sql(f"SELECT Id FROM dbo.LessonContents WHERE LessonId={lesson_id} AND ContentKind=N'Reading';")
    version = row_version(raw)
    check(send(admin, editor, f"{editor}/contents/{reading}/move", {"RowVersion": version, "direction": "up"})[0] == 302
          and contents_of(lesson_id) == ["Reading|Bài đọc", f"Video|{video_title[:150]}"], "Admin moves the reading above the video")
    check(send(admin, editor, f"{editor}/contents/{reading}/move", {"RowVersion": version, "direction": "down"})[0] == 302
          and contents_of(lesson_id)[0] == "Reading|Bài đọc", "a move with a stale RowVersion changes nothing")
    version = row_version(admin.request(editor, raw=True)[2])
    status, _, _ = send(admin, editor, f"{editor}/contents/{reading}", {"RowVersion": version, "Title": "Bài đọc mới", "Body": "Đã sửa"})
    check(status == 302 and texts("Title + N'|' + Body", f"FROM dbo.LessonContents WHERE Id={reading}") == ["Bài đọc mới|Đã sửa"],
          "Admin edits a reading")
    seed_content = sql("SELECT MIN(Id) FROM dbo.LessonContents;")
    version = row_version(admin.request(editor, raw=True)[2])
    check(send(admin, editor, f"{editor}/contents/{seed_content}/delete", {"RowVersion": version})[0] == 404,
          "a part of another lesson returns 404")
    check(send(admin, editor, f"{editor}/contents/{reading}/delete", {"RowVersion": version})[0] == 302
          and contents_of(lesson_id) == [f"Video|{video_title[:150]}"]
          and sql(f"SELECT SortOrder FROM dbo.LessonContents WHERE LessonId={lesson_id};") == "1"
          and lesson(lesson_id, "LessonType") == "Video", "deleting renumbers the parts and resets the lesson type")
    for action, count in (("AddLessonContent", "3"), ("MoveLessonContent", "1"), ("UpdateLessonContent", "1"), ("DeleteLessonContent", "2")):
        check(audit_count(lesson_id, action) == count, f"{action} is audited")

    published = sql("SELECT TOP 1 Id FROM dbo.Lessons WHERE Status<>N'Draft' ORDER BY Id;")
    before = sql(f"SELECT COUNT(*) FROM dbo.LessonContents WHERE LessonId={published};")
    page = f"/admin-lessons/{published}"
    status, _, body = send(admin, page, f"{page}/contents",
                           {"RowVersion": row_version(admin.request(page, raw=True)[2]), "ContentKind": "Reading", "Title": "x", "Body": "y"})
    check(status == 200 and "Bản nháp" in body and sql(f"SELECT COUNT(*) FROM dbo.LessonContents WHERE LessonId={published};") == before,
          "parts of a lesson that is not a draft cannot change")


def status_targets(body):
    return set(re.findall(r'name="target" value="(\w+)"', body))


def change(admin, lesson_id, target, version=None):
    editor = f"/admin-lessons/{lesson_id}"
    version = version or row_version(admin.request(editor, raw=True)[2])
    status, _, _ = send(admin, editor, f"{editor}/status", {"RowVersion": version, "target": target})
    return status, lesson(lesson_id, "Status")


def add_reading(admin, lesson_id):
    editor = f"/admin-lessons/{lesson_id}"
    return send(admin, editor, f"{editor}/contents", {"RowVersion": row_version(admin.request(editor, raw=True)[2]),
                                                      "ContentKind": "Reading", "Title": "Đọc", "Body": "Nội dung"})[0]


def us3_checks(admin, math, grade, admin_id, blocked_videos):
    status, lesson_id = create_lesson(admin, math, grade, f"{PREFIX} Xuất bản")
    editor = f"/admin-lessons/{lesson_id}"
    check(status_targets(admin.request(editor, raw=True)[2]) == {"Published"}, "a draft offers only Publish")
    check(change(admin, lesson_id, "Published") == (302, "Draft") and "Cần ít nhất 1 nội dung" in admin.request(editor)[2],
          "a lesson without content cannot be published")
    check(change(admin, lesson_id, "Archived") == (302, "Draft"), "a draft cannot be archived")
    check(add_reading(admin, lesson_id) == 302, "Admin adds a reading")
    stale = row_version(admin.request(editor, raw=True)[2])
    check(change(admin, lesson_id, "Published") == (302, "Published")
          and lesson(lesson_id, "ApprovedById + N'|' + CASE WHEN PublishedAt IS NULL THEN N'none' ELSE N'set' END") == f"{admin_id}|set",
          "Admin publishes a lesson with content and the publisher and time are recorded")
    check(change(admin, lesson_id, "Archived", stale) == (302, "Published"), "a stale status change is refused")
    raw = admin.request(editor, raw=True)[2]
    check(status_targets(raw) == {"Archived", "Draft"} and "Bé sẽ tạm không thấy bài" in unescape(raw),
          "a published lesson offers Archive and a confirmed Back to draft")
    status, _, body = send(admin, editor, editor, lesson_fields(math, grade, f"{PREFIX} khóa", RowVersion=row_version(raw)))
    check(status == 200 and texts("Title", f"FROM dbo.Lessons WHERE Id={lesson_id}") == [f"{PREFIX} Xuất bản"],
          "a published lesson's information cannot change")
    check(add_reading(admin, lesson_id) == 200 and sql(f"SELECT COUNT(*) FROM dbo.LessonContents WHERE LessonId={lesson_id};") == "1",
          "a published lesson's content cannot change")
    check(change(admin, lesson_id, "InReview") == (302, "Published"), "the old review status cannot be chosen")
    check(change(admin, lesson_id, "Archived") == (302, "Archived"), "Admin archives a published lesson")
    check(change(admin, lesson_id, "Draft") == (302, "Draft")
          and lesson(lesson_id, "ISNULL(ApprovedById, N'none') + N'|' + CASE WHEN PublishedAt IS NULL THEN N'none' ELSE N'set' END") == "none|set",
          "back to draft clears the publisher and keeps the last publish time")
    check(audit_count(lesson_id, "ChangeLessonStatus") == "3"
          and texts("Detail", f"FROM dbo.AuditLogs WHERE EntityType=N'Lesson' AND EntityId=N'{lesson_id}' AND Action=N'ChangeLessonStatus' ORDER BY Id")[0] == "Draft→Published",
          "status changes are audited with old and new status")

    legacy = sql("SELECT TOP 1 Id FROM dbo.Lessons WHERE Status=N'InReview' ORDER BY Id;")
    if legacy:
        check(status_targets(admin.request(f"/admin-lessons/{legacy}", raw=True)[2]) == {"Draft"},
              "an old in-review lesson offers only Back to draft")

    status, video_lesson = create_lesson(admin, math, grade, f"{PREFIX} Video chặn")
    page = f"/admin-lessons/{video_lesson}"
    video = sql("SELECT TOP 1 Id FROM dbo.SourceVideos WHERE Status=N'Approved' ORDER BY Id;")
    send(admin, page, f"{page}/contents", {"RowVersion": row_version(admin.request(page, raw=True)[2]), "ContentKind": "Video", "SourceVideoId": video})
    blocked_videos.append(video)
    sql(f"UPDATE dbo.SourceVideos SET Status=N'Blocked' WHERE Id={video};")
    title = texts("Title", f"FROM dbo.SourceVideos WHERE Id={video}")[0]
    check(change(admin, video_lesson, "Published") == (302, "Draft") and title in admin.request(page)[2]
          and "Không còn hợp lệ" in admin.request(page)[2], "a lesson whose video was blocked cannot be published and names the video")


def us4_checks(admin, math, grade):
    seed = sql("SELECT TOP 1 l.Id FROM dbo.Lessons l WHERE l.Status=N'Draft' AND EXISTS (SELECT 1 FROM dbo.LessonActivities a WHERE a.LessonId=l.Id) ORDER BY l.Id;")
    if seed:
        page = f"/admin-lessons/{seed}"
        status, _, _ = send(admin, page, f"{page}/delete", {"RowVersion": row_version(admin.request(page, raw=True)[2])})
        check(status == 302 and sql(f"SELECT COUNT(*) FROM dbo.Lessons WHERE Id={seed};") == "1"
              and "Hãy lưu trữ bài" in admin.request(page)[2], "a draft with learning data is not deleted and archiving is suggested")
    status, published = create_lesson(admin, math, grade, f"{PREFIX} Không xóa")
    add_reading(admin, published)
    change(admin, published, "Published")
    page = f"/admin-lessons/{published}"
    raw = admin.request(page, raw=True)[2]
    check(f'action="/admin-lessons/{published}/delete"' not in raw, "a published lesson shows no delete button")
    send(admin, page, f"{page}/delete", {"RowVersion": row_version(raw)})
    check(sql(f"SELECT COUNT(*) FROM dbo.Lessons WHERE Id={published};") == "1", "a published lesson cannot be deleted")

    status, doomed = create_lesson(admin, math, grade, f"{PREFIX} Xóa")
    add_reading(admin, doomed)
    page = f"/admin-lessons/{doomed}"
    stale = row_version(admin.request(page, raw=True)[2])
    add_reading(admin, doomed)
    send(admin, page, f"{page}/delete", {"RowVersion": stale})
    check(sql(f"SELECT COUNT(*) FROM dbo.Lessons WHERE Id={doomed};") == "1", "a stale delete is refused")
    raw = admin.request(page, raw=True)[2]
    check('data-danger="true"' in raw and f'action="/admin-lessons/{doomed}/delete"' in raw, "a draft shows a confirmed delete button")
    status, headers, _ = send(admin, page, f"{page}/delete", {"RowVersion": row_version(raw)})
    check(status == 302 and headers.get("Location") == f"/admin-catalog?subject={math}"
          and sql(f"SELECT COUNT(*) FROM dbo.Lessons WHERE Id={doomed};") == "0"
          and sql(f"SELECT COUNT(*) FROM dbo.LessonContents WHERE LessonId={doomed};") == "0",
          "Admin deletes a draft with its content and returns to the subject")
    check(audit_count(doomed, "DeleteLesson") == "1", "deletion is audited")


def memory_checks(url, directory):
    email, password = f"lesson-bootstrap-{RUN}@example.invalid", f"Bootstrap1{RUN}"
    overrides = {"Demo__UseInMemoryDatabase": "true", "Bootstrap__AdminEmail": email, "Bootstrap__AdminPassword": password}
    with smoke.running_app(url, directory, overrides) as client:
        check(client.post_form("/login", {"Email": email, "Password": password})[0] == 302, "InMemory Admin signs in")
        for path in ("/admin-lessons/new?subject=1", "/admin-lessons/1"):
            status, headers, _ = client.request(path)
            check(status == 302 and headers.get("Location") == "/admin-catalog", f"{path} sends InMemory admins to the catalog")
        status, _, body = client.request("/admin-catalog")
        check(status == 200 and "Bài học chỉ có khi chạy với SQL Server" in body, "the catalog explains lessons need SQL Server")


def main():
    blocked_videos = []
    try:
        with tempfile.TemporaryDirectory(prefix="safelearn-private-mail-") as temporary:
            directory = Path(temporary)
            url = free_url()
            with smoke.running_app(url, directory) as anonymous:
                admin, parent = smoke.Client(url), smoke.Client(url)
                register(anonymous, directory, ADMIN, "Lesson smoke admin")
                register(parent, directory, PARENT, "Lesson smoke parent")
                admin_id = sql(f"SELECT Id FROM dbo.Users WHERE Email=N'{ADMIN}';")
                sql(f"DELETE ur FROM dbo.UserRoles ur WHERE ur.UserId=N'{admin_id}';")
                sql(f"INSERT dbo.UserRoles(UserId, RoleId) SELECT N'{admin_id}', Id FROM dbo.Roles WHERE NormalizedName=N'ADMIN';")
                check(admin.post_form("/login", {"Email": ADMIN, "Password": PASSWORD})[0] == 302, "Admin signs in")
                check(parent.post_form("/login", {"Email": PARENT, "Password": PASSWORD})[0] == 302, "Parent signs in")
                math, grade = us1_checks(url, anonymous, admin, parent, admin_id)
                us2_checks(admin, math, grade)
                us3_checks(admin, math, grade, admin_id, blocked_videos)
                us4_checks(admin, math, grade)
            memory_checks(free_url(), directory)
    finally:
        for video in blocked_videos:
            sql(f"UPDATE dbo.SourceVideos SET Status=N'Approved' WHERE Id={video};")
        ids = f"SELECT Id FROM dbo.Lessons WHERE Title LIKE N'{PREFIX}%'"
        sql(f"DELETE FROM dbo.AuditLogs WHERE EntityType=N'Lesson' AND EntityId IN (SELECT CAST(Id AS nvarchar(64)) FROM ({ids}) AS generated);")
        sql(f"DELETE FROM dbo.Lessons WHERE Title LIKE N'{PREFIX}%';")
        emails = f"N'{ADMIN}', N'{PARENT}'"
        users = f"SELECT Id FROM dbo.Users WHERE Email IN ({emails})"
        sql(f"DELETE FROM dbo.AuditLogs WHERE ActorId IN ({users});")
        sql(f"DELETE FROM dbo.LoginHistories WHERE UserNameTried IN ({emails});")
        sql(f"DELETE FROM dbo.Users WHERE Email IN ({emails});")
        check(smoke_count() == "0", "generated lessons are cleaned up")
        check(sql(f"SELECT COUNT(*) FROM dbo.Users WHERE Email IN ({emails});") == "0", "generated accounts are cleaned up")
    print(f"RESULT {smoke.checks}/{smoke.checks} passed (lesson management)")


if __name__ == "__main__":
    main()
