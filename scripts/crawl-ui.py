"""Walk every reachable page for admin, parent and child in a real browser and report:
server errors, broken links, script errors, failed assets, phone-width overflow and buttons that do nothing.

Button probing blocks every non-GET request (nothing is written) and dismisses confirm dialogs.
Usage (after a build, from the project folder): py scripts\crawl-ui.py . crawl-report.json
Needs Playwright: py -m pip install playwright && py -m playwright install chromium
"""
import hashlib
import importlib.util
import json
from pathlib import Path
import re
import socket
import sys
import tempfile
import urllib.parse
import uuid

from playwright.sync_api import sync_playwright

root, report_path = Path(sys.argv[1]), Path(sys.argv[2])
sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location("auth_smoke", root / "scripts" / "smoke-sql-auth.py")
smoke = importlib.util.module_from_spec(spec)
spec.loader.exec_module(smoke)
sql = smoke.sql
run = uuid.uuid4().hex
parent_email, admin_email, password = f"sql-smoke-crawl-{run}@example.invalid", f"sql-smoke-crawla-{run}@example.invalid", f"CrawlTest1{run}"
TITLE = f"Bai do trang {run[:6]}"
with socket.socket() as listener:
    listener.bind(("127.0.0.1", 0))
    url = f"http://127.0.0.1:{listener.getsockname()[1]}"

SKIP = re.compile(r"^/(logout|kids-exit)\b|^/uploads/|\.(pdf|png|jpe?g|webp|gif|csv|zip)$", re.I)
DOWNLOAD = re.compile(r"/export\b")
PER_PATTERN, MAX_PAGES, MAX_PROBES = 2, 160, 30
ATTEMPTS = []
findings = {"server_errors": [], "broken_links": [], "script_errors": [], "failed_assets": [], "overflow": [], "dead_buttons": [], "pages": {}}


def pattern(path):
    return re.sub(r"/\d+", "/N", path.split("?")[0]) + ("?" + "&".join(sorted(k for k, _ in urllib.parse.parse_qsl(path.split("?", 1)[1]))) if "?" in path else "")


def local(href, base):
    full = urllib.parse.urljoin(base, href)
    parts = urllib.parse.urlsplit(full)
    if parts.netloc != urllib.parse.urlsplit(url).netloc or parts.scheme not in ("http", "https"):
        return None
    return parts.path + (("?" + parts.query) if parts.query else "")


PROBE = """() => {
  const seen = new Set(), out = [];
  const cands = document.querySelectorAll('button, a[href="#"], a[href=""], a:not([href]), [role=button], summary');
  cands.forEach((el, i) => {
    if (el.closest('[hidden]') || el.disabled) return;
    const r = el.getBoundingClientRect(); if (!r.width || !r.height) return;
    if (el.tagName === 'BUTTON') {
      const type = (el.getAttribute('type') || (el.form ? 'submit' : 'button')).toLowerCase();
      if (type !== 'button') return;          // real form submits are wired by their form
    }
    if (el.tagName === 'SUMMARY') return;     // native <details> toggles
    const label = (el.innerText || el.getAttribute('aria-label') || el.title || '').replace(/\\s+/g, ' ').trim().slice(0, 60);
    const key = el.tagName + '|' + label; if (seen.has(key)) return; seen.add(key);
    el.setAttribute('data-crawl', String(i)); out.push({id: String(i), label, tag: el.tagName});
  });
  return out;
}"""
STATE = """() => [location.href, document.body.innerHTML.length, document.body.innerText.length,
  [...document.querySelectorAll('[aria-expanded],[aria-pressed],[aria-selected],[open],dialog,.sl-modal,.pop,.toast,[class*=toast]')].map(e =>
    (e.getAttribute('aria-expanded')||'')+(e.getAttribute('aria-pressed')||'')+(e.getAttribute('aria-selected')||'')+(e.hasAttribute('open')?'o':'')+e.className+(getComputedStyle(e).display)).join('|'),
  document.activeElement && document.activeElement.tagName, document.documentElement.className, document.body.className,
  [...document.querySelectorAll('input,textarea,select')].map(e => e.type === 'checkbox' || e.type === 'radio' ? e.checked : e.value).join('|'),
  document.fullscreenElement ? 1 : 0]"""


