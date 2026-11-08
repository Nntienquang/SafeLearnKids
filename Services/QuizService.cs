using Microsoft.EntityFrameworkCore;
using SafeLearnKids.Web.Data;
using SafeLearnKids.Web.Models;
using SafeLearnKids.Web.Models.Entities;

namespace SafeLearnKids.Web.Services;

// US-38/39: which quizzes a child may take, how an attempt is scored and how a result is shown.
public static class QuizService
{
    // Open to the child: the quiz is published, its lesson is published in an active subject, it has at least one
    // approved question, and either the lesson suits the child's grade and subjects or a parent assigned the quiz or the lesson.
    public static IQueryable<Quiz> Playable(SafeLearnDbContext db, KidInfo kid) =>
        db.Quizzes.AsNoTracking().Where(quiz => quiz.Status == QuestionStatus.Published
            && quiz.Lesson.Status == LessonStatus.Published && quiz.Lesson.Subject.IsActive
            && quiz.QuizQuestions.Any(link => link.Question.Status == QuestionStatus.Published)
            && ((quiz.Lesson.GradeLevelId == kid.GradeLevelId && kid.SubjectIds.Contains(quiz.Lesson.SubjectId))
                || quiz.Assignments.Any(item => item.ChildProfileId == kid.Id && item.Status != AssignmentStatus.Cancelled)
                || quiz.Lesson.Assignments.Any(item => item.ChildProfileId == kid.Id && item.Status != AssignmentStatus.Cancelled)));

    public static IQueryable<KidQuizCard> Cards(IQueryable<Quiz> quizzes, int kidId) =>
        quizzes.OrderBy(quiz => quiz.Lesson.Subject.SortOrder).ThenBy(quiz => quiz.Lesson.SortOrder).ThenBy(quiz => quiz.Id).Select(quiz => new KidQuizCard(
            quiz.Id, quiz.Title, quiz.LessonId, quiz.Lesson.Title, quiz.Lesson.Subject.Code, quiz.Lesson.Subject.Name, quiz.Lesson.Subject.ColorKey,
            quiz.QuizQuestions.Count(link => link.Question.Status == QuestionStatus.Published), quiz.TimeLimitSec,
            quiz.Assignments.Where(item => item.ChildProfileId == kidId && (item.Status == AssignmentStatus.Assigned || item.Status == AssignmentStatus.InProgress))
                .Select(item => item.DueDate).FirstOrDefault(),
            quiz.QuizAttempts.Where(item => item.ChildProfileId == kidId && item.SubmittedAt != null).Max(item => item.ScorePercent),
            quiz.Assignments.Any(item => item.ChildProfileId == kidId && (item.Status == AssignmentStatus.Assigned || item.Status == AssignmentStatus.InProgress))));

    // The questions of an attempt in the order the child saw them. The order is fixed per attempt (seeded by its id),
    // so a reload or the result page shows the same order.
    public static async Task<List<QuizQuestionView>> QuestionsAsync(SafeLearnDbContext db, int quizId, long attemptId, bool shuffle, CancellationToken cancellationToken)
    {
        var rows = await db.QuizQuestions.AsNoTracking().Where(link => link.QuizId == quizId && link.Question.Status == QuestionStatus.Published)
            .OrderBy(link => link.SortOrder).ThenBy(link => link.Id)
            .Select(link => new QuizQuestionView(link.QuestionId, link.Question.Text, link.Question.QuestionType,
                link.Question.QuestionOptions.OrderBy(option => option.SortOrder).ThenBy(option => option.Id).Select(option => new QuizOptionView(option.Id, option.Text)).ToList()))
            .ToListAsync(cancellationToken);
        if (!shuffle) return rows;
        var random = new Random((int)(attemptId % int.MaxValue));
        return rows.OrderBy(_ => random.Next()).ToList();
    }

    public static int? SecondsLeft(int? limit, DateTime startedAt) =>
        limit is { } seconds ? Math.Max(0, seconds - (int)(DateTime.UtcNow - startedAt).TotalSeconds) : null;

