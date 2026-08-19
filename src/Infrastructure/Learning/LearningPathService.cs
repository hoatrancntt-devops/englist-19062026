using EnglishForIT.Application.Learning;
using EnglishForIT.Domain.Enums;
using EnglishForIT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EnglishForIT.Infrastructure.Learning;

public record LessonCard(
    string Code,
    string TitleVi,
    string Track,
    string Layer,
    string Level,
    string UnitCode,
    int EstimatedMinutes,
    bool IsCheckpoint,
    string? Illustration,
    string State,
    double Mastery,
    IReadOnlyList<string> SupportedSkills,
    /// <summary>Câu giải thích vì sao đang khoá, có con số cụ thể. Rỗng khi bài đang mở.</summary>
    string LockExplanationVi,
    /// <summary>
    /// Bài này qua bằng thi vượt chứ chưa học. Giao diện phải hiện khác bài học thật: nếu hiện
    /// giống nhau, học viên đọc "đã thạo" ở đây và "bạn mới thi vượt chứ chưa học" ở bài sau,
    /// hai câu chọi nhau và người ta kết luận hệ thống hỏng.
    /// </summary>
    bool UnlockedByChallenge = false);

public record NextLessonResult(LessonCard Card, string ReasonVi);

public record RoadmapResult(
    IReadOnlyList<LessonCard> Lessons,
    NextLessonResult? Next,
    int TotalPublished,
    int Mastered,
    int InProgress);

/// <summary>
/// Ghép dữ liệu từ DB vào engine thuần rồi trả kết quả cho tầng API.
///
/// Engine không biết gì về EF; service này là chỗ duy nhất dịch giữa hai thế giới.
/// </summary>
public class LearningPathService(AppDbContext db, IOptions<LearningPolicyOptions> policyOptions)
{
    private readonly PrerequisiteEngine _engine = new(policyOptions.Value);

    public async Task<RoadmapResult> GetRoadmapAsync(Guid userId, DateTimeOffset now, CancellationToken ct = default)
    {
        var loaded = await db.Lessons
            .AsNoTracking()
            .Where(l => l.Status == ContentStatus.Published)
            .Include(l => l.Prerequisites)
            .ToListAsync(ct);

        // Sắp xếp trong bộ nhớ, KHÔNG sắp trong truy vấn.
        //
        // Cột level lưu dạng chuỗi (để đọc dữ liệu thô không phải tra bảng số), nên
        // ORDER BY của Postgres sắp theo bảng chữ cái: "A1" < "A2" < "PreA1".
        // Kết quả là bậc vỡ lòng bị đẩy xuống cuối lộ trình. Sắp theo giá trị enum
        // mới ra đúng thứ tự sư phạm.
        // Sắp thêm theo Track trước OrderIndex: hai nhánh cùng tầng và cùng bậc
        // sẽ xen kẽ nhau nếu chỉ sắp theo OrderIndex (HD-03, INF-03, HD-04, INF-04...),
        // và học viên không nhận ra mình đang đi nhánh nào.
        var lessons = loaded
            .OrderBy(l => l.Layer)
            .ThenBy(l => l.Track)
            .ThenBy(l => l.Level)
            .ThenBy(l => l.OrderIndex)
            .ToList();

        if (lessons.Count == 0)
        {
            return new RoadmapResult([], null, 0, 0, 0);
        }

        // Cần bảng tra id sang code vì cạnh tiên quyết lưu theo id còn engine làm việc theo code.
        //
        // Tra trên MỌI bài chứ không riêng bài đã xuất bản. Bản trước lọc theo tập published rồi
        // lặng lẽ bỏ cạnh nào trỏ ra ngoài tập đó, và một bài tiên quyết bị hạ về nháp làm CỔNG
        // BIẾN MẤT thay vì khoá bài lại: OFF-04 nằm ở bậc 18 trở thành Available cho học viên
        // chưa học gì. Giữ cạnh lại thì engine tra mastery không thấy, coi như 0, và bài khoá —
        // hỏng theo chiều nhìn thấy được, sửa bằng cách xuất bản lại bài kia.
        //
        // Bài đã xoá mềm vẫn rơi khỏi bảng này qua query filter, và đúng như vậy: nội dung đã xoá
        // thì cạnh trỏ tới nó là rác, không phải cổng.
        var codeById = await db.Lessons
            .AsNoTracking()
            .Select(l => new { l.Id, l.Code })
            .ToDictionaryAsync(l => l.Id, l => l.Code, ct);

        var nodes = lessons
            .Select(l => new LessonNode(
                l.Id,
                l.Code,
                l.Level,
                l.Layer,
                l.Track,
                l.OrderIndex,
                l.IsCheckpoint,
                l.SupportedSkills,
                [.. l.Prerequisites
                    .Where(p => codeById.ContainsKey(p.RequiredLessonId))
                    .Select(p => new PrerequisiteEdge(codeById[p.RequiredLessonId], p.MinMastery, p.Kind))]))
            .ToList();

        var masteries = await db.LessonMasteries
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .ToListAsync(ct);

        var progressByCode = masteries
            .Where(m => codeById.ContainsKey(m.LessonId))
            .ToDictionary(
                m => codeById[m.LessonId],
                m => new MasterySnapshot(
                    m.LessonId,
                    codeById[m.LessonId],
                    m.State,
                    m.MasteryEffective,
                    m.SkillScores,
                    m.MasteredAt,
                    m.UnlockedByChallenge),
                StringComparer.OrdinalIgnoreCase);

        var challengePassed = await db.ChallengePasses
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .Select(c => c.LessonId)
            .ToListAsync(ct);

        var challengeCodes = challengePassed
            .Where(codeById.ContainsKey)
            .Select(id => codeById[id])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var evaluations = nodes.ToDictionary(
            n => n.Code,
            n => _engine.Evaluate(n, progressByCode, challengeCodes, now),
            StringComparer.OrdinalIgnoreCase);

        var profile = await db.UserProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId, ct);

