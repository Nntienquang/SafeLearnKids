"""Exercise subject catalog management against the built root app and real SQL.

Uses the existing smoke harness, disposable UUID accounts and private email pickup.
The InMemory check confirms the page explains SQL is required. Build first.
Removes only its generated data.
"""

from html import unescape
import importlib.util
from pathlib import Path
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
ADMIN = f"sql-smoke-catalog-admin-{RUN}@example.invalid"
PARENT = f"sql-smoke-catalog-parent-{RUN}@example.invalid"
PASSWORD = f"CatalogTest1{RUN}"
CODE = f"SMOKE_{RUN[:20]}".upper()
NAME = "<b>Môn smoke</b>"


def free_url():
    with socket.socket() as listener:
        listener.bind(("127.0.0.1", 0))
        return f"http://127.0.0.1:{listener.getsockname()[1]}"


def register(client, directory, email, name):
    status, _, _ = client.post_form("/register", {
        "FullName": name, "Email": email, "Password": PASSWORD, "ConfirmPassword": PASSWORD, "Agree": "true"})
    check(status == 302, f"registers {name}")
    check(smoke.confirm(client, smoke.mail_link(directory, email, "/confirm-email"))[0] == 302, f"confirms {name}")


def send(client, page, path, fields, token=True):
    """Post repeated fields such as GradeLevelIds the way a browser does."""
    pairs = list(fields)
    if token:
        pairs.append(("__RequestVerificationToken", smoke.token_from(client.request(page)[2])))
    request = urllib.request.Request(client.url + path, data=urllib.parse.urlencode(pairs).encode())
    try:
        response = client.opener.open(request, timeout=10)
    except urllib.error.HTTPError as error:
        response = error
    with response:
        return response.code, response.headers, unescape(response.read().decode("utf-8", "replace"))


def subject_fields(code, name, grades, **extra):
    values = {"Code": code, "Name": name, "Description": "Smoke", "SortOrder": "90", "IconKey": "star", "ColorKey": "v", **extra}
    return list(values.items()) + [("GradeLevelIds", str(grade)) for grade in grades]


def texts(column, rest):
    """Read nvarchar values as UTF-16 hex, since sqlcmd output loses Vietnamese letters."""
    rows = sql(f"SELECT CONVERT(varchar(1000), CAST({column} AS varbinary(500)), 2) {rest};").split()
    return [bytes.fromhex(row).decode("utf-16-le") for row in rows]


def grade_id(level):
    return sql(f"SELECT Id FROM dbo.GradeLevels WHERE Level={level};")


def subject_id():
    return sql(f"SELECT Id FROM dbo.Subjects WHERE Code=N'{CODE}';")


def grades_of(subject):
    return sql(f"SELECT STRING_AGG(CAST(g.Level AS nvarchar(5)), N',') WITHIN GROUP (ORDER BY g.Level) FROM dbo.SubjectGrades sg "
               f"JOIN dbo.GradeLevels g ON g.Id=sg.GradeLevelId WHERE sg.SubjectId={subject};")


def audit_count(subject, action):
    return sql(f"SELECT COUNT(*) FROM dbo.AuditLogs WHERE EntityType=N'Subject' AND EntityId=N'{subject}' AND Action=N'{action}';")


