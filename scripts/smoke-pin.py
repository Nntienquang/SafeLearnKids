"""Every PIN case on real SQL: the parent PIN that ends child mode (US-50), each child's own PIN (US-08),
and the reminders shown while a PIN is missing (picker notice, exit label, parent home).

Two devices share one sign-in, as in a family: the parent's phone and the child's tablet (child mode lives in a cookie on
the tablet only). Uses disposable UUID accounts and private email pickup. Build first. Removes everything it creates.
"""

import importlib.util
from pathlib import Path
import sys
import tempfile
import uuid


sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location("child_access", Path(__file__).with_name("smoke-child-access.py"))
access = importlib.util.module_from_spec(spec)
spec.loader.exec_module(access)
family, smoke, check, sql = access.family, access.smoke, access.check, access.sql
create_child, post, text = access.create_child, access.post, access.text

RUN = uuid.uuid4().hex
PARENT = f"sql-smoke-pin-{RUN}@example.invalid"
SOLO = f"sql-smoke-pin-solo-{RUN}@example.invalid"
PASSWORD = family.PASSWORD
NOTICE, NO_PARENT, NO_CHILD = "data-pin-notice", "data-no-parent-pin", "data-no-child-pin"


def location(client, path):
    status, headers, _ = client.request(path)
    return status, headers.get("Location", "")


def exit_child(tablet, pin=None, password=None):
    return post(tablet, "/kids-exit", "/kids-exit", [("pin", pin)] if pin is not None else [("password", password)])


def choose(tablet, kid, pin=None):
    return post(tablet, "/kids-picker", f"/kids-picker/{kid}", [("pin", pin)] if pin is not None else [])


def set_parent_pin(phone, pin, confirm=None, password=PASSWORD):
    return post(phone, "/pin", "/pin", [("Pin", pin), ("ConfirmPin", confirm or pin), ("Password", password)])


def set_child_pin(phone, kid, pin, confirm=None):
    return post(phone, "/pin", f"/pin/child/{kid}", [("pin", pin), ("confirmPin", confirm or pin)])


def child_row(kid):
    return sql(f"SELECT ISNULL(LEFT(PinHash, 6), N'-') + N'|' + CAST(PinFailedAttempts AS nvarchar) + N'|' "
               f"+ CASE WHEN PinLockedUntil > SYSUTCDATETIME() THEN N'locked' ELSE N'open' END FROM dbo.ChildProfiles WHERE Id={kid};")