def sql_status(target):
    return sql(f"SELECT Status FROM dbo.Lessons WHERE Id={int(target.rsplit('/', 1)[1])};")


def crawl(context, role, start):
    page = context.new_page()
    page.set_default_timeout(15000)
    errors, failed = [], []
    page.on("dialog", lambda d: (ATTEMPTS.append("dialog"), d.dismiss()))
    page.on("pageerror", lambda e: errors.append(str(e)[:200]))
    page.on("console", lambda m: m.type == "error" and errors.append("console: " + m.text[:200]))
    page.on("response", lambda r: r.status >= 400 and urllib.parse.urlsplit(r.url).netloc == urllib.parse.urlsplit(url).netloc
            and r.request.resource_type in ("script", "stylesheet", "image", "font", "fetch", "xhr") and failed.append(f"{r.status} {r.url}"))
    queue, done, counts, links_checked = list(start), [], {}, {}
    while queue and len(done) < MAX_PAGES:
        path = queue.pop(0)
        key = pattern(path)
        if path in done or counts.get(key, 0) >= PER_PATTERN or SKIP.search(path):
            continue
        counts[key] = counts.get(key, 0) + 1
        done.append(path)
        if DOWNLOAD.search(path):
            response = context.request.get(url + path)
            if response.status >= 400:
                findings["server_errors" if response.status >= 500 else "broken_links"].append(f"[{role}] {path} -> {response.status}")
            continue
        errors.clear(); failed.clear()
        try:
            response = page.goto(url + path, wait_until="load")
        except Exception as error:
            findings["server_errors"].append(f"[{role}] {path} -> {error}")
            continue
        status = response.status if response else 0
        landed = local(page.url, url)
        findings["pages"].setdefault(role, []).append(f"{status} {path}" + (f" -> {landed}" if landed != path else ""))
        if status >= 500 or "error/500" in page.url:
            findings["server_errors"].append(f"[{role}] {path} -> {status} {page.url}")
            continue
        if status == 404 or "error/404" in page.url:
            findings["broken_links"].append(f"[{role}] {path} -> 404")
            continue
        for error in errors:
            findings["script_errors"].append(f"[{role}] {path}: {error}")
        for item in failed:
            findings["failed_assets"].append(f"[{role}] {path}: {item}")
        # links found on the page: follow them and check they answer
        hrefs = page.eval_on_selector_all("a[href]", "els => els.map(e => e.getAttribute('href'))")
        for href in hrefs:
            target = local(href, page.url)
            if not target or target.startswith("/#") or SKIP.search(target):
                continue
            if target not in links_checked:
                links_checked[target] = path + " (status " + str(sql_status(target)) + ")" if target.startswith("/lesson-detail/") else path
            if target not in done and target not in queue:
                queue.append(target)
        # phone width
        page.set_viewport_size({"width": 390, "height": 800})
        if page.evaluate("document.documentElement.scrollWidth > 392"):
            findings["overflow"].append(f"[{role}] {pattern(path)} ({page.evaluate('document.documentElement.scrollWidth')}px)")
        page.set_viewport_size({"width": 1280, "height": 860})
        if counts[key] == 1:
            probe(page, role, path)
    # every link seen must answer (pages beyond the crawl budget included)
    for target, origin in links_checked.items():
        if target in done:
            continue
        response = context.request.get(url + target, max_redirects=5)
        if response.status >= 400:
            findings["server_errors" if response.status >= 500 else "broken_links"].append(f"[{role}] link {target} on {origin} -> {response.status}")
    page.close()
    return done