    // Scores an attempt on the server. Only options that belong to the question count; anything else is unanswered.
    // Points come from dbo.QuizQuestions so an admin can weight harder questions.
    public static async Task GradeAsync(SafeLearnDbContext db, QuizAttempt attempt, int passPercent, IReadOnlyDictionary<int, int> chosen, bool discard, CancellationToken cancellationToken)
    {
        var questions = await db.QuizQuestions.AsNoTracking().Where(link => link.QuizId == attempt.QuizId && link.Question.Status == QuestionStatus.Published)
            .Select(link => new
            {
                link.QuestionId, link.Points,
                Options = link.Question.QuestionOptions.Select(option => new { option.Id, option.IsCorrect }).ToList()
            }).ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        int earned = 0, total = 0, correct = 0;
        foreach (var question in questions)
        {
            total += question.Points;
            int? selected = !discard && chosen.TryGetValue(question.QuestionId, out var pick) && question.Options.Any(option => option.Id == pick) ? pick : null;
            var right = selected is { } id && question.Options.First(option => option.Id == id).IsCorrect;
            if (right)
            {
                earned += question.Points;
                correct++;
            }
            db.QuizAttemptAnswers.Add(new QuizAttemptAnswer
            {
                AttemptId = attempt.Id, QuizId = attempt.QuizId, QuestionId = question.QuestionId, SelectedOptionId = selected, IsCorrect = right, AnsweredAt = now
            });
        }
        attempt.SubmittedAt = now < attempt.StartedAt ? attempt.StartedAt : now;
        attempt.CorrectCount = correct;
        attempt.TotalCount = questions.Count;
        attempt.ScorePercent = total == 0 ? 0 : Math.Round(earned * 100m / total, 2);
        attempt.Passed = attempt.ScorePercent >= passPercent;
    }

    // The full breakdown of a submitted attempt. The caller has already checked who may see it.
    public static async Task<QuizResultViewModel?> ResultAsync(SafeLearnDbContext db, long attemptId, CancellationToken cancellationToken)
    {
        var attempt = await db.QuizAttempts.AsNoTracking().Where(item => item.Id == attemptId && item.SubmittedAt != null).Select(item => new
        {
            item.Id, item.QuizId, item.Quiz.Title, item.Quiz.LessonId, Lesson = item.Quiz.Lesson.Title, item.Quiz.PassPercent, item.Quiz.TimeLimitSec, item.Quiz.ShuffleQuestions,
            item.ChildProfileId, Child = item.ChildProfile.DisplayName, item.StartedAt, SubmittedAt = item.SubmittedAt!.Value, item.ScorePercent, item.CorrectCount, item.TotalCount, item.Passed,
            DueDate = item.Assignment != null ? item.Assignment.DueDate : null
        }).FirstOrDefaultAsync(cancellationToken);
        if (attempt is null) return null;
        var answers = await db.QuizAttemptAnswers.AsNoTracking().Where(item => item.AttemptId == attemptId).Select(item => new
        {
            item.QuestionId, item.SelectedOptionId, item.IsCorrect, item.Question.Text, item.Question.Explanation,
            Options = item.Question.QuestionOptions.OrderBy(option => option.SortOrder).ThenBy(option => option.Id).Select(option => new { option.Id, option.Text, option.IsCorrect }).ToList()
        }).ToListAsync(cancellationToken);
        // Same order as the child saw.
        var order = (await QuestionsAsync(db, attempt.QuizId, attempt.Id, attempt.ShuffleQuestions, cancellationToken)).Select(item => item.Id).ToList();
        var sorted = answers.OrderBy(item => order.IndexOf(item.QuestionId) is var index && index >= 0 ? index : int.MaxValue).ToList();
        var submittedDay = DateOnly.FromDateTime(attempt.SubmittedAt.ToLocalTime());
        return new QuizResultViewModel
        {
            AttemptId = attempt.Id,
            QuizId = attempt.QuizId,
            Title = attempt.Title,
            LessonId = attempt.LessonId,
            LessonTitle = attempt.Lesson,
            ChildName = attempt.Child,
            ChildId = attempt.ChildProfileId,
            Score = attempt.ScorePercent ?? 0,
            Correct = attempt.CorrectCount,
            Total = attempt.TotalCount,
            Passed = attempt.Passed == true,
            PassPercent = attempt.PassPercent,
            SubmittedAt = attempt.SubmittedAt,
            DueDate = attempt.DueDate,
            Late = attempt.DueDate is { } due && submittedDay > due,
            TimedOut = attempt.TimeLimitSec is { } limit && (attempt.SubmittedAt - attempt.StartedAt).TotalSeconds > limit + QuizRules.GraceSeconds,
            Questions = sorted.Select((item, index) => new ResultQuestion(index + 1, item.Text, item.Explanation,
                item.Options.Select(option => new ResultOption(option.Text, option.IsCorrect, option.Id == item.SelectedOptionId)).ToList(),
                item.IsCorrect, item.SelectedOptionId is not null)).ToList()
        };
    }

    public static bool Late(DateOnly? due, DateTime? submittedAt) =>
        due is { } day && submittedAt is { } at && DateOnly.FromDateTime(at.ToLocalTime()) > day;
}
