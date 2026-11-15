/*
SafeLearn Kids: reference data a new installation needs (grades, roles, subjects, badges).
Safe to run more than once: rows are added only when missing, existing rows are never changed.
Save/run as UTF-8, e.g.  sqlcmd ... -f 65001 -i database/02-reference-data.sql
Lessons, sources and videos are not seeded: the admin adds them in the app (or restore a backup, see DEPLOY.md).
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- Grades 1 to 5 (primary school)
INSERT dbo.GradeLevels (Level, Name)
SELECT v.Level, v.Name
FROM (VALUES (1, N'Lớp 1'), (2, N'Lớp 2'), (3, N'Lớp 3'), (4, N'Lớp 4'), (5, N'Lớp 5')) v(Level, Name)
WHERE NOT EXISTS (SELECT 1 FROM dbo.GradeLevels g WHERE g.Level = v.Level);

-- Identity roles (the app also creates Admin and Parent at startup)
INSERT dbo.Roles (Id, Name, NormalizedName, ConcurrencyStamp)
SELECT NEWID(), v.Name, UPPER(v.Name), NEWID()
FROM (VALUES (N'Admin'), (N'Parent')) v(Name)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Roles r WHERE r.NormalizedName = UPPER(v.Name));

-- Subjects of the Vietnamese primary curriculum (2018 programme) and the grades that study them
DECLARE @subjects TABLE (Code nvarchar(20), Name nvarchar(100), Description nvarchar(300), IconKey nvarchar(30), ColorKey nvarchar(30), SortOrder int, FromGrade int, ToGrade int);
INSERT @subjects VALUES
    (N'MATH',      N'Toán',                  N'Số, phép tính, hình học và đo lường cho học sinh tiểu học',  N'calc',   N'blue',   1, 1, 5),
    (N'VIET',      N'Tiếng Việt',            N'Đọc, viết, nói và nghe tiếng Việt',                          N'pen',    N'green',  2, 1, 5),
    (N'ENGLISH',   N'Tiếng Anh',             N'Từ vựng, mẫu câu và phát âm tiếng Anh cơ bản',               N'globe',  N'purple', 3, 1, 5),
    (N'TNXH',      N'Tự nhiên và Xã hội',    N'Gia đình, trường học, cộng đồng, thực vật, động vật',         N'globe',  N'teal',   4, 1, 3),
    (N'SCIENCE',   N'Khoa học',              N'Chất, năng lượng, thực vật, động vật, con người và sức khỏe', N'flask',  N'orange', 5, 4, 5),
    (N'LSDL',      N'Lịch sử và Địa lý',     N'Địa phương, các vùng miền và lịch sử Việt Nam',              N'book',   N'red',    6, 4, 5),
    (N'LIFESKILL', N'Kỹ năng sống',          N'An toàn, tự phục vụ, giao tiếp và cảm xúc',                  N'heart',  N'teal',   7, 1, 5);

INSERT dbo.Subjects (Code, Name, Description, IconKey, ColorKey, SortOrder, IsActive)
SELECT s.Code, s.Name, s.Description, s.IconKey, s.ColorKey, s.SortOrder, 1
FROM @subjects s
WHERE NOT EXISTS (SELECT 1 FROM dbo.Subjects x WHERE x.Code = s.Code);

INSERT dbo.SubjectGrades (SubjectId, GradeLevelId)
SELECT x.Id, g.Id
FROM @subjects s
JOIN dbo.Subjects x ON x.Code = s.Code
JOIN dbo.GradeLevels g ON g.Level BETWEEN s.FromGrade AND s.ToGrade
WHERE NOT EXISTS (SELECT 1 FROM dbo.SubjectGrades sg WHERE sg.SubjectId = x.Id AND sg.GradeLevelId = g.Id);

-- Achievement badges (US-43); the admin can edit or add more at /admin-badges
INSERT dbo.Badges (Code, Name, Description, IconKey, CriteriaType, Threshold, IsActive)
SELECT v.Code, v.Name, v.Description, v.IconKey, v.CriteriaType, v.Threshold, 1
FROM (VALUES
    (N'FIRST_LESSON', N'Bước đầu tiên',   N'Học xong bài đầu tiên',                 N'star',   N'LessonsCompleted', 1),
    (N'LESSONS_10',   N'Ham học',         N'Học xong 10 bài',                       N'book',   N'LessonsCompleted', 10),
    (N'STREAK_3',     N'Kiên trì',        N'Học 3 ngày liên tiếp',                  N'flame',  N'StreakDays',       3),
    (N'STREAK_7',     N'Chăm chỉ cả tuần', N'Học 7 ngày liên tiếp',                 N'flame',  N'StreakDays',       7),
    (N'QUIZ_PERFECT', N'Điểm tuyệt đối',  N'Làm đúng hết 1 bài tập',                N'target', N'QuizPerfect',      1),
    (N'MINUTES_120',  N'Nhà khám phá',    N'Học tổng cộng 120 phút',                N'clock',  N'MinutesTotal',     120)
) v(Code, Name, Description, IconKey, CriteriaType, Threshold)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Badges b WHERE b.Code = v.Code);

COMMIT;
PRINT N'Reference data is in place.';
