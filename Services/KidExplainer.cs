using System.Text.Json;
using Anthropic.Exceptions;
using Anthropic.Models.Beta.Messages;
using Microsoft.Extensions.Options;

namespace SafeLearnKids.Web.Services;

public record ExplainSource(string Question, IReadOnlyList<string> Options, int Correct, string? Explanation, string Lesson, string Subject, int Grade);

public record ExplainDraft(string? Text, string Model, string? Error);

// "Explain it another way" for a question a child got wrong. Only a draft: an admin reads, edits and saves it on the
// question form, and children see only the saved text (no AI text reaches a child without review, as for US-40).
// With Ai:ApiKey the draft comes from Claude; without one a plain template restates the answer and the explanation.
public sealed class KidExplainer(IOptions<AiOptions> options, ILogger<KidExplainer> logger)
{
    public const string OfflineModel = "offline-template-v1";
    public const int MaxLength = 1000;

    public bool UsesAi => options.Value.Configured;

    public async Task<ExplainDraft> DraftAsync(ExplainSource source, CancellationToken cancellationToken)
    {
        if (source.Correct < 0 || source.Correct >= source.Options.Count) return new(null, OfflineModel, "Câu hỏi chưa có đáp án đúng.");
        if (!UsesAi) return new(Template(source), OfflineModel, null);
        var model = options.Value.Model;
        try
        {
            var text = await AskClaudeAsync(source, model, cancellationToken);
            return text is null ? new(null, model, "AI chưa soạn được lời giải thích. Hãy thử lại hoặc tự viết.") : new(text, model, null);
        }
        catch (AnthropicRateLimitException)
        {
            return new(null, model, "AI đang bận, vui lòng thử lại sau ít phút.");
        }
        catch (AnthropicApiException error)
        {
            logger.LogWarning("Kid explanation failed: {Message}", error.Message);
            return new(null, model, "Chưa gọi được dịch vụ AI. Kiểm tra cấu hình Ai:ApiKey hoặc thử lại.");
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning("Kid explanation failed: {Type}", error.GetType().Name);
            return new(null, model, "Chưa gọi được dịch vụ AI. Vui lòng thử lại.");
        }
    }

    private async Task<string?> AskClaudeAsync(ExplainSource source, string model, CancellationToken cancellationToken)
    {
        var prompt = $"""
            Bài học: {source.Lesson} · Môn {source.Subject} · Lớp {source.Grade}
            Câu hỏi: {source.Question}
            Các lựa chọn:
            {string.Join("\n", source.Options.Select((option, index) => $"{(char)('A' + index)}. {option}"))}
            Đáp án đúng: {(char)('A' + source.Correct)}. {source.Options[source.Correct]}
            Lời giải thích hiện có: {source.Explanation ?? "(chưa có)"}
            """;
        var schema = new Dictionary<string, JsonElement>
        {
            ["type"] = JsonSerializer.SerializeToElement("object"),
            ["additionalProperties"] = JsonSerializer.SerializeToElement(false),
            ["required"] = JsonSerializer.SerializeToElement(new[] { "explanation" }),
            ["properties"] = JsonSerializer.SerializeToElement(new { explanation = new { type = "string" } })
        };
        var response = await options.Value.CreateClient().Beta.Messages.Create(new MessageCreateParams
        {
            Model = model,
            MaxTokens = 16000,
            Betas = ["server-side-fallback-2026-07-01"],
            Fallbacks = new Default(),
            OutputConfig = new BetaOutputConfig { Effort = Effort.Medium, Format = new BetaJsonOutputFormat { Schema = schema } },
            System = """
                Bạn là cô giáo tiểu học. Một bé vừa trả lời sai câu hỏi dưới đây. Hãy viết một lời giải thích KHÁC với lời giải thích
                hiện có, theo cách bé ở đúng lớp đó dễ hiểu: dùng ví dụ đời thường quen thuộc (đồ chơi, hoa quả, ngón tay...) hoặc
                chia thành 2–3 bước nhỏ, rồi chỉ ra vì sao đáp án đúng là đúng.
                Viết tiếng Việt, xưng "con", giọng ấm áp, động viên, không chê bé. Dưới 500 ký tự, không dùng tiêu đề hay ký hiệu markdown.
                Chỉ dựa vào câu hỏi và đáp án đã cho, không thêm kiến thức ngoài lề. Không nhắc đến AI, không đưa đường link hay thông tin cá nhân.
                """,
            Messages = [new BetaUserMessageParam { Content = prompt }]
        }, cancellationToken);
        if (response.StopReason == "refusal" || response.StopReason == "max_tokens") return null;
        var json = string.Concat(response.Content.Select(block => block.TryPickText(out var text) ? text.Text : ""));
        using var document = JsonDocument.Parse(json);
        var explanation = document.RootElement.TryGetProperty("explanation", out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()?.Trim() : null;
        if (string.IsNullOrEmpty(explanation)) return null;
        // Plain text only, cut to the column size; the admin still reads it before saving.
        explanation = explanation.Replace("**", "").Replace("##", "").Trim();
        return explanation.Length <= MaxLength ? explanation : explanation[..MaxLength];
    }

    private static string Template(ExplainSource source)
    {
        var text = $"Mình cùng đọc chậm lại câu hỏi nhé: \"{source.Question}\". Đáp án đúng là \"{source.Options[source.Correct]}\"."
            + (string.IsNullOrWhiteSpace(source.Explanation) ? "" : " " + source.Explanation.Trim())
            + $" Con mở lại bài \"{source.Lesson}\", tìm chỗ nói về điều này rồi thử làm lại câu hỏi nha!";
        return text.Length <= MaxLength ? text : text[..MaxLength];
    }
}
