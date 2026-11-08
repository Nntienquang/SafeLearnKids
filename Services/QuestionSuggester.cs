using System.Text.Json;
using System.Text.RegularExpressions;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Beta.Messages;
using Microsoft.Extensions.Options;
using SafeLearnKids.Web.Models;

namespace SafeLearnKids.Web.Services;

// Section "Ai" in configuration. Keep ApiKey out of appsettings.json: use user-secrets or an environment variable (Ai__ApiKey).
public class AiOptions
{
    public string? ApiKey { get; set; }
    public string Model { get; set; } = "claude-opus-5-5";
}

public record LessonSource(string Title, string Subject, int Grade, string? Summary, IReadOnlyList<string> Readings, IReadOnlyList<string> Existing);

// US-40: suggested questions always start "InReview"; an admin must approve them before any quiz can use them.
// With an API key the suggestions come from Claude; without one a simple offline generator builds them from the lesson text.
public partial class QuestionSuggester(IOptions<AiOptions> options, ILogger<QuestionSuggester> logger)
{
    public const string OfflineModel = "offline-rule-v1";
    public const int MaxCount = 5;
    private const int MaxSourceChars = 12000;

    public bool Configured => !string.IsNullOrWhiteSpace(options.Value.ApiKey);

    public async Task<SuggestionResult> SuggestAsync(LessonSource lesson, int count, CancellationToken cancellationToken)
    {
        count = Math.Clamp(count, 1, MaxCount);
        if (!Configured) return Offline(lesson, count);
        var model = options.Value.Model;
        try
        {
            var raw = await AskClaudeAsync(lesson, count, model, cancellationToken);
            if (raw is null) return new SuggestionResult([], model, "AI không tạo câu hỏi cho bài này. Hãy thử lại hoặc soạn tay.");
            var questions = Parse(raw, lesson.Existing).Take(count).ToList();
            return new SuggestionResult(questions, model, questions.Count == 0 ? "AI chưa tạo được câu hỏi hợp lệ. Hãy thử lại." : null);
        }
        catch (AnthropicRateLimitException)
        {
            return new SuggestionResult([], model, "AI đang bận, vui lòng thử lại sau ít phút.");
        }
        catch (AnthropicApiException error)
        {
            // The message never contains the key; log it for the developers and show a plain notice.
            logger.LogWarning("Question suggestion failed: {Message}", error.Message);
            return new SuggestionResult([], model, "Chưa gọi được dịch vụ AI. Kiểm tra cấu hình Ai:ApiKey hoặc thử lại.");
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning("Question suggestion failed: {Type}", error.GetType().Name);
            return new SuggestionResult([], model, "Chưa gọi được dịch vụ AI. Vui lòng thử lại.");
        }
    }

