"""Exercise Sprint 3 content sources (US-17 to US-22) against the built root app and real SQL.

A local fake YouTube serves both the public feed and the Data API, so no internet or real key is used.
Build first. Removes only the sources, videos, lessons, keywords and accounts it creates.
"""

from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import importlib.util
import json
from pathlib import Path
import socket
import sys
import tempfile
import threading
import urllib.parse
import uuid


sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location("auth_smoke", Path(__file__).with_name("smoke-sql-auth.py"))
smoke = importlib.util.module_from_spec(spec)
spec.loader.exec_module(smoke)
check, sql = smoke.check, smoke.sql
# sqlcmd prints in the console code page, so Vietnamese text is compared inside SQL, not read back.

RUN = uuid.uuid4().hex
ADMIN = f"sql-smoke-src-admin-{RUN}@example.invalid"
PARENT = f"sql-smoke-src-parent-{RUN}@example.invalid"
OTHER = f"sql-smoke-src-other-{RUN}@example.invalid"
PASSWORD = f"SourceTest1{RUN}"
KEY = f"TESTKEY{RUN}"
KEYWORD = f"tukhoa{RUN[:8]}"
PLAYLIST = f"PLsmoke{RUN[:20]}"
MISSING = f"PLmissing{RUN[:18]}"
QUOTA = f"PLquota{RUN[:20]}"
CHANNEL = "UC" + RUN[:22]
UPLOADS = "UU" + RUN[:22]
VIDEOS = [f"v{RUN[i:i + 10]}" for i in (0, 10, 20)]
API_VIDEOS = [f"a{RUN[i:i + 10]}" for i in (0, 7, 14)]
TAG = f"src-{RUN[:12]}"
created_urls = []


def free_port():
    with socket.socket() as listener:
        listener.bind(("127.0.0.1", 0))
        return listener.getsockname()[1]


FEED = """<?xml version="1.0" encoding="UTF-8"?>
<feed xmlns:yt="http://www.youtube.com/xml/schemas/2015" xmlns:media="http://search.yahoo.com/mrss/" xmlns="http://www.w3.org/2005/Atom">
{entries}
</feed>"""
ENTRY = """<entry><yt:videoId>{id}</yt:videoId><title>{title}</title><published>2026-10-0{day}T08:00:00+00:00</published>
<media:group><media:thumbnail url="https://evil.example/x.jpg"/></media:group></entry>"""


class FakeYouTube(BaseHTTPRequestHandler):
    def log_message(self, *args):
        pass

    def reply(self, status, body, kind):
        data = body.encode()
        self.send_response(status)
        self.send_header("Content-Type", kind)
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        self.wfile.write(data)

    def do_GET(self):
        url = urllib.parse.urlsplit(self.path)
        query = dict(urllib.parse.parse_qsl(url.query))
        if url.path == "/feeds/videos.xml":
            if query.get("playlist_id") != PLAYLIST:
                return self.reply(404, "", "text/plain")
            titles = ["Học đếm số cho bé", f"Video có {KEYWORD} lạ", "Bài hát thiếu nhi"]
            entries = "\n".join(ENTRY.format(id=id, title=title, day=i + 1) for i, (id, title) in enumerate(zip(VIDEOS, titles)))
            return self.reply(200, FEED.format(entries=entries), "application/atom+xml")
        if url.path.startswith("/v3/"):
            if query.get("key") != KEY:
                return self.reply(400, json.dumps({"error": "bad key"}), "application/json")
            if url.path == "/v3/channels" and query.get("id") == CHANNEL:
                return self.reply(200, json.dumps({"items": [{"contentDetails": {"relatedPlaylists": {"uploads": UPLOADS}}}]}), "application/json")
            if url.path == "/v3/playlistItems":
                if query.get("playlistId") == QUOTA:
                    return self.reply(403, json.dumps({"error": "quota"}), "application/json")
                if query.get("playlistId") != UPLOADS:
                    return self.reply(404, "{}", "application/json")
                first = "pageToken" not in query
                ids = API_VIDEOS[:2] if first else API_VIDEOS[2:]
                body = {"items": [{"snippet": {"title": f"Toán lớp 1 phần {id}"}, "contentDetails": {"videoId": id, "videoPublishedAt": "2026-10-05T08:00:00Z"}} for id in ids]}
                if first:
                    body["nextPageToken"] = "page2"
                return self.reply(200, json.dumps(body), "application/json")
            if url.path == "/v3/videos":
                items = [{"id": id, "contentDetails": {"duration": "PT4M5S"}, "status": {"embeddable": id != API_VIDEOS[2]}} for id in query.get("id", "").split(",")]
                return self.reply(200, json.dumps({"items": items}), "application/json")
        self.reply(404, "", "text/plain")


