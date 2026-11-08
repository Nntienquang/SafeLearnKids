---

description: "Task list for Quản lý bài học (lesson management)"
---

# Tasks: Quản lý bài học

**Input**: Design documents from `specs/001-lesson-management/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/admin-lesson-routes.md, quickstart.md

**Tests**: Included. Constitution principle VI requires automated tests for new business logic; this
project uses Python smoke tests in `scripts/` against the built app and real SQL Server.

**Organization**: Tasks are grouped by user story so each story can be implemented and tested
independently. All paths are relative to the repository root (`SafeLearnKids/`).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: User story the task belongs to (US1–US4)

## Conventions for every task

- Code, identifiers and comments in English; user-facing text in Vietnamese.
- Services follow `Services/CatalogService.cs`: resolve `SafeLearnDbContext` via
  `IServiceProvider.GetService`, return `null`/unavailable when running InMemory.
- Controller actions follow `Areas/Admin/Controllers/CatalogController.cs`: `TempData["Info"]` /
  `TempData["Error"]`, redirect after POST, audit through `AccountActivityLog`.
- Catch only expected SQL failures (`DbUpdateConcurrencyException`, SQL errors 547/2601/2627) and
  return a Vietnamese message; let anything else reach the global exception handler.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Branch and test harness for this feature

- [X] T001 Create and switch to branch `feature/lesson-management` from the current `Sprint-1` HEAD with `git switch -c` so the uncommitted subject-catalog work (`Services/CatalogService.cs`, `Models/CatalogViewModels.cs`, `Areas/Admin/Views/Catalog/*`, `CatalogController.cs`, `AccountActivityLog.cs`, `Program.cs`, `wwwroot/js/admin.js`, `scripts/smoke-catalog.py`) comes along — T003, T004, T005 and T016 build on it. Ask the user before committing; when approved, commit the catalog work first as its own English commit (e.g. `feat(catalog): manage subjects and list lessons from SQL`) and the spec-kit files (`.specify/`, `specs/`) as a separate `docs:` commit, before any lesson code
- [X] T002 Create `scripts/smoke-lessons.py` skeleton reusing `scripts/smoke-sql-auth.py` the same way `scripts/smoke-catalog.py` does: random `RUN` id, disposable Admin and Parent accounts promoted via `sqlcmd`, `send()` helper that posts repeated fields with the antiforgery token and unescapes the body, `texts()` helper that reads nvarchar via UTF-16 hex, `main()` with `finally` cleanup that deletes every lesson whose Title starts with `Smoke {RUN}` (contents and tags cascade), their `AuditLogs` rows (`EntityType=N'Lesson'`), and the disposable accounts; print `RESULT n/n passed (lesson management)`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared rules, models, service shells and routing that every story uses

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T003 [P] Refactor `Services/AccountActivityLog.cs`: replace `CatalogActionAsync` with `EntityActionAsync(string actorId, string action, string entityType, int entityId, string? detail = null)`, add `public const string LessonEntity = "Lesson"` next to `SubjectEntity`; update every call in `Areas/Admin/Controllers/CatalogController.cs` to `EntityActionAsync(..., AccountActivityLog.SubjectEntity, ...)` (research R6)
- [X] T004 [P] Create `Services/LessonWorkflow.cs` (static, no I/O): constants `Draft`, `InReview`, `Published`, `Archived`; content kinds `Video`, `Reading`, `Document`, `Image`; lesson types `Video`, `Reading`, `Mixed`; `CanChange(from, to)` allowing only Draft→Published, Published→Archived and {InReview (legacy), Published, Archived}→Draft; `IsEditable(status)` true only for Draft; `LessonTypeFor(IEnumerable<string> kinds)` returning "only Video → `Video`; no Video → `Reading`; Video and another kind → `Mixed`; no content → `Video`" (research R2, R10). `LessonWorkflow` becomes the single source of lesson status strings: in `Models/CatalogViewModels.cs` change `CatalogOptions.Statuses` to `[LessonWorkflow.Draft, LessonWorkflow.InReview, LessonWorkflow.Published, LessonWorkflow.Archived]` and the `CatalogOptions.Status` switch cases to the same constants, so no status literal remains outside `LessonWorkflow` (constitution I, III)
- [X] T005 [P] Create `Models/LessonViewModels.cs` with `LessonFormViewModel` (`Id?`, `SubjectId`, `GradeLevelId`, `Title` "Bắt buộc, trim, 1–200 ký tự", `Summary` "Tùy chọn, ≤ 500 ký tự", `DurationMin` "≥ 0" with no upper bound, `SortOrder` "0–999", `RowVersion` base64 string, plus `SubjectName`, `Grades` options), `LessonSaveResult(int? Id, string? Error, bool NotFound = false, bool Conflict = false)`; do not define status labels/tones here — views use the existing `CatalogOptions.Status` from `Models/CatalogViewModels.cs` (`Bản nháp`/`a`, `Chờ duyệt`/`b`, `Đã xuất bản`/`g`, `Lưu trữ`/`n`)
- [X] T006 Create `Services/LessonService.cs` shell: primary constructor `(IServiceProvider services)`, `Db` accessor, `Available`, private helpers `Expect(Lesson lesson, string rowVersion)` (sets `Entry(lesson).Property(l => l.RowVersion).OriginalValue` from base64, invalid base64 → conflict) and `Touch(Lesson lesson)` (sets `UpdatedAt = DateTime.UtcNow`), `SqlError(Exception)` like `CatalogService`
- [X] T007 Create `Services/LessonContentService.cs` shell with the same constructor, `Db` accessor and reuse of the RowVersion/Touch helpers (make them `internal static` in `LessonService` or a small shared static class in the same file — do not copy them)
- [X] T008 Register `LessonService` and `LessonContentService` as scoped services in `Program.cs` next to `CatalogService` (depends on T006, T007)
- [X] T009 Create `Areas/Admin/Controllers/LessonsController.cs`: `[Area("Admin")]`, `[Authorize(Roles = "Admin")]`, primary constructor `(LessonService lessons, LessonContentService contents, AccountActivityLog activity, UserManager<ApplicationUser> users)`, private `ActorId`, private `Unavailable()` that sets `TempData["Error"]` "Bài học chỉ có khi chạy với SQL Server." and redirects to `/admin-catalog`
- [X] T010 [P] Replace the static lesson page: in `Areas/Admin/Controllers/CatalogController.cs` change `GET admin-lesson` to `RedirectToAction(nameof(Index))`; delete `Areas/Admin/Views/Catalog/Lesson.cshtml`; remove the `if(P==='admin-lesson'){...}` block from `wwwroot/js/admin.js` and any helper used only by it (research R8)

**Checkpoint**: Build succeeds; `/admin-lesson` redirects to `/admin-catalog`; subject catalog smoke still passes

---

## Phase 3: User Story 1 - Tạo và sửa thông tin bài học (Priority: P1) 🎯 MVP — Trello US-12

**Goal**: Admin creates a Draft lesson from the selected subject and edits its information without silently overwriting another Admin's changes.

**Independent Test**: Create a Draft for Toán · a grade assigned to Toán, see it in the Toán lesson list as "Bản nháp"; rename it and see the new name after reload; a stale second save is rejected.

### Tests for User Story 1

- [X] T011 [US1] Add US1 checks to `scripts/smoke-lessons.py`: anonymous → login redirect and Parent → `/error/403` for `/admin-lessons/new?subject=1` and `/admin-lessons/1`; GET new form lists only grades in `SubjectGrades` of the subject; POST without antiforgery → 400; blank title, 201-char title, 501-char summary, negative duration, grade not assigned to the subject → form redisplayed (200) and no row created; valid create → 302 to `/admin-lessons/{id}` with `Status='Draft'`, `LessonType='Video'`, `CreatedById` = admin, `SortOrder` = previous max in subject+grade + 1, audit `CreateLesson`; edit saves title/summary/duration; posting an old `RowVersion` → 200 with conflict message and no change; unknown id → 404; title `<b>x</b>` is HTML-encoded on the editor page

### Implementation for User Story 1

- [X] T012 [US1] Implement in `Services/LessonService.cs`: `NewFormAsync(int subjectId)` (subject name, active flag, grades from `SubjectGrades`; `null` if subject missing), `FormAsync(int id)` (projection incl. base64 `RowVersion`), `CreateAsync(LessonFormViewModel, string actorId)` (trim; validate per T005 constraints and that (SubjectId, GradeLevelId) exists in `SubjectGrades`; `SortOrder` defaults to max in subject+grade + 1 when not supplied; `Status = Draft`, `LessonType = Video`, `CreatedById = actorId`, `CreatedAt`/`UpdatedAt` UTC), `UpdateAsync(int id, LessonFormViewModel)` (only when `LessonWorkflow.IsEditable`; same validation; `Expect` + `Touch`; `DbUpdateConcurrencyException` → `Conflict` with "Bài đã được người khác thay đổi. Hãy tải lại trang.")
- [X] T013 [US1] Add to `Areas/Admin/Controllers/LessonsController.cs`: `GET admin-lessons/new?subject=` (404 if subject missing), `POST admin-lessons/new` (redisplay form on error, audit `CreateLesson`, 302 to editor with `TempData["Info"]`), `GET admin-lessons/{id:int}`, `POST admin-lessons/{id:int}` (redisplay editor with message on error/conflict, audit `UpdateLesson`) per `contracts/admin-lesson-routes.md`
- [X] T014 [P] [US1] Create `Areas/Admin/Views/Lessons/Create.cshtml` following `Areas/Admin/Views/Catalog/SubjectForm.cshtml` layout (`sl-card acc-form`, `acc-sec`, `field`, `asp-validation-for`): subject shown read-only, grade select limited to the subject's grades, title, summary, duration, sort order; Cancel returns to `/admin-catalog?subject={id}`
- [X] T015 [US1] Create `Areas/Admin/Views/Lessons/Edit.cshtml` with the information section: TempData banners, status chip, warning banner when the subject is hidden, info form with hidden `RowVersion`, all inputs `disabled` and save button hidden when the lesson is not Draft (with hint "Đưa bài về Bản nháp để sửa"); leave clearly marked sections for contents, tags and status to be filled by US2–US4
- [X] T016 [P] [US1] Update `Areas/Admin/Views/Catalog/Index.cshtml`: "Bài học mới" → `/admin-lessons/new?subject={selected.Id}` (hidden when no subject is selected); each lesson's "Sửa" → `/admin-lessons/{lesson.Id}`

**Checkpoint**: US1 smoke checks pass; lessons can be created and edited end to end

---

## Phase 4: User Story 2 - Quản lý nội dung trong bài (Priority: P2) — Trello US-13

**Goal**: Admin adds Video (approved only) and Reading content, edits, reorders and deletes it; the lesson type follows the content.

**Independent Test**: On a Draft, add an approved video and a reading, swap their order, reload and see the new order; delete the reading and see only the video; lesson type goes Video → Mixed → Video.

### Tests for User Story 2

- [X] T017 [US2] Add US2 checks to `scripts/smoke-lessons.py`: editor video list contains only `SourceVideos.Status='Approved'` titles and not the Blocked one; posting a Blocked `SourceVideoId` → error and no row; add video → `ContentKind='Video'`, `SortOrder=1`, title defaults to the video title; add Reading with blank body → error; add Reading with a stale `RowVersion` → 200, error shown and the typed title and body still present in the add form (FR-018); add Reading `<script>alert(1)</script>` → stored and shown encoded; `LessonType` becomes `Mixed`; move second item up → `SortOrder` 1..n in new order; edit reading title/body; delete reading (data-confirm form) → remaining `SortOrder` renumbered 1..n and `LessonType='Video'`; stale `RowVersion` on any content POST → error and no change; content POST on a non-Draft lesson → error; audits `AddLessonContent`, `MoveLessonContent`, `UpdateLessonContent`, `DeleteLessonContent`

### Implementation for User Story 2

- [X] T018 [US2] Implement in `Services/LessonContentService.cs`: `ApprovedVideosAsync()` (Id, Title, DurationSec; `Status = 'Approved'` only), `ContentsAsync(int lessonId)` (ordered rows with kind, title, video title/duration/status, body, and an `IsValid` flag per research R3), `AddAsync(int lessonId, string rowVersion, kind, title, sourceVideoId, body)`, `UpdateAsync(...)`, `MoveAsync(int lessonId, int contentId, string rowVersion, bool up)`, `DeleteAsync(int lessonId, int contentId, string rowVersion)`. Rules: lesson must be Draft; kinds created are only `Video` (video must be Approved at time of adding) or `Reading` (Body non-empty after trim, ≤ 20 000 characters); `Title` ≤ 150 characters; after every change renumber `SortOrder` 1..n, set `LessonType = LessonWorkflow.LessonTypeFor(...)`, `Touch` the lesson and `Expect` its RowVersion in the same `SaveChanges`; existing `Document`/`Image` items can be moved/deleted but not created or edited
- [X] T019 [US2] Add content routes to `Areas/Admin/Controllers/LessonsController.cs`: `POST admin-lessons/{id}/contents`, `POST admin-lessons/{id}/contents/{contentId}`, `POST admin-lessons/{id}/contents/{contentId}/move`, `POST admin-lessons/{id}/contents/{contentId}/delete`; on success each redirects to the editor with `TempData`; on error the add and edit routes re-render `Edit.cshtml` (200) with the error and the submitted kind, title, body and video selection, while move and delete redirect with `TempData["Error"]` (contract; FR-018); audits per research R6 with the content title as detail; 404 when the lesson or content does not belong together
- [X] T020 [US2] Fill the contents section of `Areas/Admin/Views/Lessons/Edit.cshtml`: ordered list with kind icon, title, video duration or reading excerpt rendered with `white-space: pre-wrap`, red "không còn hợp lệ" chip when `IsValid` is false, "Lên"/"Xuống" buttons (hidden at the ends), edit form per item, delete form with `data-confirm`/`data-danger`; add form with kind switch (Video select of approved videos / Reading textarea), pre-filled from the editor model when re-rendered after an error, and the same for the edit form of the item that failed; every form carries the lesson `RowVersion`; all controls hidden when the lesson is not Draft; empty state "Bài chưa có nội dung"

**Checkpoint**: US1 and US2 smoke checks pass

---

## Phase 5: User Story 3 - Vòng đời bài: xuất bản, lưu trữ (Priority: P3) — Trello US-12

**Goal**: Lessons move Draft → Published → Archived (and back to Draft) only when valid, with publisher and time recorded. There is no review step; legacy `InReview` lessons can only go back to Draft.

**Independent Test**: A Draft without content cannot be published; with content it goes to Published (publisher + time set), Archived, then Draft; Published → Draft asks for confirmation.

### Tests for User Story 3

- [X] T021 [US3] Add US3 checks to `scripts/smoke-lessons.py`: publish a lesson with no content → error naming the missing requirement, status unchanged; add a reading then publish → `Published`, `ApprovedById` = admin, `PublishedAt` set; content and info POSTs now rejected; Published→InReview and Draft→Archived (not allowed) → error; archive → `Archived`; back to Draft → `Draft`, `ApprovedById` NULL, `PublishedAt` kept; a seed `InReview` lesson offers only "Về bản nháp" (do not change seed data: check the page, not the transition); a lesson whose only video is temporarily set to `Blocked` (restore it in `finally`) cannot be published and the message names the content; stale `RowVersion` → error; every success audited as `ChangeLessonStatus` with detail like `Draft→Published`; the "Về bản nháp" form on a Published lesson carries `data-confirm`

### Implementation for User Story 3

- [X] T022 [US3] Implement `ChangeStatusAsync(int id, string target, string rowVersion, string actorId)` in `Services/LessonService.cs`: reject unless `LessonWorkflow.CanChange`; for → Published require at least one content item where `LessonWorkflow.IsPublishable` is true and list invalid video items by title in the error; → Published sets `ApprovedById = actorId`, `PublishedAt = UtcNow`; → Draft clears `ApprovedById`; `Touch` + `Expect`; return old→new for the audit detail (research R2, R3, R11)
- [X] T023 [US3] Add `POST admin-lessons/{id}/status` (`target`, `RowVersion`) to `Areas/Admin/Controllers/LessonsController.cs`, audit `ChangeLessonStatus` with detail `"{from}→{to}"`
- [X] T024 [US3] Fill the status section of `Areas/Admin/Views/Lessons/Edit.cshtml`: current status chip, publisher name and published time when present, buttons only for transitions allowed by `LessonWorkflow.CanChange` from the current status ("Xuất bản", "Lưu trữ", "Về bản nháp"); "Về bản nháp" from Published uses `data-confirm` with text "Bé sẽ tạm không thấy bài cho tới khi bài được xuất bản lại."; info line "Cần ít nhất 1 nội dung hợp lệ để xuất bản."

**Checkpoint**: US1–US3 smoke checks pass

---

## Phase 6: User Story 4 - Xóa bài (Priority: P4) — Trello US-12

**Goal**: Admin deletes mistaken Draft lessons that have no related data. Tagging (Trello US-14) is out of scope.

**Independent Test**: Delete a fresh Draft and see it gone from the catalog.

### Tests for User Story 4

- [X] T025 [US4] Add US4 checks to `scripts/smoke-lessons.py`: delete seed lesson 1 (has activity) → refused with archive hint and still present; delete a non-Draft smoke lesson → refused; delete a fresh smoke Draft with contents → lesson and contents gone, redirect to `/admin-catalog?subject={id}`, audit `DeleteLesson`; stale `RowVersion` → refused

### Implementation for User Story 4

- [X] T026 [US4] Implement in `Services/LessonService.cs`: `DeleteAsync(int id, string rowVersion)` (Draft only; refuse when any row exists in `Assignments`, `ContentReports`, `Favorites`, `LearningPathItems`, `LessonActivities`, `Questions`, `Quizzes` for the lesson with "Bài đã có dữ liệu liên quan nên không xóa được. Hãy lưu trữ bài."; catch SQL error 547 the same way; contents and any legacy tags cascade) (research R5)
- [X] T027 [US4] Add `POST admin-lessons/{id}/delete` to `Areas/Admin/Controllers/LessonsController.cs`; audit `DeleteLesson` (detail: title); delete redirects to `/admin-catalog?subject={subjectId}`
- [X] T028 [US4] Add to `Areas/Admin/Views/Lessons/Edit.cshtml` a delete form with `data-confirm="Xóa vĩnh viễn bài …?"`, `data-danger="true"`, `sl-btn sl-btn-d`, shown only for Draft lessons

**Checkpoint**: All user stories independently functional

---

## Phase 7: Polish & Cross-Cutting Concerns

- [X] T029 Run `dotnet build SafeLearnKids.Web.csproj` and fix every error and new compiler warning
- [X] T030 Run `py scripts\smoke-lessons.py`, `py scripts\smoke-catalog.py`, `py scripts\smoke-admin-users.py`, `py scripts\check-auth-regressions.py`; all must print `RESULT n/n passed`; confirm the 5 seed lessons, seed videos and seed tags are unchanged afterwards
- [X] T031 Add an InMemory check to `scripts/smoke-lessons.py`: with `Demo__UseInMemoryDatabase=true` and a bootstrap Admin, `/admin-lessons/new?subject=1` and `/admin-lessons/1` show or redirect with the SQL-only message instead of an error page
- [X] T032 Walk through `specs/001-lesson-management/quickstart.md` manually in the browser at desktop and mobile width (no horizontal page scroll) and fix layout issues in `Areas/Admin/Views/Lessons/*.cshtml`
- [X] T033 [P] Add a short "Quản lý bài học" section to `README.md` (routes, status rules, publish condition, smoke script) next to the existing user-management section
- [X] T034 Review the diff against the constitution: no duplicated logic between `LessonService`/`LessonContentService`/`CatalogService`, no magic strings outside `LessonWorkflow`, no `Console.WriteLine`, comments explain why only; summarise changes and test steps for the user (Definition of Done)

- [X] T035 Compute lesson length from content (clarification 2026-11-03): add `LessonWorkflow.DurationMinutesFor` (videos' `DurationSec` + readings at 100 words a minute, rounded up), call it in `LessonContentService` next to the lesson type, start new lessons at 0, remove the duration input from `_LessonFields.cshtml` and `LessonFormViewModel`, show the computed length in the status card, add `HasSentinel(-1)` for `DurationMin` in `Data/SafeLearnDbContext.cs`, and cover it in `scripts/smoke-lessons.py`
- [X] T036 Add a "Xem thử" preview for YouTube video parts in `Areas/Admin/Views/Lessons/Edit.cshtml`: `LessonWorkflow.EmbedUrlFor` builds `https://www.youtube-nocookie.com/embed/{VideoId}`, the iframe is lazy-loaded inside a closed `<details>` with `referrerpolicy="strict-origin-when-cross-origin"`; covered by `scripts/smoke-lessons.py`. Real playback needs real `SourceVideos.VideoId` values (seed data uses fake ids)
---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: depends on the uncommitted subject-catalog work being present in the working tree (T001)
- **Foundational (Phase 2)**: depends on Setup; blocks all user stories
- **US1 (Phase 3)**: depends on Foundational — MVP
- **US2 (Phase 4)**: depends on US1 (needs a lesson and the editor page)
- **US3 (Phase 5)**: depends on US2 (publish rule needs content)
- **US4 (Phase 6)**: depends on US1 only; can run in parallel with US2/US3 if a different person owns `LessonService.cs` edits carefully, but `Edit.cshtml` and `LessonsController.cs` are shared, so sequential is safer
- **Polish (Phase 7)**: depends on all stories

### Within Each Story

- Service → controller → view; the story's smoke checks are written first and run after the view task
- `scripts/smoke-lessons.py`, `Areas/Admin/Controllers/LessonsController.cs` and `Areas/Admin/Views/Lessons/Edit.cshtml` are edited by every story, so their tasks are never marked [P]

### Parallel Opportunities

- Phase 2: T003, T004, T005, T010 touch different files and can run together; T006/T007 after T004–T005; T008 after T006–T007; T009 after T006–T007
- US1: T014 and T016 can run in parallel with T012 once T005 is done

## Parallel Example: Foundational

```text
Task: "T003 Refactor AccountActivityLog to EntityActionAsync in Services/AccountActivityLog.cs"
Task: "T004 Create LessonWorkflow in Services/LessonWorkflow.cs"
Task: "T005 Create LessonViewModels in Models/LessonViewModels.cs"
Task: "T010 Replace static lesson page in CatalogController.cs, Catalog/Lesson.cshtml, admin.js"
```

## Parallel Example: User Story 1

```text
Task: "T014 Create Areas/Admin/Views/Lessons/Create.cshtml"
Task: "T016 Update Areas/Admin/Views/Catalog/Index.cshtml links"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Phase 1 + Phase 2
2. Phase 3 (US1)
3. **Stop and validate**: US1 smoke checks + manual create/edit; demo to the team

### Incremental Delivery

1. US1 → create/edit lessons (MVP)
2. US2 → lessons have real content
3. US3 → lessons can be published safely
4. US4 → delete mistaken drafts
5. Each story ends with its smoke checks passing and can be committed as its own small English commit
   (e.g. `feat(lessons): create and edit draft lessons`)

## Notes

- Do not change the database schema; all tables already exist
- Uploading documents/images, quiz management, kid/parent pages reading real lessons and the lesson
  duration upper limit (Sprint 5) are out of scope