    private async Task<string?> AskClaudeAsync(LessonSource lesson, int count, string model, CancellationToken cancellationToken)
    {
        AnthropicClient client = new() { ApiKey = options.Value.ApiKey };
        var source = string.Join("\n\n", lesson.Readings);
        if (source.Length > MaxSourceChars) source = source[..MaxSourceChars];
        var prompt = $"""
            Bài học: {lesson.Title}
            Môn: {lesson.Subject} · Lớp {lesson.Grade}
            Mục tiêu: {lesson.Summary ?? "(không có)"}

            Nội dung bài:
            {(source.Length == 0 ? "(bài chỉ có video hoặc tài liệu, hãy dựa vào tên và mục tiêu bài)" : source)}

            Câu hỏi đã có (không lặp lại):
            {(lesson.Existing.Count == 0 ? "(chưa có)" : string.Join("\n", lesson.Existing.Take(50).Select(text => "- " + text)))}

            Hãy soạn {count} câu hỏi kiểm tra cho bài này.
            """;
        var schema = new Dictionary<string, JsonElement>
        {
            ["type"] = JsonSerializer.SerializeToElement("object"),
            ["additionalProperties"] = JsonSerializer.SerializeToElement(false),
            ["required"] = JsonSerializer.SerializeToElement(new[] { "questions" }),
            ["properties"] = JsonSerializer.SerializeToElement(new
            {
                questions = new
                {
                    type = "array",
                    items = new
                    {
                        type = "object",
                        additionalProperties = false,
                        required = new[] { "text", "type", "difficulty", "explanation", "options", "correct" },
                        properties = new
                        {
                            text = new { type = "string" },
                            type = new { type = "string", @enum = QuestionType.All },
                            difficulty = new { type = "integer" },
                            explanation = new { type = "string" },
                            options = new { type = "array", items = new { type = "string" } },
                            correct = new { type = "integer" }
                        }
                    }
                }
            })
        };
        var response = await client.Beta.Messages.Create(new MessageCreateParams
        {
            Model = model,
            MaxTokens = 16000,
            // A refusal is re-served by a fallback model inside the same call.
            Betas = ["server-side-fallback-2026-07-01"],
            Fallbacks = new Anthropic.Models.Beta.Messages.Default(),
            OutputConfig = new BetaOutputConfig { Effort = Effort.Medium, Format = new BetaJsonOutputFormat { Schema = schema } },
            System = """
                Bạn soạn câu hỏi kiểm tra bằng tiếng Việt cho học sinh tiểu học, dựa đúng vào nội dung bài được cung cấp.
                Mỗi câu ngắn gọn, rõ ràng, phù hợp lứa tuổi, chỉ có một đáp án đúng và không đánh đố.
                Loại "SingleChoice" có 3 hoặc 4 lựa chọn; loại "TrueFalse" có đúng 2 lựa chọn "Đúng" và "Sai".
                "correct" là vị trí (bắt đầu từ 0) của đáp án đúng trong "options". "difficulty" từ 1 (dễ) đến 3 (khó).
                "explanation" giải thích ngắn (dưới 300 ký tự) vì sao đáp án đúng, viết thân thiện với trẻ.
                Không đưa thông tin cá nhân, nội dung bạo lực, đáng sợ hay quảng cáo.
                """,
            Messages = [new BetaUserMessageParam { Content = prompt }]
        }, cancellationToken);
        if (response.StopReason == "refusal" || response.StopReason == "max_tokens") return null;
        return string.Concat(response.Content.Select(block => block.TryPickText(out var text) ? text.Text : ""));
    }