def register(directory, url, email, name):
    client = smoke.Client(url)
    status, _, _ = client.post_form("/register", {"FullName": name, "Email": email, "Password": PASSWORD, "ConfirmPassword": PASSWORD, "Agree": "true"})
    check(status == 302 and smoke.confirm(client, smoke.mail_link(directory, email, "/confirm-email"))[0] == 302, f"registers {name}")


def form(client, page, path, pairs=()):
    token = smoke.token_from(client.request(page)[2])
    return client.request(path, list(pairs) + [("__RequestVerificationToken", token)])


def source_id(external):
    return sql(f"SELECT Id FROM dbo.ContentSources WHERE ExternalId=N'{external}';")


def add_source(admin, url, name):
    created_urls.append(url)
    return form(admin, "/admin-sources/new", "/admin-sources/new", [("Url", url), ("Name", name), ("Description", "Nguồn thử cho bé học")])


def video_status(video):
    return sql(f"SELECT Status FROM dbo.SourceVideos WHERE VideoId=N'{video}';")


def feed_checks(url, directory):
    with smoke.running_app(url, directory, {"YouTube__FeedBaseUrl": FAKE + "/feeds/videos.xml", "YouTube__ApiKey": ""}) as anonymous:
        for email, name in ((ADMIN, "Source admin"), (PARENT, "Source parent"), (OTHER, "Source other")):
            register(directory, url, email, name)
        admin_id = sql(f"SELECT Id FROM dbo.Users WHERE Email=N'{ADMIN}';")
        sql(f"DELETE FROM dbo.UserRoles WHERE UserId=N'{admin_id}';")
        sql(f"INSERT dbo.UserRoles(UserId, RoleId) SELECT N'{admin_id}', Id FROM dbo.Roles WHERE NormalizedName=N'ADMIN';")
        sql(f"INSERT dbo.BlockedKeywords(Keyword, CreatedById, CreatedAt) VALUES (N'{KEYWORD}', N'{admin_id}', SYSUTCDATETIME());")
        admin, parent, other = smoke.Client(url), smoke.Client(url), smoke.Client(url)
        for client, email in ((admin, ADMIN), (parent, PARENT), (other, OTHER)):
            check(client.post_form("/login", {"Email": email, "Password": PASSWORD})[0] == 302, f"{email.split('-')[3]} signs in")

        check(anonymous.request("/admin-curate")[0] == 302 and "/error/403" in parent.request("/admin-videos")[1].get("Location", ""), "only admins open source management")
        check(add_source(admin, "javascript:alert(1)", "Xấu")[0] == 200, "non-web links are refused")
        status, _, body = add_source(admin, "https://www.youtube.com/@kenhthu", "Kênh tên")
        check(status == 200 and "Chia sẻ" in body, "channel handles get a hint to use the channel id")
        check(add_source(admin, f"https://www.youtube.com/watch?v=abc&list={PLAYLIST}", "Danh sách thử")[0] == 302
              and sql(f"SELECT Platform + N'|' + Status FROM dbo.ContentSources WHERE ExternalId=N'{PLAYLIST}';") == "YouTubePlaylist|Suggested", "Admin adds a playlist as a pending source")
        playlist = source_id(PLAYLIST)
        check(add_source(admin, f"https://youtube.com/playlist?list={PLAYLIST}", "Trùng")[0] == 200
              and sql(f"SELECT COUNT(*) FROM dbo.ContentSources WHERE ExternalId=N'{PLAYLIST}';") == "1", "the same source cannot be added twice")
        site_url = f"https://example.org/hoc-{RUN[:8]}"
        add_source(admin, site_url, "Website thử")
        website = sql(f"SELECT Id FROM dbo.ContentSources WHERE Url=N'{site_url}';")
        check(website != "" and sql(f"SELECT Platform FROM dbo.ContentSources WHERE Id={website};") == "Website", "websites are recorded")
        add_source(admin, f"https://www.youtube.com/playlist?list={MISSING}", "Không tồn tại")
        missing = source_id(MISSING)

        page = f"/admin-sources/{playlist}"
        check(form(admin, page, f"{page}/sync")[0] == 302, "Admin syncs the pending playlist")
        check(sql(f"SELECT COUNT(*) FROM dbo.SourceVideoSources WHERE SourceId={playlist};") == "3" and video_status(VIDEOS[0]) == "Pending", "new videos wait for review")
        check(video_status(VIDEOS[1]) == "Blocked" and sql(f"SELECT COUNT(*) FROM dbo.SourceVideos WHERE VideoId=N'{VIDEOS[1]}' AND ReviewNote LIKE N'%{KEYWORD}%';") == "1", "a title with a blocked keyword is blocked automatically")
        check(sql(f"SELECT ThumbnailUrl FROM dbo.SourceVideos WHERE VideoId=N'{VIDEOS[0]}';") == f"https://i.ytimg.com/vi/{VIDEOS[0]}/hqdefault.jpg", "thumbnails use YouTube's image host, not the feed's URL")
        check(sql(f"SELECT COUNT(*) FROM dbo.ContentSources WHERE Id={playlist} AND LastSyncedAt IS NOT NULL AND LastSyncError IS NULL;") == "1", "the sync time is recorded")
        form(admin, page, f"{page}/sync")
        check(sql(f"SELECT COUNT(*) FROM dbo.SourceVideos WHERE VideoId IN (N'{VIDEOS[0]}', N'{VIDEOS[1]}', N'{VIDEOS[2]}');") == "3", "syncing again adds no duplicates")
        form(admin, f"/admin-sources/{website}", f"/admin-sources/{website}/sync")
        check(sql(f"SELECT COUNT(*) FROM dbo.ContentSources WHERE Id={website} AND LastSyncError LIKE N'%website%';") == "1", "websites are not fetched")
        form(admin, f"/admin-sources/{missing}", f"/admin-sources/{missing}/sync")
        check(sql(f"SELECT COUNT(*) FROM dbo.ContentSources WHERE Id={missing} AND LastSyncError IS NOT NULL;") == "1"
              and "Đồng bộ chưa thành công" in admin.request(f"/admin-sources/{missing}")[2], "a missing playlist reports the error")

        check(form(admin, page, f"{page}/assess")[0] == 302 and sql(f"SELECT ModelName + N'|' + Status FROM dbo.CurationSuggestions WHERE SourceId={playlist};") == "rule-v1|Pending",
              "the rule engine scores the source")
        score = float(sql(f"SELECT Score FROM dbo.CurationSuggestions WHERE SourceId={playlist};"))
        check(score < 75 and "Đây chỉ là gợi ý" in admin.request(page)[2], "the blocked keyword lowers the score and the page says admins decide")
        check(form(admin, page, f"{page}/status", [("to", "Rejected"), ("note", "")])[0] == 302
              and sql(f"SELECT Status FROM dbo.ContentSources WHERE Id={playlist};") == "Suggested", "rejecting needs a reason")
        check(form(admin, page, f"{page}/status", [("to", "Approved")])[0] == 302
              and sql(f"SELECT Status FROM dbo.ContentSources WHERE Id={playlist} AND ReviewedById IS NOT NULL;") == "Approved", "Admin approves the source")
        check(sql(f"SELECT Status FROM dbo.CurationSuggestions WHERE SourceId={playlist};") == "Dismissed", "approving against a low score closes the suggestion as dismissed")

        videos = "/admin-videos"
        video0 = sql(f"SELECT Id FROM dbo.SourceVideos WHERE VideoId=N'{VIDEOS[0]}';")
        check(form(admin, videos, f"/admin-videos/{video0}/status", [("to", "Blocked"), ("note", "")])[0] == 302 and video_status(VIDEOS[0]) == "Pending", "blocking a video needs a reason")
        check(form(admin, videos, f"/admin-videos/{video0}/status", [("to", "Approved"), ("back", "https://evil.example/")])[1].get("Location", "").startswith("/admin-videos")
              and video_status(VIDEOS[0]) == "Approved", "Admin approves a video and is never redirected off-site")
        check(VIDEOS[2] in admin.request("/admin-videos")[2] and VIDEOS[0] not in admin.request("/admin-videos")[2], "the review queue lists pending videos")
        # The content form only appears when editing a saved lesson.
        editor = "/admin-lesson/" + sql("SELECT TOP 1 Id FROM dbo.Lessons ORDER BY Id;")
        check(f'<option value="{video0}">' in admin.request(editor, raw=True)[2], "lesson editors can pick the approved video")

        sql("INSERT dbo.Lessons(SubjectId, GradeLevelId, Title, Summary, LessonType, DurationMin, SortOrder, Status, CreatedById, CreatedAt, UpdatedAt, PublishedAt) "
            f"SELECT TOP 1 sg.SubjectId, sg.GradeLevelId, N'{TAG}', N'Bài thử nguồn', N'Video', 5, 99, N'Published', N'{admin_id}', SYSUTCDATETIME(), SYSUTCDATETIME(), SYSUTCDATETIME() "
            "FROM dbo.SubjectGrades sg JOIN dbo.Subjects s ON s.Id=sg.SubjectId WHERE s.IsActive=1;")
        lesson = sql(f"SELECT Id FROM dbo.Lessons WHERE Title=N'{TAG}';")
        sql(f"INSERT dbo.LessonContents(LessonId, ContentKind, Title, SourceVideoId, SortOrder) VALUES ({lesson}, N'Video', N'Video thử', {video0}, 1);")
        detail = f"/lesson-detail/{lesson}"
        embedded = lambda client: f"embed/{VIDEOS[0]}" in client.request(detail, raw=True)[2]
        check(embedded(parent) and embedded(other), "families see an approved video from an approved source")

        sources = "/sources"
        status, _, body = parent.request(sources)
        check(status == 200 and "Danh sách thử" in body and "Website thử" not in body, "parents see approved sources only")
        check(form(parent, sources, f"/sources/{playlist}/block")[0] == 302
              and sql(f"SELECT ps.Status FROM dbo.ParentSources ps JOIN dbo.Users u ON u.Id=ps.ParentId WHERE u.Email=N'{PARENT}' AND ps.SourceId={playlist};") == "Blocked",
              "a parent blocks a source for the family")
        check(not embedded(parent) and embedded(other), "the block hides the source's video for that family only")
        check(form(parent, sources, f"/sources/{playlist}/allow")[0] == 302 and embedded(parent), "the family can allow it again")
        check(form(parent, sources, f"/sources/{website}/block")[0] == 404, "families cannot choose sources the admins have not approved")
        check(parent.request(f"/sources/{playlist}/block", {})[0] == 400, "family choices require CSRF")

        suggest = lambda client, link, name: form(client, sources, "/sources/suggest", [("Suggest.Url", link), ("Suggest.Name", name), ("Suggest.Description", "Bé thích")])
        check(suggest(parent, "notalink", "Sai")[0] == 200, "a bad suggestion link is refused")
        status, _, body = suggest(parent, f"https://www.youtube.com/playlist?list={PLAYLIST}", "Trùng")
        check(status == 200 and "đã được duyệt" in body, "suggesting an approved source says it is already there")
        mine = [f"PLmine{i}{RUN[:18]}" for i in range(6)]
        created_urls.extend(f"https://www.youtube.com/playlist?list={item}" for item in mine)
        for item in mine[:5]:
            suggest(parent, f"https://www.youtube.com/playlist?list={item}", "Đề xuất " + item[-4:])
        check(sql(f"SELECT COUNT(*) FROM dbo.ContentSources cs JOIN dbo.Users u ON u.Id=cs.SuggestedById WHERE u.Email=N'{PARENT}' AND cs.Status=N'Suggested';") == "5",
              "parents suggest new sources for review")
        check(suggest(parent, f"https://www.youtube.com/playlist?list={mine[5]}", "Quá nhiều")[0] == 200 and source_id(mine[5]) == "", "pending suggestions per family are limited")
        check(PARENT in admin.request("/admin-curate?status=Suggested")[2], "admins see who suggested a source")
        first = source_id(mine[0])
        form(admin, f"/admin-sources/{first}", f"/admin-sources/{first}/status", [("to", "Rejected"), ("note", "Không phù hợp lứa tuổi")])
        check("Không phù hợp lứa tuổi" in parent.request(sources)[2], "the family sees why its suggestion was rejected")

        check(form(admin, page, f"{page}/delete")[0] == 302 and source_id(PLAYLIST) == playlist, "an approved source cannot be deleted")
        check(form(admin, page, f"{page}/status", [("to", "Suspended"), ("note", "Kiểm tra lại")])[0] == 302
              and sql(f"SELECT Status FROM dbo.ContentSources WHERE Id={playlist};") == "Suspended", "Admin suspends the source")
        check(not embedded(parent) and not embedded(other), "videos of a suspended source stop playing")
        check("nguồn bị tạm ngưng" in admin.request(f"/admin-lesson/{lesson}")[2], "the lesson editor flags the unavailable video")
        check(form(admin, page, f"{page}/delete")[0] == 302 and source_id(PLAYLIST) == "", "a suspended source can be deleted")
        check(sql(f"SELECT COUNT(*) FROM dbo.ParentSources WHERE SourceId={playlist};") == "0"
              and video_status(VIDEOS[0]) == "Approved", "deleting a source removes family choices and keeps the videos")
        sql(f"DELETE FROM dbo.Lessons WHERE Id={lesson};")
        approved = sql("SELECT COUNT(*) FROM dbo.ContentSources WHERE Status=N'Approved' AND Platform<>N'Website';")
        # Bulk sync touches sources the test did not create; keep their sync columns to restore afterwards.
        sql(f"SELECT Id, LastSyncedAt, LastSyncError INTO dbo.SmokeSyncBackup_{RUN[:12]} FROM dbo.ContentSources;")
        check(form(admin, "/admin-curate", "/admin-sources/sync-approved")[0] == 302, f"Admin syncs all {approved} approved sources at once")
        check(sql(f"SELECT COUNT(*) FROM dbo.AuditLogs WHERE ActorId=N'{admin_id}' AND EntityType IN (N'ContentSource', N'SourceVideo');") != "0", "source decisions are audited")