def probe(page, role, path):
    """Click every non-submit button once; a button that changes nothing is reported."""
    attempts = ATTEMPTS

    def block(route):
        if route.request.method != "GET":
            attempts.append(route.request.method + " " + route.request.url)
            route.abort()
        else:
            route.continue_()

    page.route("**/*", block)
    try:
        candidates = page.evaluate(PROBE)[:MAX_PROBES]
        for item in candidates:
            if page.url.split("#")[0] != (url + path).split("#")[0]:
                page.goto(url + path, wait_until="load")
                page.evaluate(PROBE)
            target = page.locator(f'[data-crawl="{item["id"]}"]')
            if not target.count() or not target.first.is_visible():
                continue
            attempts.clear()
            before = page.evaluate(STATE)
            pages_before = len(page.context.pages)
            try:
                with page.expect_download(timeout=600) as _:
                    target.first.click(timeout=2000, no_wait_after=True)
                attempts.append("download")
            except Exception:
                pass
            page.wait_for_timeout(350)
            after = page.evaluate(STATE)
            if len(page.context.pages) > pages_before:
                for extra in page.context.pages[pages_before:]:
                    extra.close()
                continue
            if before == after and not attempts:
                findings["dead_buttons"].append(f"[{role}] {pattern(path)}: <{item['tag'].lower()}> \"{item['label']}\"")
            if before != after:
                page.keyboard.press("Escape")
                if page.evaluate("document.fullscreenElement !== null"):
                    page.evaluate("document.exitFullscreen()")
    finally:
        page.unroute("**/*")


def login(page, who):
    page.goto(url + "/login")
    page.fill("input[name=Email]", who)
    page.fill("input[name=Password]", password)
    page.click("form[action$='/login'] button[type=submit]")


try:
    with tempfile.TemporaryDirectory(prefix="safelearn-private-mail-") as temporary, smoke.running_app(url, Path(temporary)):
        for who in (parent_email, admin_email):
            client = smoke.Client(url)
            assert client.post_form("/register", {"FullName": "Me Do Trang", "Email": who, "Password": password, "ConfirmPassword": password, "Agree": "true"})[0] == 302
            smoke.confirm(client, smoke.mail_link(Path(temporary), who, "/confirm-email"))
        admin_id = sql(f"SELECT Id FROM dbo.Users WHERE Email=N'{admin_email}';")
        sql(f"DELETE FROM dbo.UserRoles WHERE UserId=N'{admin_id}';")
        sql(f"INSERT dbo.UserRoles(UserId, RoleId) SELECT N'{admin_id}', Id FROM dbo.Roles WHERE NormalizedName=N'ADMIN';")
        with sync_playwright() as playwright:
            browser = playwright.chromium.launch()
            # Anonymous visitor
            anonymous = browser.new_context(viewport={"width": 1280, "height": 860})
            crawl(anonymous, "guest", ["/", "/login", "/register", "/recover", "/terms", "/privacy", "/child-data-policy", "/check-email"])
            anonymous.close()
            # Parent: onboarding, one family lesson, then everything reachable
            parent = browser.new_context(viewport={"width": 1280, "height": 860})
            page = parent.new_page()
            login(page, parent_email)
            page.wait_for_url("**/onboarding*")
            page.fill("#ob-n", "Be Do Trang")
            page.check("#ob-consent")
            page.click("#ob-next")
            page.wait_for_url("**/parent-home*")
            page.goto(url + "/family-lessons/new")
            page.fill("#fl-title", TITLE)
            subject = page.evaluate("[...document.querySelectorAll('#fl-subject option')].find(o => /lớp .*3/.test(o.textContent)).value")
            page.select_option("#fl-subject", subject)
            page.select_option("#fl-grade", "3")
            page.click("button:has-text('Tạo bài')")
            page.wait_for_url("**/family-lessons/*")
            page.close()
            crawl(parent, "parent", ["/parent-home", "/profiles", "/pin", "/library", "/family-lessons", "/lesson-requests", "/sources", "/progress",
                                     "/time", "/history", "/weekly", "/notifications", "/settings", "/change-password", "/onboarding"])
            # Child mode on the same sign-in
            page = parent.new_page()
            page.goto(url + "/kids-picker")
            page.locator("[data-c]").first.click()
            page.wait_for_url("**/kids-home*")
            page.close()
            crawl(parent, "kid", ["/kids-home", "/kids-search", "/kids-badges", "/kids-review", "/kids-reading", "/kids-time-up", "/parent-home"])
            parent.close()
            # Admin
            admin = browser.new_context(viewport={"width": 1280, "height": 860})
            page = admin.new_page()
            login(page, admin_email)
            page.wait_for_url("**/admin-home*")
            page.close()
            crawl(admin, "admin", ["/admin-home", "/admin-users", "/admin-catalog", "/admin-lesson", "/admin-curate", "/admin-videos", "/admin-questions",
                                   "/admin-paths", "/admin-badges", "/admin-reports", "/admin-community", "/admin-lesson-requests", "/admin-stats",
                                   "/admin-audit", "/admin-data-requests"])
            admin.close()
            browser.close()
