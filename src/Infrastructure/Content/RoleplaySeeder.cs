using System.Text.Json;
using System.Text.Json.Serialization;
using EnglishForIT.Application.Content;
using EnglishForIT.Domain.Entities.Content;
using EnglishForIT.Domain.Enums;
using EnglishForIT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EnglishForIT.Infrastructure.Content;

/// <summary>
/// Nạp kịch bản roleplay từ content/roleplay/*.yaml.
///
/// Dựng lại node là an toàn, khác hẳn bài học: không có bảng nào trỏ khoá ngoại vào
/// <c>roleplay_nodes</c> — lượt chơi lưu đường đi dưới dạng JSON chứ không phải khoá ngoại,
/// chính là để nội dung sửa được mà không mất lịch sử của học viên.
/// </summary>
public class RoleplaySeeder(
    AppDbContext db,
    YamlContentLoader loader,
    RoleplayValidator validator,
    ILogger<RoleplaySeeder> logger)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,

        // camelCase để client đọc thẳng, giống mọi payload khác của API.
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task<SeedReport> SeedAsync(string contentRoot, CancellationToken ct = default)
    {
        var problems = new List<string>();

        var load = loader.LoadRoleplayScenarios(contentRoot);
        problems.AddRange(load.Errors.Select(e => $"Doc kich ban that bai {Path.GetFileName(e.FilePath)}: {e.Message}"));

        var docs = load.Scenarios.Select(s => s.Document).ToList();

        if (docs.Count == 0)
        {
            logger.LogWarning("Không có kịch bản roleplay nào trong {Root}/roleplay", contentRoot);
            return new SeedReport(0, 0, 0, 0, problems);
        }

        var issues = validator.ValidateSet(docs).ToList();

        if (issues.Count > 0)
        {
            problems.AddRange(issues.Select(i => i.ToString()));
            logger.LogError("Kịch bản roleplay không qua cổng chất lượng, huỷ seed. {Count} vấn đề", issues.Count);

            return new SeedReport(0, 0, 0, load.Scenarios.Count, problems);
        }

        // CỐ Ý không Include(Nodes): thêm node qua navigation của một scenario đang tracked
        // sẽ khiến EF đánh dấu Modified thay vì Added (khoá là GUID v7 sinh phía client),
        // sinh UPDATE khớp 0 dòng rồi ném DbUpdateConcurrencyException.
        var existing = await db.RoleplayScenarios
            .ToDictionaryAsync(s => s.Code, StringComparer.OrdinalIgnoreCase, ct);

        int inserted = 0, updated = 0, unchanged = 0;

        foreach (var loaded in load.Scenarios)
        {
            var doc = loaded.Document;

            if (existing.TryGetValue(doc.Code, out var scenario))
            {
                var nodeCount = await db.RoleplayNodes.CountAsync(n => n.ScenarioId == scenario.Id, ct);

                if (scenario.SourceHash == loaded.SourceHash && nodeCount == doc.Nodes.Count)
                {
                    unchanged++;
                    continue;
                }

                Apply(scenario, doc, loaded.SourceHash);

                await db.RoleplayNodes.Where(n => n.ScenarioId == scenario.Id).ExecuteDeleteAsync(ct);
                AddNodes(scenario, doc);

                updated++;
            }
            else
            {
                scenario = new RoleplayScenario
                {
                    Code = doc.Code,
                    TitleVi = doc.TitleVi,
                    ContextVi = doc.ContextVi,
                    PartnerName = doc.PartnerName,
                    StartNodeCode = doc.StartNode,
                    SourceHash = loaded.SourceHash,
                };

                Apply(scenario, doc, loaded.SourceHash);
                db.RoleplayScenarios.Add(scenario);
                AddNodes(scenario, doc);

                inserted++;
            }
        }

        await db.SaveChangesAsync(ct);

        var report = new SeedReport(inserted, updated, unchanged, 0, problems);
        logger.LogInformation("Seed roleplay xong: {Report}", report);

        return report;
    }

    private static void Apply(RoleplayScenario scenario, RoleplayDocument doc, string hash)
    {
        scenario.Code = doc.Code;
        scenario.TitleVi = doc.TitleVi;
        scenario.ContextVi = doc.ContextVi;
        scenario.Track = doc.Track;
        scenario.Level = doc.Level;
        scenario.PartnerName = doc.PartnerName;
        scenario.StartNodeCode = doc.StartNode;
        scenario.SourceHash = hash;
        scenario.Status = ContentStatus.Published;
    }

    /// <summary>
    /// Thêm node thẳng vào DbSet chứ không qua <c>scenario.Nodes</c>.
    /// Cùng lý do với seeder đề xếp lớp: khoá GUID v7 sinh phía client làm EF đánh dấu
    /// Modified thay vì Added khi thêm qua navigation của entity đang tracked.
    /// </summary>
    private void AddNodes(RoleplayScenario scenario, RoleplayDocument doc)
    {
        foreach (var node in doc.Nodes)
        {
            db.RoleplayNodes.Add(new RoleplayNode
            {
                ScenarioId = scenario.Id,
                Code = node.Code,
                PartnerLineEn = node.PartnerLineEn,
                PartnerLineVi = node.PartnerLineVi,
                IsTerminal = node.Terminal,
                SummaryVi = node.SummaryVi,
                ChoicesJson = JsonSerializer.Serialize(
                    node.Choices.Select(c => new
                    {
                        en = c.En,
                        vi = c.Vi,
                        next = c.Next,
                        quality = c.Quality.ToLowerInvariant(),
                        feedbackVi = c.FeedbackVi,
                    }),
                    Json),

                IsSuccessEnding = node.Success,
            });
        }
    }
}