def api_checks(url, directory):
    logs = []
    with smoke.running_app(url, directory, {"YouTube__ApiBaseUrl": FAKE + "/v3", "YouTube__ApiKey": KEY}, logs):
        admin = smoke.Client(url)
        check(admin.post_form("/login", {"Email": ADMIN, "Password": PASSWORD})[0] == 302, "admin signs in to the API run")
        add_source(admin, f"https://www.youtube.com/channel/{CHANNEL}", "Kênh qua API")
        channel = source_id(CHANNEL)
        check(sql(f"SELECT Platform FROM dbo.ContentSources WHERE Id={channel};") == "YouTubeChannel", "Admin adds a channel by its id")
        form(admin, f"/admin-sources/{channel}", f"/admin-sources/{channel}/sync")
        check(sql(f"SELECT COUNT(*) FROM dbo.SourceVideoSources WHERE SourceId={channel};") == "3", "with a key, sync reads every page of the uploads playlist")
        check(sql(f"SELECT DurationSec FROM dbo.SourceVideos WHERE VideoId=N'{API_VIDEOS[0]}';") == "245", "the API sync stores durations")
        check(video_status(API_VIDEOS[2]) == "Blocked" and sql(f"SELECT COUNT(*) FROM dbo.SourceVideos WHERE VideoId=N'{API_VIDEOS[2]}' AND ReviewNote LIKE N'%YouTube%';") == "1",
              "videos YouTube will not embed are blocked")
        add_source(admin, f"https://www.youtube.com/playlist?list={QUOTA}", "Hết hạn mức")
        quota = source_id(QUOTA)
        form(admin, f"/admin-sources/{quota}", f"/admin-sources/{quota}/sync")
        check("hạn mức" in admin.request(f"/admin-sources/{quota}")[2], "an exhausted quota is explained")
    check(KEY not in "".join(logs), "the API key never reaches the logs")