finally:
    ids = f"SELECT Id FROM dbo.Users WHERE Email IN (N'{parent_email}', N'{admin_email}')"
    lessons = f"SELECT Id FROM dbo.Lessons WHERE OwnerParentId IN ({ids})"
    kids = f"SELECT Id FROM dbo.ChildProfiles WHERE ParentId IN ({ids})"
    # Child mode visits create activity, attempt and session rows: remove every row that points at the test children.
    tables = sql("SELECT STRING_AGG(CAST(TABLE_NAME AS nvarchar(max)), ' ') FROM INFORMATION_SCHEMA.COLUMNS WHERE COLUMN_NAME=N'ChildProfileId' AND TABLE_SCHEMA=N'dbo';").split()
    if "QuizAttempts" in tables:
        try:
            sql(f"DELETE FROM dbo.QuizAttemptAnswers WHERE AttemptId IN (SELECT Id FROM dbo.QuizAttempts WHERE ChildProfileId IN ({kids}));")
        except Exception as error:
            print("cleanup:", error)
    for _ in range(3):
        for table in [t for t in tables if t != "ChildProfiles"]:
            try:
                sql(f"DELETE FROM dbo.[{table}] WHERE ChildProfileId IN ({kids});")
            except Exception:
                pass
    for statement in (f"DELETE FROM dbo.LessonContents WHERE LessonId IN ({lessons});", f"DELETE FROM dbo.LessonActivities WHERE LessonId IN ({lessons});",
                      f"DELETE FROM dbo.Lessons WHERE Id IN ({lessons});", f"DELETE FROM dbo.DailyUsages WHERE ChildProfileId IN ({kids});",
                      f"DELETE FROM dbo.LearningSessions WHERE ChildProfileId IN ({kids});",
                      f"DELETE FROM dbo.Notifications WHERE UserId IN ({ids});", f"DELETE FROM dbo.ChildProfiles WHERE ParentId IN ({ids});",
                      f"DELETE FROM dbo.UserSettings WHERE UserId IN ({ids});", f"DELETE FROM dbo.AuditLogs WHERE ActorId IN ({ids});",
                      f"DELETE FROM dbo.LoginHistories WHERE UserNameTried IN (N'{parent_email}', N'{admin_email}');",
                      f"DELETE FROM dbo.UserRoles WHERE UserId IN ({ids});",
                      f"DELETE FROM dbo.Users WHERE Email IN (N'{parent_email}', N'{admin_email}');"):
        try:
            sql(statement)
        except Exception as error:
            print("cleanup:", error)
    report_path.write_text(json.dumps(findings, ensure_ascii=False, indent=1), encoding="utf-8")
    for key, items in findings.items():
        if key != "pages":
            print(f"{key}: {len(items)}")
            for item in items[:60]:
                print("   ", item)
    print("pages visited:", {role: len(items) for role, items in findings["pages"].items()})
