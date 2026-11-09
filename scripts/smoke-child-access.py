"""US-08 (each child's own PIN) and US-10 (consent to process each child's data) on real SQL.

Uses the existing smoke harness, disposable UUID accounts and private email pickup. Build first. Removes everything it creates.
"""

import html
import importlib.util
import json
from pathlib import Path
import re
import sys
import tempfile
import urllib.error
import urllib.request
import uuid


sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location("family_smoke", Path(__file__).with_name("smoke-family.py"))
family = importlib.util.module_from_spec(spec)
spec.loader.exec_module(family)
smoke, check, sql = family.smoke, family.check, family.sql

RUN = uuid.uuid4().hex
PARENT = f"sql-smoke-access-{RUN}@example.invalid"
OTHER = f"sql-smoke-access-other-{RUN}@example.invalid"
PASSWORD = family.PASSWORD


def create_child(client, name, consent=True):
    body = client.request("/profiles", raw=True)[2]
    token = json.loads(re.search(r'window\.SL_CSRF = ("[^"]+")', body).group(1))
    payload = {"name": name, "cls": 3, "subjects": ["toan"]}
    if consent is not None:
        payload["consent"] = consent
    request = urllib.request.Request(client.url + "/children", data=json.dumps(payload).encode(),
                                     headers={"Content-Type": "application/json", "RequestVerificationToken": token})
    try:
        with client.opener.open(request, timeout=10) as response:
            return response.status, json.loads(response.read())
    except urllib.error.HTTPError as error:
        return error.code, json.loads(error.read() or b"{}")


def post(client, page, path, pairs=()):
    status, headers, body = family.form(client, page, path, pairs)
    return status, headers, html.unescape(body) if isinstance(body, str) else body


def text(client, path):
    status, headers, body = client.request(path, raw=True)
    return status, headers, html.unescape(body)


def consent(kid):
    return sql(f"SELECT ISNULL((SELECT Decision + N'|' + ConsentType + N'|' + Version + N'|' + CASE WHEN RevokedAt IS NULL THEN N'-' ELSE N'R' END "
               f"FROM dbo.UserConsents WHERE ChildProfileId={kid}), N'none');")