def main():
    global FAKE
    port = free_port()
    server = ThreadingHTTPServer(("127.0.0.1", port), FakeYouTube)
    threading.Thread(target=server.serve_forever, daemon=True).start()
    FAKE = f"http://127.0.0.1:{port}"
    try:
        with tempfile.TemporaryDirectory(prefix="safelearn-private-mail-") as temporary:
            directory = Path(temporary)
            feed_checks(f"http://127.0.0.1:{free_port()}", directory)
            api_checks(f"http://127.0.0.1:{free_port()}", directory)
    finally:
        server.shutdown()
        emails = f"N'{ADMIN}', N'{PARENT}', N'{OTHER}'"
        ids = f"SELECT Id FROM dbo.Users WHERE Email IN ({emails})"
        externals = ", ".join(f"N'{item}'" for item in [PLAYLIST, MISSING, QUOTA, CHANNEL] + [f"PLmine{i}{RUN[:18]}" for i in range(6)])
        sql(f"IF OBJECT_ID(N'dbo.SmokeSyncBackup_{RUN[:12]}') IS NOT NULL BEGIN UPDATE s SET LastSyncedAt=b.LastSyncedAt, LastSyncError=b.LastSyncError FROM dbo.ContentSources s JOIN dbo.SmokeSyncBackup_{RUN[:12]} b ON b.Id=s.Id; DROP TABLE dbo.SmokeSyncBackup_{RUN[:12]}; END")
        sql(f"DELETE FROM dbo.Lessons WHERE Title=N'{TAG}';")
        sql(f"DELETE FROM dbo.ParentSources WHERE ParentId IN ({ids}) OR SourceId IN (SELECT Id FROM dbo.ContentSources WHERE ExternalId IN ({externals}) OR Url LIKE N'https://example.org/hoc-{RUN[:8]}%');")
        sql(f"DELETE FROM dbo.ContentSources WHERE ExternalId IN ({externals}) OR Url LIKE N'https://example.org/hoc-{RUN[:8]}%' OR SuggestedById IN ({ids});")
        video_ids = ", ".join(f"N'{item}'" for item in VIDEOS + API_VIDEOS)
        sql(f"DELETE FROM dbo.SourceVideos WHERE VideoId IN ({video_ids});")
        sql(f"DELETE FROM dbo.BlockedKeywords WHERE Keyword=N'{KEYWORD}';")
        sql(f"DELETE FROM dbo.AuditLogs WHERE ActorId IN ({ids}) OR EntityId IN ({ids});")
        sql(f"DELETE FROM dbo.LoginHistories WHERE UserNameTried IN ({emails});")
        sql(f"DELETE FROM dbo.Users WHERE Email IN ({emails});")
        check(sql(f"SELECT COUNT(*) FROM dbo.Users WHERE Email IN ({emails});") == "0"
              and sql(f"SELECT COUNT(*) FROM dbo.SourceVideos WHERE VideoId IN ({video_ids});") == "0"
              and sql(f"SELECT COUNT(*) FROM dbo.ContentSources WHERE ExternalId IN ({externals});") == "0", "generated source data is cleaned up")
    print(f"RESULT {smoke.checks}/{smoke.checks} passed (sources + sync + videos + family choices + assessment)")


FAKE = ""

if __name__ == "__main__":
    main()
