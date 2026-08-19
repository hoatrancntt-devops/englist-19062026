using System.Text.Json;
using EnglishForIT.Domain.Entities.Progress;
using EnglishForIT.Domain.Enums;
using EnglishForIT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EnglishForIT.Infrastructure.Learning;

/// <summary>Một lựa chọn hiện cho học viên. KHÔNG kèm quality — đó là đáp án.</summary>
public record RoleplayChoiceView(int Index, string En, string Vi);

public record RoleplayTurn(
    string NodeCode,
    string PartnerLineEn,
    string PartnerLineVi,
    IReadOnlyList<RoleplayChoiceView> Choices,
    bool IsTerminal,
    string? SummaryVi,
    /// <summary>Chỉ có giá trị ở node kết thúc.</summary>
    bool? Success);

public record RoleplaySummary(
    string Code,
    string TitleVi,
    string ContextVi,
    string PartnerName,
    string Track,
    string Level,
    int TurnCount,
    /// <summary>Kết quả lần chơi gần nhất, null nếu chưa chơi lần nào.</summary>
    string? LastOutcome,
    double? LastScore);

public record RoleplayStart(Guid AttemptId, RoleplaySummary Scenario, RoleplayTurn Turn);

public record RoleplayAnswerResult(
    /// <summary>Nhận xét về lựa chọn vừa rồi. Rỗng khi lựa chọn đạt.</summary>
    string? FeedbackVi,
    string Quality,
    RoleplayTurn Next,
    /// <summary>Có giá trị khi vừa tới node kết thúc.</summary>
    RoleplayResult? Result);

public record RoleplayResult(
    string Outcome,
    double Score,
    int GoodChoices,
    int CurtChoices,
    int WrongChoices,
    string MessageVi);

/// <summary>
/// Chạy một lượt roleplay.
///
/// Khác mọi phần chấm khác trong hệ thống: không có đúng và sai, có ba mức. "curt" là câu
/// đúng ngữ pháp nhưng cộc lốc — lỗi phổ biến nhất của kỹ sư Việt Nam nói tiếng Anh với
/// đối tác, và là lý do chính khiến phần này tồn tại. Chấm nhị phân sẽ xoá mất đúng thứ
/// cần dạy.
///
/// Chất lượng lựa chọn KHÔNG bao giờ rời máy chủ trước khi học viên chọn. Thấy trước nhãn
/// "good" thì bài này thành trò bấm nhãn.
/// </summary>
public class RoleplayService(AppDbContext db, ILogger<RoleplayService> logger)
{
    /// <summary>Điểm mỗi mức. Cộc lốc vẫn được nửa điểm vì học viên đã hoàn thành việc.</summary>
    private const double GoodPoints = 1.0;
    private const double CurtPoints = 0.5;

    public async Task<IReadOnlyList<RoleplaySummary>> ListAsync(
        Guid userId, CancellationToken ct = default)
    {
        var scenarios = await db.RoleplayScenarios
            .AsNoTracking()
            .Where(s => s.Status == ContentStatus.Published)
            .OrderBy(s => s.Code)
            .ToListAsync(ct);

        var nodeCounts = await db.RoleplayNodes
            .GroupBy(n => n.ScenarioId)
            .Select(g => new { ScenarioId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ScenarioId, x => x.Count, ct);

        var lastAttempts = await db.RoleplayAttempts
            .AsNoTracking()
            .Where(a => a.UserId == userId && a.CompletedAt != null)
            .GroupBy(a => a.ScenarioId)
            .Select(g => g.OrderByDescending(a => a.CompletedAt).First())
            .ToListAsync(ct);

        var byScenario = lastAttempts.ToDictionary(a => a.ScenarioId);

        return [.. scenarios.Select(s => new RoleplaySummary(
            s.Code,
            s.TitleVi,
            s.ContextVi,
            s.PartnerName,
            s.Track.ToString(),
            s.Level.ToString(),
            nodeCounts.GetValueOrDefault(s.Id),
            byScenario.TryGetValue(s.Id, out var attempt) ? attempt.Outcome.ToString() : null,
            byScenario.TryGetValue(s.Id, out var scored) ? scored.Score : null))];
    }

    /// <summary>Bắt đầu một lượt mới. Lượt đang dở của cùng kịch bản bị bỏ, không nối tiếp.</summary>
    public async Task<RoleplayStart?> StartAsync(Guid userId, string code, DateTimeOffset now, CancellationToken ct = default)
    {
        var scenario = await db.RoleplayScenarios
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Code == code && s.Status == ContentStatus.Published, ct);

        if (scenario is null)
        {
            return null;
        }

        var start = await db.RoleplayNodes
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.ScenarioId == scenario.Id && n.Code == scenario.StartNodeCode, ct);

        if (start is null)
        {
            // Cổng chất lượng đã chặn trường hợp này lúc seed, nên tới đây nghĩa là DB
            // bị sửa tay. Báo lỗi thay vì hiện màn hình trống.
            logger.LogError("Kịch bản {Code} không có node bắt đầu {Node}", code, scenario.StartNodeCode);
            return null;
        }

        // Lượt dở cũ bị huỷ: hội thoại nối lại sau ba ngày thì học viên đã quên ngữ cảnh,
        // và giá trị của bài nằm ở chỗ nói liền mạch.
        var stale = await db.RoleplayAttempts
            .Where(a => a.UserId == userId && a.ScenarioId == scenario.Id && a.CompletedAt == null)
            .ToListAsync(ct);

        db.RoleplayAttempts.RemoveRange(stale);

        var attempt = new RoleplayAttempt
        {
            UserId = userId,
            ScenarioId = scenario.Id,
            StartedAt = now,
            PathJson = "[]",
        };