def sql_checks(url, directory):
    with smoke.running_app(url, directory) as anonymous:
        admin, parent = smoke.Client(url), smoke.Client(url)
        register(anonymous, directory, ADMIN, "Catalog smoke admin")
        register(parent, directory, PARENT, "Catalog smoke parent")
        admin_id = sql(f"SELECT Id FROM dbo.Users WHERE Email=N'{ADMIN}';")
        sql(f"DELETE ur FROM dbo.UserRoles ur WHERE ur.UserId=N'{admin_id}';")
        sql(f"INSERT dbo.UserRoles(UserId, RoleId) SELECT N'{admin_id}', Id FROM dbo.Roles WHERE NormalizedName=N'ADMIN';")

        check(anonymous.request("/admin-catalog")[0] == 302, "anonymous users are sent to login")
        check(parent.post_form("/login", {"Email": PARENT, "Password": PASSWORD})[0] == 302, "Parent signs in")
        for path in ("/admin-catalog", "/admin-catalog/subjects/new"):
            status, headers, _ = parent.request(path)
            check(status == 302 and "/error/403" in headers.get("Location", ""), f"Parent is denied {path}")
        check(admin.post_form("/login", {"Email": ADMIN, "Password": PASSWORD})[0] == 302, "Admin signs in")

        status, _, body = admin.request("/admin-catalog")
        names = texts("Name", "FROM dbo.Subjects")
        check(status == 200 and all(name in body for name in names), "catalog lists every SQL subject")
        math = sql("SELECT Id FROM dbo.Subjects WHERE Code=N'MATH';")
        published = texts("Title", f"FROM dbo.Lessons WHERE SubjectId={math} AND Status=N'Published'")
        others = texts("Title", f"FROM dbo.Lessons WHERE SubjectId={math} AND Status<>N'Published'")
        _, _, body = admin.request(f"/admin-catalog?subject={math}&status=Published")
        check(all(title in body for title in published) and not any(title in body for title in others), "lessons filter by status")
        check(admin.request(f"/admin-catalog?subject={math}&status=Bogus&grade=999")[0] == 200, "unknown filters are ignored")

        page = "/admin-catalog/subjects/new"
        check(send(admin, page, page, subject_fields(CODE, NAME, [grade_id(1)]), token=False)[0] == 400 and subject_id() == "",
              "creating a subject requires CSRF")
        check(send(admin, page, page, subject_fields(CODE, NAME, []))[0] == 200 and subject_id() == "", "a subject needs a grade")
        check(send(admin, page, page, subject_fields(CODE, NAME, [grade_id(1)], IconKey="evil"))[0] == 200 and subject_id() == "",
              "unknown icons are refused")
        check(send(admin, page, page, subject_fields("bad code!", NAME, [grade_id(1)]))[0] == 200, "invalid codes are refused")
        status, headers, _ = send(admin, page, page, subject_fields(CODE.lower(), NAME, [grade_id(1), grade_id(2)]))
        created = subject_id()
        check(status == 302 and created != "" and grades_of(created) == "1,2", "Admin creates a subject with grades")
        check(audit_count(created, "CreateSubject") == "1", "creation is audited")
        status, _, body = send(admin, page, page, subject_fields(CODE, "Duplicate", [grade_id(1)]))
        check(status == 200 and "Mã môn đã tồn tại" in body, "duplicate codes are refused")
        raw = admin.request(f"/admin-catalog?subject={created}", raw=True)[2]
        check(NAME not in raw and "&lt;b&gt;" in raw, "subject names are HTML encoded")

        edit = f"/admin-catalog/subjects/{created}/edit"
        status, _, _ = send(admin, edit, edit, subject_fields("HIJACK", "Môn smoke 2", [grade_id(2), grade_id(3)]))
        check(status == 302 and texts("Code + N'|' + Name", f"FROM dbo.Subjects WHERE Id={created}") == [f"{CODE}|Môn smoke 2"]
              and grades_of(created) == "2,3", "edits save but the code stays fixed")
        lesson_grade = sql(f"SELECT TOP 1 g.Level FROM dbo.Lessons l JOIN dbo.GradeLevels g ON g.Id=l.GradeLevelId WHERE l.SubjectId={math};")
        math_edit = f"/admin-catalog/subjects/{math}/edit"
        math_name = texts("Name", f"FROM dbo.Subjects WHERE Id={math}")[0]
        before = grades_of(math)
        other_grade = grade_id(5 if lesson_grade != "5" else 4)
        status, _, body = send(admin, math_edit, math_edit, subject_fields("MATH", math_name, [other_grade]))
        check(status == 200 and "không bỏ được lớp" in body and grades_of(math) == before, "a grade with lessons cannot be dropped")

        visibility = f"/admin-catalog/subjects/{created}/visibility"
        check(send(admin, edit, visibility, [("active", "false")])[0] == 302
              and sql(f"SELECT CAST(IsActive AS int) FROM dbo.Subjects WHERE Id={created};") == "0", "Admin hides a subject")
        check("Đã ẩn" in admin.request(f"/admin-catalog?subject={created}")[2] and audit_count(created, "HideSubject") == "1", "hidden subjects are marked and audited")
        check(send(admin, edit, visibility, [("active", "true")])[0] == 302
              and sql(f"SELECT CAST(IsActive AS int) FROM dbo.Subjects WHERE Id={created};") == "1", "Admin shows a subject again")

        check(send(admin, math_edit, f"/admin-catalog/subjects/{math}/delete", [])[0] == 302
              and sql(f"SELECT COUNT(*) FROM dbo.Subjects WHERE Id={math};") == "1", "subjects in use cannot be deleted")
        check(send(admin, edit, f"/admin-catalog/subjects/{created}/delete", [])[0] == 302 and subject_id() == ""
              and sql(f"SELECT COUNT(*) FROM dbo.SubjectGrades WHERE SubjectId={created};") == "0", "Admin deletes an unused subject")
        check(audit_count(created, "DeleteSubject") == "1", "deletion is audited")
        check(admin.request("/admin-catalog/subjects/999999/edit")[0] == 404, "unknown subjects return 404")
        return created