def main():
    url = family.free_url()
    try:
        with tempfile.TemporaryDirectory(prefix="safelearn-private-mail-") as temporary, smoke.running_app(url, Path(temporary)):
            family.register(Path(temporary), url, PARENT, "Me Bao Ve")
            family.register(Path(temporary), url, OTHER, "Nha Khac")
            parent, other = smoke.Client(url), smoke.Client(url)
            check(parent.post_form("/login", {"Email": PARENT, "Password": PASSWORD})[0] == 302, "the parent signs in")
            check(other.post_form("/login", {"Email": OTHER, "Password": PASSWORD})[0] == 302, "another parent signs in")

            # ---------- US-10: consent when the profile is created ----------
            status, body = create_child(parent, f"Khong dong y {RUN[:4]}", consent=None)
            check(status == 409 and "đồng ý" in body.get("error", ""), "a profile cannot be created without the parent's consent")
            check(create_child(parent, f"Tu choi {RUN[:4]}", consent=False)[0] == 409, "an unticked consent box is refused")
            check(sql(f"SELECT COUNT(*) FROM dbo.ChildProfiles c JOIN dbo.Users u ON u.Id=c.ParentId WHERE u.Email=N'{PARENT}';") == "0",
                  "no profile is stored without consent")
            status, first = create_child(parent, f"Be An {RUN[:4]}")
            status2, second = create_child(parent, f"Be Binh {RUN[:4]}")
            first, second = int(first["id"]), int(second["id"])
            check(status == 200 and status2 == 200 and consent(first) == "Accepted|ChildDataPolicy|v1.0|-",
                  "with consent the profile is created and the consent is stored for that child")
            sql(f"INSERT dbo.ChildProfiles(ParentId, DisplayName, GradeLevelId, IsActive) SELECT u.Id, N'Cu {RUN[:4]}', g.Id, 1 "
                f"FROM dbo.Users u CROSS JOIN dbo.GradeLevels g WHERE u.Email=N'{PARENT}' AND g.Level=3;")
            legacy = int(sql(f"SELECT Id FROM dbo.ChildProfiles WHERE DisplayName=N'Cu {RUN[:4]}';"))

            _, _, body = text(parent, "/kids-picker")
            check(f'data-c="{first}"' in body and f'data-locked="{legacy}"' in body and "Ba mẹ cần đồng ý" in body,
                  "the picker opens consented children and locks a profile without consent")
            status, _, body = post(parent, "/kids-picker", f"/kids-picker/{legacy}")
            check(status == 400 and "cần đồng ý chính sách dữ liệu" in body, "a child without consent cannot enter child mode")
            status, headers, _ = post(parent, "/kids-picker", f"/kids-picker/{first}")
            check(status == 302 and "/kids-home" in headers.get("Location", ""), "a consented child without a PIN goes straight in")
            check(parent.request("/kids-home", raw=True)[0] == 200, "the child learns")
            check(parent.request("/privacy", raw=True)[0] == 302, "parent pages stay locked in child mode")
            check(post(parent, "/kids-exit", "/kids-exit", [("password", PASSWORD)])[0] == 302, "the parent leaves child mode")

            # Withdraw consent: the child is locked out, also on a session already open.
            post(parent, "/kids-picker", f"/kids-picker/{first}")
            sql(f"UPDATE dbo.UserConsents SET Decision=N'Revoked', RevokedAt=SYSUTCDATETIME() WHERE ChildProfileId={first};")  # withdrawn on another device
            status, headers, _ = parent.request("/kids-home", raw=True)
            check(status == 302 and "/kids-picker" in headers.get("Location", ""), "a child whose consent was withdrawn mid-session is sent back to the picker")
            status, headers, _ = parent.request("/privacy", raw=True)
            check(status == 302 and "/kids-home" in headers.get("Location", ""), "withdrawing consent does not unlock the parent pages for the child")
            sql(f"UPDATE dbo.UserConsents SET Decision=N'Accepted', RevokedAt=NULL WHERE ChildProfileId={first};")
            check(post(parent, "/kids-exit", "/kids-exit", [("password", PASSWORD)])[0] == 302, "only the parent password or PIN ends child mode")
            status, _, body = text(parent, "/privacy")
            check(f'data-consent="given"' in body and f'data-consent="missing"' in body, "the privacy page shows each child's consent")
            status, headers, _ = post(parent, "/privacy", f"/privacy/{first}/consent/revoke")
            check(status == 302 and consent(first) == "Revoked|ChildDataPolicy|v1.0|R", "the parent withdraws consent for one child")
            check(sql(f"SELECT COUNT(*) FROM dbo.AuditLogs WHERE Action=N'ChildConsentRevoked' AND EntityId=N'{first}';") == "1", "the withdrawal is logged")
            status, _, body = post(parent, "/kids-picker", f"/kids-picker/{first}")
            check(status == 400, "after withdrawal the child cannot enter")
            status, _, body = text(parent, f"/parent-home?child={first}")
            check("Chưa đồng ý xử lý dữ liệu" in body, "the parent home reminds the parent")
            check(post(other, "/privacy", f"/privacy/{first}/consent", [("agree", "true")])[0] == 404
                  and post(other, "/privacy", f"/privacy/{second}/consent/revoke")[0] == 404, "another family cannot change this family's consent")
            post(parent, "/privacy", f"/privacy/{first}/consent")
            check(consent(first).startswith("Revoked"), "agreeing again needs the box ticked")
            post(parent, "/privacy", f"/privacy/{first}/consent", [("agree", "true")])
            check(consent(first) == "Accepted|ChildDataPolicy|v1.0|-", "the parent agrees again")
            post(parent, "/privacy", f"/privacy/{legacy}/consent", [("agree", "true")])
            check(consent(legacy).startswith("Accepted"), "an older profile gets consent from the privacy page")

            # ---------- US-08: each child's PIN ----------
            post(parent, "/pin", f"/pin/child/{first}", [("pin", "12"), ("confirmPin", "12")])
            post(parent, "/pin", f"/pin/child/{first}", [("pin", "1234"), ("confirmPin", "4321")])
            check(sql(f"SELECT COUNT(*) FROM dbo.ChildProfiles WHERE Id={first} AND PinHash IS NOT NULL;") == "0", "a short or mismatched PIN is refused")
            status, headers, _ = post(parent, "/pin", f"/pin/child/{first}", [("pin", "2468"), ("confirmPin", "2468")])
            stored = sql(f"SELECT PinHash FROM dbo.ChildProfiles WHERE Id={first};")
            check(status == 302 and stored and "2468" not in stored, "the parent sets a PIN for one child, stored hashed")
            check(sql(f"SELECT COUNT(*) FROM dbo.AuditLogs WHERE Action=N'ChildPinSet' AND EntityId=N'{first}';") == "1", "setting the PIN is logged")
            check(post(other, "/pin", f"/pin/child/{first}", [("pin", "1111"), ("confirmPin", "1111")])[0] == 404, "another family cannot set this child's PIN")
            _, _, body = text(parent, "/pin")
            check("Có PIN" in body and "Chưa có PIN" in body, "the PIN page shows which children have a PIN")

            status, _, body = post(parent, "/kids-picker", f"/kids-picker/{first}")
            check(status == 200 and f'data-pin-for="{first}"' in body and parent.request("/kids-home", raw=True)[0] == 302,
                  "a child with a PIN is asked for it and is not let in yet")
            status, _, body = post(parent, "/kids-picker", f"/kids-picker/{first}", [("pin", "1111")])
            check(status == 400 and "Còn 4 lần" in body, "a wrong PIN is refused and counted")
            for _ in range(4):
                status, _, body = post(parent, "/kids-picker", f"/kids-picker/{first}", [("pin", "0000")])
            check(status == 400 and "thử lại sau 5 phút" in body
                  and sql(f"SELECT CASE WHEN PinLockedUntil > SYSUTCDATETIME() THEN 1 ELSE 0 END FROM dbo.ChildProfiles WHERE Id={first};") == "1",
                  "five wrong PINs lock that child for 5 minutes")
            status, _, _ = post(parent, "/kids-picker", f"/kids-picker/{first}", [("pin", "2468")])
            check(status == 400 and parent.request("/kids-home", raw=True)[0] == 302, "even the right PIN waits while locked")
            status, headers, _ = post(parent, "/kids-picker", f"/kids-picker/{second}")
            check(status == 302 and "/kids-home" in headers.get("Location", ""), "the lock is per child: a brother or sister without a PIN goes in")
            check(post(parent, "/kids-picker", f"/pin/child/{first}/remove")[0] == 403
                  and sql(f"SELECT COUNT(*) FROM dbo.ChildProfiles WHERE Id={first} AND PinHash IS NOT NULL;") == "1",
                  "the parent's PIN page is locked in child mode")
            sql(f"UPDATE dbo.ChildProfiles SET PinLockedUntil=NULL WHERE Id={first};")
            status, headers, _ = post(parent, "/kids-picker", f"/kids-picker/{first}", [("pin", "2468")])
            check(status == 302 and "/kids-home" in headers.get("Location", "")
                  and sql(f"SELECT PinFailedAttempts FROM dbo.ChildProfiles WHERE Id={first};") == "0", "the right PIN lets the child in and resets the count")
            check(post(parent, "/kids-exit", "/kids-exit", [("password", PASSWORD)])[0] == 302, "the parent leaves child mode again")
            post(parent, "/pin", f"/pin/child/{first}/remove")
            check(sql(f"SELECT COUNT(*) FROM dbo.ChildProfiles WHERE Id={first} AND PinHash IS NULL;") == "1", "the parent removes the PIN")
            status, headers, _ = post(parent, "/kids-picker", f"/kids-picker/{first}")
            check(status == 302, "without a PIN the child goes straight in again")
            post(parent, "/kids-exit", "/kids-exit", [("password", PASSWORD)])

            # Deleting a profile removes its consent too.
            body = parent.request("/profiles", raw=True)[2]
            token = json.loads(re.search(r'window\.SL_CSRF = ("[^"]+")', body).group(1))
            request = urllib.request.Request(url + f"/children/{second}/delete", data=b"{}", headers={"Content-Type": "application/json", "RequestVerificationToken": token})
            with parent.opener.open(request, timeout=10) as response:
                check(response.status in (200, 204), "a profile is deleted")
            check(sql(f"SELECT COUNT(*) FROM dbo.UserConsents WHERE ChildProfileId={second};") == "0", "its consent record is removed with it")
    finally:
        ids = f"SELECT Id FROM dbo.Users WHERE Email IN (N'{PARENT}', N'{OTHER}')"
        kids = f"SELECT Id FROM dbo.ChildProfiles WHERE ParentId IN ({ids})"
        for statement in (f"DELETE FROM dbo.UserConsents WHERE ChildProfileId IN ({kids});",
                          f"DELETE FROM dbo.DailyUsages WHERE ChildProfileId IN ({kids});",
                          f"DELETE FROM dbo.LearningSessions WHERE ChildProfileId IN ({kids});",
                          f"DELETE FROM dbo.ChildSubjects WHERE ChildProfileId IN ({kids});",
                          f"DELETE FROM dbo.ChildProfiles WHERE ParentId IN ({ids});",
                          f"DELETE FROM dbo.Notifications WHERE UserId IN ({ids});",
                          f"DELETE FROM dbo.UserSettings WHERE UserId IN ({ids});",
                          f"DELETE FROM dbo.AuditLogs WHERE ActorId IN ({ids});",
                          f"DELETE FROM dbo.LoginHistories WHERE UserNameTried IN (N'{PARENT}', N'{OTHER}');",
                          f"DELETE FROM dbo.Users WHERE Email IN (N'{PARENT}', N'{OTHER}');"):
            try:
                sql(statement)
            except Exception as error:
                print("cleanup:", error)
    print("smoke-child-access: all checks passed")


if __name__ == "__main__":
    main()