        db.RoleplayAttempts.Add(attempt);
        await db.SaveChangesAsync(ct);

        var summaries = await ListAsync(userId, ct);
        var summary = summaries.First(s => s.Code == code);

        return new RoleplayStart(attempt.Id, summary, ToTurn(start));
    }

    /// <summary>Chọn một lựa chọn và đi tới lượt kế.</summary>
    public async Task<RoleplayAnswerResult?> ChooseAsync(
        Guid userId, Guid attemptId, string nodeCode, int choiceIndex, DateTimeOffset now, CancellationToken ct = default)
    {
        var attempt = await db.RoleplayAttempts
            .FirstOrDefaultAsync(a => a.Id == attemptId && a.UserId == userId && a.CompletedAt == null, ct);

        if (attempt is null)
        {
            return null;
        }

        var node = await db.RoleplayNodes
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.ScenarioId == attempt.ScenarioId && n.Code == nodeCode, ct);

        if (node is null)
        {
            return null;
        }

        var choices = ParseChoices(node.ChoicesJson);

        if (choiceIndex < 0 || choiceIndex >= choices.Count)
        {
            return null;
        }

        var chosen = choices[choiceIndex];

        var path = JsonSerializer.Deserialize<List<PathStep>>(attempt.PathJson) ?? [];
        path.Add(new PathStep(nodeCode, choiceIndex, chosen.Quality));
        attempt.PathJson = JsonSerializer.Serialize(path);

        var nextNode = string.IsNullOrWhiteSpace(chosen.Next)
            ? null
            : await db.RoleplayNodes
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.ScenarioId == attempt.ScenarioId && n.Code == chosen.Next, ct);

        RoleplayResult? result = null;

        if (nextNode is null || nextNode.IsTerminal)
        {
            result = Complete(attempt, path, nextNode?.IsSuccessEnding ?? false, now);
        }

        await db.SaveChangesAsync(ct);

        // Node kế null nghĩa là lựa chọn kết thúc ngay tại chỗ — dựng một lượt kết thúc
        // rỗng để client luôn có thứ để hiển thị.
        var turn = nextNode is not null
            ? ToTurn(nextNode)
            : new RoleplayTurn(nodeCode, node.PartnerLineEn, node.PartnerLineVi, [], true, node.SummaryVi, false);

        return new RoleplayAnswerResult(
            string.Equals(chosen.Quality, "good", StringComparison.OrdinalIgnoreCase) ? null : chosen.FeedbackVi,
            chosen.Quality,
            turn,
            result);
    }

    /// <summary>
    /// Chốt lượt chơi.
    ///
    /// Điểm là tỷ lệ phần trăm trên tổng điểm tối đa của chính đường đi đó, không phải trên
    /// một con số cố định: hai đường đi có số lượt khác nhau vẫn so sánh được với nhau.
    /// </summary>
    private static RoleplayResult Complete(
        RoleplayAttempt attempt, List<PathStep> path, bool successEnding, DateTimeOffset now)
    {
        var good = path.Count(p => p.Quality.Equals("good", StringComparison.OrdinalIgnoreCase));
        var curt = path.Count(p => p.Quality.Equals("curt", StringComparison.OrdinalIgnoreCase));
        var wrong = path.Count - good - curt;

        var earned = good * GoodPoints + curt * CurtPoints;
        var score = path.Count == 0 ? 0 : Math.Round(earned * 100.0 / path.Count, 1);

        attempt.CompletedAt = now;
        attempt.Score = score;
        attempt.Outcome = successEnding
            ? curt > 0 || wrong > 0 ? RoleplayOutcome.CompletedWithHints : RoleplayOutcome.Completed
            : RoleplayOutcome.Incomplete;

        return new RoleplayResult(
            attempt.Outcome.ToString(),
            score,
            good,
            curt,
            wrong,
            BuildMessage(successEnding, good, curt, wrong));
    }

    private static string BuildMessage(bool success, int good, int curt, int wrong)
    {
        if (!success)
        {
            return wrong > 0
                ? $"Cuộc hội thoại dừng sớm vì {wrong} lựa chọn đi sai hướng. Đọc phần tổng kết rồi thử lại."
                : "Cuộc hội thoại dừng trước khi xong việc. Đọc phần tổng kết rồi thử lại.";
        }

        if (curt == 0 && wrong == 0)
        {
            return $"Xong việc với cả {good} lượt đều đạt. Đây là cách nói mà đối tác muốn làm việc cùng.";
        }

        if (curt > 0 && wrong == 0)
        {
            return $"Xong việc, nhưng {curt} lượt còn cộc lốc. Đúng ngữ pháp mà cụt vẫn làm người nghe thấy bị coi nhẹ.";
        }

        return $"Xong việc, nhưng còn {wrong} lượt đi sai hướng và {curt} lượt cộc lốc.";
    }

    private static RoleplayTurn ToTurn(Domain.Entities.Content.RoleplayNode node)
    {
        var choices = ParseChoices(node.ChoicesJson);

        return new RoleplayTurn(
            node.Code,
            node.PartnerLineEn,
            node.PartnerLineVi,
            // CHỈ en và vi. quality và feedbackVi ở lại máy chủ cho tới khi học viên chọn.
            [.. choices.Select((c, i) => new RoleplayChoiceView(i, c.En, c.Vi))],
            node.IsTerminal,
            node.SummaryVi,
            node.IsTerminal ? node.IsSuccessEnding : null);
    }

    private static List<ChoiceData> ParseChoices(string json) =>
        JsonSerializer.Deserialize<List<ChoiceData>>(json, JsonOptions) ?? [];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private record ChoiceData(string En, string Vi, string? Next, string Quality, string? FeedbackVi);

    private record PathStep(string Node, int Choice, string Quality);
}