def memory_checks(url, directory):
    email, password = f"catalog-bootstrap-{RUN}@example.invalid", f"Bootstrap1{RUN}"
    overrides = {"Demo__UseInMemoryDatabase": "true", "Bootstrap__AdminEmail": email, "Bootstrap__AdminPassword": password}
    with smoke.running_app(url, directory, overrides) as client:
        check(client.post_form("/login", {"Email": email, "Password": password})[0] == 302, "InMemory Admin signs in")
        status, _, body = client.request("/admin-catalog")
        check(status == 200 and "chỉ có khi chạy với SQL Server" in body, "catalog explains SQL is required in InMemory mode")


def main():
    created = None
    try:
        with tempfile.TemporaryDirectory(prefix="safelearn-private-mail-") as temporary:
            directory = Path(temporary)
            created = sql_checks(free_url(), directory)
            memory_checks(free_url(), directory)
    finally:
        emails = f"N'{ADMIN}', N'{PARENT}'"
        ids = f"SELECT Id FROM dbo.Users WHERE Email IN ({emails})"
        leftover = sql(f"SELECT Id FROM dbo.Subjects WHERE Code=N'{CODE}';")
        if leftover:
            sql(f"DELETE FROM dbo.SubjectGrades WHERE SubjectId={leftover}; DELETE FROM dbo.Subjects WHERE Id={leftover};")
        for item in {created, leftover} - {None, ""}:
            sql(f"DELETE FROM dbo.AuditLogs WHERE EntityType=N'Subject' AND EntityId=N'{item}';")
        sql(f"DELETE FROM dbo.AuditLogs WHERE EntityId IN ({ids}) OR ActorId IN ({ids});")
        sql(f"DELETE FROM dbo.LoginHistories WHERE UserNameTried IN ({emails});")
        sql(f"DELETE FROM dbo.UserConsents WHERE UserId IN ({ids});")
        sql(f"DELETE FROM dbo.Users WHERE Email IN ({emails});")
        check(sql(f"SELECT COUNT(*) FROM dbo.Users WHERE Email IN ({emails});") == "0", "generated accounts are cleaned up")
    print(f"RESULT {smoke.checks}/{smoke.checks} passed (subject catalog)")


if __name__ == "__main__":
    main()