def main():
    url = family.free_url()
    try:
        with tempfile.TemporaryDirectory(prefix="safelearn-private-mail-") as temporary:
          with smoke.running_app(url, Path(temporary)):
              family.register(Path(temporary), url, PARENT, "Me Hai Be")
              family.register(Path(temporary), url, SOLO, "Me Mot Be")
              phone, solo = smoke.Client(url), smoke.Client(url)
              check(phone.post_form("/login", {"Email": PARENT, "Password": PASSWORD})[0] == 302, "the parent signs in")
              check(solo.post_form("/login", {"Email": SOLO, "Password": PASSWORD})[0] == 302, "a one-child parent signs in")
              _, an = create_child(phone, f"An {RUN[:4]}")
              _, binh = create_child(phone, f"Binh {RUN[:4]}")
              an, binh = int(an["id"]), int(binh["id"])
              an_name, binh_name = f"An {RUN[:4]}", f"Binh {RUN[:4]}"
              _, only = create_child(solo, f"Mot {RUN[:4]}")
              only = int(only["id"])
              tablet = smoke.clone(phone)

              # ---------- A new family: no PIN at all ----------
              check(child_row(an).startswith("-|0|open") and child_row(binh).startswith("-|0|open"), "a new child has no PIN")
              check(sql(f"SELECT COUNT(*) FROM dbo.UserSettings s JOIN dbo.Users u ON u.Id=s.UserId WHERE u.Email=N'{PARENT}' AND s.ParentPinHash IS NOT NULL;") == "0",
                    "a new account has no parent PIN")
              _, _, home = text(phone, f"/parent-home?child={an}")
              check("Chưa đặt mã PIN chế độ trẻ" in home and 'href="/pin"' in home, "the parent home reminds the parent to set a PIN")
              check(f"{an_name} chưa có PIN riêng" in home, "with two children the home also mentions the child's own PIN (lowest reminder)")
              _, _, picker = text(tablet, "/kids-picker")
              check(NOTICE in picker and NO_PARENT in picker and 'href="/pin"' in picker,
                    "opening child mode without a parent PIN shows a notice for the parent with a link to set one")
              check(NO_CHILD in picker and an_name in picker.split(NO_CHILD, 1)[1].split("</div>", 1)[0]
                    and binh_name in picker.split(NO_CHILD, 1)[1].split("</div>", 1)[0], "the notice names the children without their own PIN")
              check("Dành cho ba mẹ (cần mật khẩu)" in picker, "the exit button says a password is needed, not a PIN")
              _, _, solo_home = text(solo, "/parent-home")
              _, _, solo_picker = text(smoke.clone(solo), "/kids-picker")
              check("Chưa đặt mã PIN chế độ trẻ" in solo_home and "chưa có PIN riêng" not in solo_home,
                    "with one child only the parent PIN is mentioned on the home page")
              check(NO_PARENT in solo_picker and NO_CHILD not in solo_picker, "with one child the picker notice skips the child PIN")

              # Child mode without any PIN
              status, where = choose(tablet, an)[:2]
              check(status == 302 and where.get("Location", "").endswith("/kids-home"), "without a PIN of its own the child goes straight in")
              check(location(tablet, "/parent-home")[1].endswith("/kids-home"), "child mode still locks the parent pages without a parent PIN")
              check(location(tablet, "/pin")[1].endswith("/kids-home"), "the child cannot open the PIN page")
              check(post(tablet, "/kids-home", "/pin", [("Pin", "1111"), ("ConfirmPin", "1111"), ("Password", PASSWORD)])[0] == 403,
                    "the child cannot set the parent PIN from the tablet")
              _, _, picker = text(tablet, "/kids-picker")
              check(NOTICE not in picker, "while a child is learning, switching profiles shows no parent notice")
              _, _, page = text(tablet, "/kids-exit")
              check("Ba mẹ nhập mật khẩu" in page and "Ba mẹ chưa đặt PIN" in page and 'id="pin-pad"' not in page,
                    "the exit screen explains that no PIN is set and asks for the account password")
              status, _, page = exit_child(tablet, pin="1234")
              check(status == 400 and "Mật khẩu chưa đúng" in page, "a PIN typed while none is set is refused")
              status, _, page = exit_child(tablet, password="SaiMatKhau1")
              check(status == 400 and "Mật khẩu chưa đúng. Còn 3 lần thử." in page, "a wrong password is refused and counted")
              check(exit_child(tablet, password=PASSWORD)[0] == 302 and tablet.request("/parent-home")[0] == 200,
                    "the right password ends child mode")

              # ---------- Parent PIN ----------
              check(set_parent_pin(phone, "2468", password="SaiMatKhau1")[0] == 400, "setting the PIN needs the account password")
              check(set_parent_pin(phone, "2468", "1357")[0] == 400, "the two PINs must match")
              check(set_parent_pin(phone, "24a8")[0] == 400 and set_parent_pin(phone, "246")[0] == 400 and set_parent_pin(phone, "24680")[0] == 400,
                    "a PIN is exactly four digits")
              check(set_parent_pin(phone, "2468")[0] == 302, "the parent sets a PIN")
              _, _, home = text(phone, f"/parent-home?child={an}")
              check("Chưa đặt mã PIN chế độ trẻ" not in home, "the home reminder disappears once the PIN is set")
              _, _, picker = text(tablet, "/kids-picker")
              check(NO_PARENT not in picker and NO_CHILD in picker and "Dành cho ba mẹ (cần PIN)" in picker,
                    "the picker drops the parent part of the notice and the exit button asks for the PIN")
              choose(tablet, an)
              _, _, page = text(tablet, "/kids-exit")
              check("Nhập PIN của ba mẹ" in page and 'id="pin-pad"' in page and "Quên PIN?" in page, "with a PIN the exit screen shows the keypad and a password fallback")
              status, _, page = exit_child(tablet, pin="1111")
              check(status == 400 and "Sai PIN. Còn 4 lần thử." in page and location(tablet, "/parent-home")[1].endswith("/kids-home"),
                    "a wrong PIN keeps child mode and counts down")
              check(exit_child(tablet, pin="2468")[0] == 302, "the right PIN ends child mode")
              check(set_parent_pin(phone, "1357")[0] == 302, "the parent changes the PIN")
              choose(tablet, an)
              check(exit_child(tablet, pin="2468")[0] == 400, "the old PIN no longer works")
              check(exit_child(tablet, pin="1357")[0] == 302, "the new PIN works")
              choose(tablet, an)
              check(exit_child(tablet, password=PASSWORD)[0] == 302, "a parent who forgot the PIN can leave with the password")

              # ---------- Each child's own PIN ----------
              check(set_child_pin(phone, binh, "12a4")[0] == 302 and child_row(binh).startswith("-|"), "a child PIN must be four digits")
              check(set_child_pin(phone, binh, "1234", "4321")[0] == 302 and child_row(binh).startswith("-|"), "the two child PINs must match")
              check(set_child_pin(phone, binh, "1234")[0] == 302 and child_row(binh).startswith("AQAAAA|0|open"), "the parent sets Binh's PIN, stored hashed")
              check(post(solo, "/pin", f"/pin/child/{binh}", [("pin", "9999"), ("confirmPin", "9999")])[0] == 404, "another family cannot set this child's PIN")
              _, _, page = text(phone, "/pin")
              check("Có PIN · đặt lúc" in page and "Chưa có PIN: bé vào học không cần PIN" in page, "the PIN page shows which child has a PIN")
              _, _, picker = text(tablet, "/kids-picker")
              check("· có mã PIN" in picker and binh_name not in picker.split(NO_CHILD, 1)[1].split("</div>", 1)[0],
                    "the picker marks Binh as having a PIN and only An is still listed without one")
              status, _, page = choose(tablet, binh)
              check(status == 200 and f'data-pin-for="{binh}"' in page and "nhập mã PIN của con" in page, "choosing Binh asks for Binh's PIN")
              status, _, page = choose(tablet, binh, "0000")
              check(status == 400 and "Sai mã PIN. Còn 4 lần thử." in page, "a wrong child PIN is refused and counted")
              check(choose(tablet, binh, "1234")[0] == 302 and tablet.request("/kids-home")[0] == 200, "the right child PIN lets Binh in")
              status, _, page = choose(tablet, an)
              check(status == 302, "switching to a child without a PIN, still in child mode, goes straight in")
              status, _, page = choose(tablet, binh)
              check(status == 200 and f'data-pin-for="{binh}"' in page, "switching back to Binh inside child mode asks for the PIN again")
              for _ in range(5):
                  status, _, page = choose(tablet, binh, "0000")
              check(status == 400 and "thử lại sau 5 phút" in page and child_row(binh).endswith("locked"), "five wrong child PINs lock Binh for 5 minutes")
              check(choose(tablet, binh, "1234")[0] == 400, "even the right PIN waits while Binh is locked")
              check(choose(tablet, an)[0] == 302, "the lock is per child: An still goes in")
              check(exit_child(tablet, pin="1357")[0] == 302, "the parent leaves child mode")
              _, _, page = text(phone, "/pin")
              check("đang khóa" in page, "the PIN page shows that Binh is locked")
              check(set_child_pin(phone, binh, "5678")[0] == 302 and child_row(binh) == "AQAAAA|0|open", "setting a new child PIN lifts the lock")
              check(choose(tablet, binh, "1234")[0] == 400, "Binh's old PIN no longer works")
              check(choose(tablet, binh, "5678")[0] == 302, "Binh's new PIN works")
              check(post(tablet, "/kids-picker", f"/pin/child/{binh}/remove")[0] == 403, "the child cannot remove a PIN from the tablet")
              check(exit_child(tablet, pin="1357")[0] == 302, "the parent leaves child mode again")
              check(post(phone, "/pin", f"/pin/child/{binh}/remove")[0] == 302 and child_row(binh).startswith("-|"), "the parent removes Binh's PIN")
              check(choose(tablet, binh)[0] == 302, "without a PIN Binh goes straight in again")
              check(exit_child(tablet, pin="1357")[0] == 302, "the parent leaves child mode")

              # ---------- Parent PIN lockout (on the one-child family, so the lock does not slow the rest) ----------
              check(set_parent_pin(solo, "1122")[0] == 302, "the other parent sets a PIN")
              pad = smoke.clone(solo)
              choose(pad, only)
              for _ in range(5):
                  status, _, page = exit_child(pad, pin="9999")
              check(status == 400 and "Thử lại sau 5 phút" in page, "five wrong parent PINs lock the exit for 5 minutes")
              check(exit_child(pad, pin="1122")[0] == 400 and exit_child(pad, password=PASSWORD)[0] == 400,
                    "while locked neither the right PIN nor the password ends child mode")
              check(location(pad, "/parent-home")[1].endswith("/kids-home"), "the child stays in child mode while the exit is locked")
              check(solo.request("/parent-home")[0] == 200, "the lock on the tablet does not sign the parent's phone out")
          # The exit lock is kept in SQL: a restart (or a second server) does not open it.
          check(sql(f"SELECT COUNT(*) FROM dbo.UserSettings s JOIN dbo.Users u ON u.Id=s.UserId WHERE u.Email=N'{SOLO}' AND s.PinLockedUntil > SYSUTCDATETIME();") == "1",
                "the exit lock is stored in SQL")
          with smoke.running_app(url, Path(temporary)):
              status, _, page = exit_child(pad, pin="1122")
              check(status == 400 and "Thử lại sau" in page, "after a restart the exit is still locked")
              sql(f"UPDATE s SET PinLockedUntil=DATEADD(second, -1, SYSUTCDATETIME()) FROM dbo.UserSettings s JOIN dbo.Users u ON u.Id=s.UserId WHERE u.Email=N'{SOLO}';")
              check(exit_child(pad, pin="9999")[2].count("Còn 4 lần thử") == 1, "when the lock ends the count starts again from five")
              check(exit_child(pad, pin="1122")[0] == 302 and sql(f"SELECT CAST(s.PinFailedAttempts AS varchar) FROM dbo.UserSettings s JOIN dbo.Users u ON u.Id=s.UserId WHERE u.Email=N'{SOLO}';") == "0",
                    "the right PIN ends child mode and clears the count")
    finally:
        ids = f"SELECT Id FROM dbo.Users WHERE Email IN (N'{PARENT}', N'{SOLO}')"
        kids = f"SELECT Id FROM dbo.ChildProfiles WHERE ParentId IN ({ids})"
        for statement in (f"DELETE FROM dbo.UserConsents WHERE ChildProfileId IN ({kids});",
                          f"DELETE FROM dbo.DailyUsages WHERE ChildProfileId IN ({kids});",
                          f"DELETE FROM dbo.LearningSessions WHERE ChildProfileId IN ({kids});",
                          f"DELETE FROM dbo.ChildSubjects WHERE ChildProfileId IN ({kids});",
                          f"DELETE FROM dbo.ChildProfiles WHERE ParentId IN ({ids});",
                          f"DELETE FROM dbo.Notifications WHERE UserId IN ({ids});",
                          f"DELETE FROM dbo.UserConsents WHERE UserId IN ({ids});",
                          f"DELETE FROM dbo.UserSettings WHERE UserId IN ({ids});",
                          f"DELETE FROM dbo.AuditLogs WHERE ActorId IN ({ids});",
                          f"DELETE FROM dbo.LoginHistories WHERE UserNameTried IN (N'{PARENT}', N'{SOLO}');",
                          f"DELETE FROM dbo.UserRoles WHERE UserId IN ({ids});",
                          f"DELETE FROM dbo.Users WHERE Email IN (N'{PARENT}', N'{SOLO}');"):
            try:
                sql(statement)
            except Exception as error:
                print("cleanup:", error)
    print(f"RESULT {smoke.checks}/{smoke.checks} passed (parent PIN + child PIN + reminders)")


if __name__ == "__main__":
    main()