    // Never trust the model blindly: lengths, option counts and the correct index are checked like a hand-written question.
    private static IEnumerable<SuggestedQuestion> Parse(string json, IReadOnlyList<string> existing)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("questions", out var list) || list.ValueKind != JsonValueKind.Array) yield break;
        var seen = new HashSet<string>(existing.Select(Normalize));
        foreach (var item in list.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            var text = Str(item, "text");
            var type = Str(item, "type");
            var explanation = Str(item, "explanation");
            var difficulty = item.TryGetProperty("difficulty", out var level) && level.TryGetInt32(out var value) ? Math.Clamp(value, 1, 3) : 1;
            var correct = item.TryGetProperty("correct", out var pick) && pick.TryGetInt32(out var index) ? index : -1;
            var answers = item.TryGetProperty("options", out var array) && array.ValueKind == JsonValueKind.Array
                ? array.EnumerateArray().Select(option => option.ValueKind == JsonValueKind.String ? option.GetString()!.Trim() : "").ToList() : [];
            if (type == QuestionType.TrueFalse) answers = ["Đúng", "Sai"];
            if (text is not { Length: > 0 and <= 500 } || !QuestionType.All.Contains(type)
                || answers.Count is < QuizRules.MinOptions or > QuizRules.MaxOptions || answers.Any(option => option.Length is 0 or > 300)
                || answers.Distinct(StringComparer.OrdinalIgnoreCase).Count() != answers.Count
                || correct < 0 || correct >= answers.Count || !seen.Add(Normalize(text))) continue;
            if (explanation is { Length: > 500 }) explanation = explanation[..500];
            yield return new SuggestedQuestion(text, type!, difficulty, string.IsNullOrEmpty(explanation) ? null : explanation, answers, correct);
        }
    }

    private static string? Str(JsonElement item, string name) =>
        item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()?.Trim() : null;

    private static string Normalize(string text) => Spaces().Replace(text.Trim().ToLowerInvariant(), " ");

    // Offline: fill-in-the-blank questions from sentences of the lesson text, plus true/false with a changed number.
    private static SuggestionResult Offline(LessonSource lesson, int count)
    {
        var sentences = lesson.Readings.Append(lesson.Summary ?? "")
            .SelectMany(text => SentenceEnd().Split(text))
            .Select(sentence => Spaces().Replace(sentence, " ").Trim())
            .Where(sentence => sentence.Split(' ').Length is >= 5 and <= 30 && sentence.Length <= 300)
            .Distinct().ToList();
        var words = sentences.SelectMany(sentence => Words().Matches(sentence).Select(match => match.Value))
            .Where(word => word.Length >= 4 && !char.IsDigit(word[0])).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var seen = new HashSet<string>(lesson.Existing.Select(Normalize));
        var result = new List<SuggestedQuestion>();
        var random = new Random(lesson.Title.Length * 31 + sentences.Count);
        foreach (var sentence in sentences)
        {
            if (result.Count >= count) break;
            var number = Number().Match(sentence);
            if (number.Success && int.TryParse(number.Value, out var n) && n < 100000)
            {
                var wrong = n + (random.Next(2) == 0 ? 1 : 2);
                var statement = sentence[..number.Index] + wrong + sentence[(number.Index + number.Length)..];
                Add(result, seen, $"Đúng hay sai: \"{statement}\"", QuestionType.TrueFalse, 2, $"Sai. Trong bài: \"{sentence}\"", ["Đúng", "Sai"], 1);
                if (result.Count >= count) break;
                var options = new[] { n, n + 1, Math.Max(0, n - 1) == n ? n + 2 : n - 1, n + 3 }.Distinct().Take(4).Select(value => value.ToString()).ToList();
                Shuffle(options, random);
                Add(result, seen, "Điền số còn thiếu: " + sentence[..number.Index] + "___" + sentence[(number.Index + number.Length)..],
                    QuestionType.SingleChoice, 1, $"Trong bài: \"{sentence}\"", options, options.IndexOf(n.ToString()));
                continue;
            }
            var candidates = Words().Matches(sentence).Select(match => match.Value).Where(word => word.Length >= 4 && !char.IsDigit(word[0])).ToList();
            if (candidates.Count == 0) continue;
            var answer = candidates.OrderByDescending(word => word.Length).First();
            var distractors = words.Where(word => !word.Equals(answer, StringComparison.OrdinalIgnoreCase)).OrderBy(_ => random.Next()).Take(3).ToList();
            if (distractors.Count < 2) continue;
            var picks = distractors.Append(answer).ToList();
            Shuffle(picks, random);
            var index = sentence.IndexOf(answer, StringComparison.Ordinal);
            Add(result, seen, "Điền từ còn thiếu: " + sentence[..index] + "___" + sentence[(index + answer.Length)..],
                QuestionType.SingleChoice, 1, $"Trong bài: \"{sentence}\"", picks, picks.IndexOf(answer));
        }
        return new SuggestionResult(result, OfflineModel,
            result.Count == 0 ? "Bài chưa có đủ nội dung chữ để tự gợi ý câu hỏi. Hãy soạn tay hoặc cấu hình Ai:ApiKey." : null);
    }

    private static void Add(List<SuggestedQuestion> result, HashSet<string> seen, string text, string type, int difficulty, string explanation, List<string> options, int correct)
    {
        if (text.Length > 500 || options.Count < QuizRules.MinOptions || correct < 0 || !seen.Add(Normalize(text))) return;
        result.Add(new SuggestedQuestion(text, type, difficulty, explanation.Length > 500 ? explanation[..500] : explanation, options, correct));
    }

    private static void Shuffle(List<string> items, Random random)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }

    [GeneratedRegex(@"(?<=[.!?…])\s+|\r?\n+")]
    private static partial Regex SentenceEnd();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"\p{L}[\p{L}\p{Mn}]*|\d+")]
    private static partial Regex Words();

    [GeneratedRegex(@"\b\d+\b")]
    private static partial Regex Number();
}