        var choice = _engine.ChooseNext(
            nodes,
            evaluations,
            progressByCode,
            profile?.CurrentLayer ?? ContextLayer.Life,
            profile?.CurrentLevel ?? CefrLevel.PreA1,
            profile?.PreferAllTracks == true ? null : profile?.PrimaryTrack ?? LearningTrack.Foundation);

        var cards = lessons
            .Select(l => ToCard(l, evaluations[l.Code], progressByCode.GetValueOrDefault(l.Code)))
            .ToList();

        var cardByCode = cards.ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);

        var next = choice is null
            ? null
            : new NextLessonResult(cardByCode[choice.Lesson.Code], choice.ReasonVi);

        return new RoadmapResult(
            cards,
            next,
            lessons.Count,

            // Bài mới đánh dấu biết KHÔNG tính vào số "đã thạo". Màn hình ghi rõ chữ "đã thạo"
            // cạnh con số này, mà thi vượt thì cố ý không có nghĩa là đã thạo — đếm vào đây là
            // tự phá lại đúng điều vừa siết. Bài được ghi công từ xếp lớp vẫn tính, vì đó là
            // một bài đo trình độ thật.
            cards.Count(c => c.State == nameof(LessonState.Mastered) && !c.UnlockedByChallenge),

            cards.Count(c => c.State == nameof(LessonState.InProgress)));
    }

    private LessonCard ToCard(
        Domain.Entities.Content.Lesson lesson,
        LessonEvaluation evaluation,
        MasterySnapshot? snapshot)
    {
        return new LessonCard(
            lesson.Code,
            lesson.TitleVi,
            lesson.Track.ToString(),
            lesson.Layer.ToString(),
            lesson.Level.ToString(),
            lesson.UnitCode ?? string.Empty,
            lesson.EstimatedMinutes,
            lesson.IsCheckpoint,
            lesson.Illustration,
            evaluation.State.ToString(),
            Math.Round(snapshot?.MasteryEffective ?? 0, 1),
            [.. lesson.SupportedSkills.Select(s => s.ToString())],
            evaluation.IsOpen ? string.Empty : _engine.ExplainLock(evaluation),
            snapshot?.UnlockedByChallenge ?? false);
    }
}
