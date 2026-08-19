using System.Security.Cryptography;
using System.Text;
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
/// Nạp đề xếp lớp từ content/placement/*.yaml.
///
/// Tách khỏi <see cref="ContentSeeder"/> vì hai bất biến khác nhau. Bài học không được
/// xoá vì tiến độ treo vào đó. Câu hỏi trong đề thì <b>được</b> dựng lại, nhưng chỉ khi
/// không có lượt thi nào đang trỏ vào — <c>placement_answers.item_id</c> là khoá ngoại,
/// dựng lại câu trong khi có người đang thi sẽ cắt mất bài làm của họ.
/// </summary>
public class PlacementSeeder(
    AppDbContext db,
    YamlContentLoader loader,
    PlacementValidator validator,
    ILogger<PlacementSeeder> logger)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,

        // camelCase để PromptJson đi ra client giống mọi trường khác của API.
        // PascalCase ở đây từng làm cả phần đề bài hiện trắng mà không có lỗi nào.
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Phiên bản logic dựng đề. Tăng khi đổi cách sinh PromptJson hoặc AnswerJson,
    /// kể cả khi file YAML không đổi một ký tự.
    /// </summary>
    private const string SeederVersion = "2";

    public async Task<SeedReport> SeedAsync(string contentRoot, CancellationToken ct = default)
    {
        var problems = new List<string>();

        var load = loader.LoadPlacementForms(contentRoot);
        problems.AddRange(load.Errors.Select(e => $"Doc de xep lop that bai {Path.GetFileName(e.FilePath)}: {e.Message}"));

        var docs = load.Forms.Select(f => f.Document).ToList();

        if (docs.Count == 0)
        {
            // Chưa soạn đề không phải lỗi: hệ thống vẫn chạy, chỉ là chưa xếp lớp được.
            logger.LogWarning("Không có đề xếp lớp nào trong {Root}/placement", contentRoot);
            return new SeedReport(0, 0, 0, 0, problems);
        }

        var issues = docs.SelectMany(validator.ValidateOne)
            .Concat(validator.ValidateSet(docs))
            .ToList();

        if (issues.Count > 0)
        {
            problems.AddRange(issues.Select(i => i.ToString()));
            logger.LogError("Đề xếp lớp không qua cổng chất lượng, huỷ seed. {Count} vấn đề", issues.Count);
            return new SeedReport(0, 0, 0, load.Forms.Count, problems);
        }

        // CỐ Ý không Include(Items). Nạp navigation đó rồi Clear() sẽ khiến EF vừa phát
        // lệnh DELETE cho từng dòng vừa cắt quan hệ, và những lệnh thừa đó khớp 0 dòng
        // rồi ném DbUpdateConcurrencyException làm sập cả lượt khởi động.
        // Đây đúng là cái bẫy ContentSeeder đã dính một lần.
        var existing = await db.PlacementForms
            .ToDictionaryAsync(f => f.Code, StringComparer.OrdinalIgnoreCase, ct);

        // Đếm câu bằng truy vấn riêng: chỉ cần biết đề đã dựng xong hay chưa.
        var itemCounts = await db.PlacementFormItems
            .GroupBy(i => i.FormId)
            .Select(g => new { FormId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.FormId, x => x.Count, ct);

        // Đề nào đang có người thi thì không đụng tới. Biết trước bằng một truy vấn
        // thay vì thử rồi bắt lỗi khoá ngoại, để thông báo nói được đúng lý do.
        var formsInUse = await db.PlacementAttempts
            .Select(a => a.FormId)
            .Distinct()
            .ToListAsync(ct);

        var inUse = formsInUse.ToHashSet();

        int inserted = 0, updated = 0, unchanged = 0, skipped = 0;

        foreach (var loaded in load.Forms)
        {
            var doc = loaded.Document;
            var effectiveHash = CombineWithSeederVersion(loaded.SourceHash);

            if (existing.TryGetValue(doc.Code, out var form))
            {
                if (form.SourceHash == effectiveHash && itemCounts.GetValueOrDefault(form.Id) == doc.Items.Count)
                {
                    unchanged++;
                    continue;
                }

                if (inUse.Contains(form.Id))
                {
                    // Không chặn cả lượt seed vì một đề bận: các đề khác vẫn cần được nạp.
                    skipped++;
                    problems.Add(
                        $"Đề {doc.Code} đã đổi nhưng có lượt thi đang trỏ vào nên giữ nguyên bản cũ. " +
                        "Muốn đổi nội dung thì thêm đề mã mới và tắt is_active của đề cũ.");
                    continue;
                }

                ApplyForm(form, doc, effectiveHash);

                // Xoá bằng ExecuteDelete chứ không qua change tracker: an toàn vì đã kiểm
                // không có lượt thi nào trỏ vào đề này.
                await db.PlacementFormItems.Where(i => i.FormId == form.Id).ExecuteDeleteAsync(ct);
                AddItems(form, doc);

                updated++;
            }
            else
            {
                form = new PlacementForm
                {
                    Code = doc.Code,
                    TitleVi = doc.TitleVi,
                    SourceHash = effectiveHash,
                };

                ApplyForm(form, doc, effectiveHash);
                AddItems(form, doc);

                db.PlacementForms.Add(form);
                existing[doc.Code] = form;
                inserted++;
            }
        }

        // Đề biến mất khỏi YAML thì tắt chứ không xoá — lượt thi cũ phải còn tra được
        // để học viên xem lại kết quả xếp lớp của mình.
        var codesInYaml = docs.Select(d => d.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var orphan in existing.Values.Where(f => !codesInYaml.Contains(f.Code) && f.IsActive))
        {
            orphan.IsActive = false;
            logger.LogWarning("Đề {Code} không còn trong YAML, đã tắt. Lượt thi cũ giữ nguyên.", orphan.Code);
        }

        await db.SaveChangesAsync(ct);

        var report = new SeedReport(inserted, updated, unchanged, skipped, problems);
        logger.LogInformation("Seed đề xếp lớp xong: {Report}", report);

        return report;
    }

    private static void ApplyForm(PlacementForm form, PlacementDocument doc, string hash)
    {
        form.Code = doc.Code;
        form.TitleVi = doc.TitleVi;
        form.EstimatedMinutes = doc.EstimatedMinutes;
        form.IsActive = doc.IsActive;
        form.SourceHash = hash;
    }

    /// <summary>
    /// Thêm câu vào DbSet chứ KHÔNG qua <c>form.Items</c>.
    ///
    /// Khoá chính là GUID v7 sinh phía client nên câu mới đã có Id ngay lúc tạo. Thêm nó
    /// qua navigation của một form đang được tracked thì EF thấy khoá đã có giá trị và
    /// đánh dấu Modified thay vì Added — sinh ra UPDATE khớp 0 dòng rồi ném
    /// DbUpdateConcurrencyException. Lỗi này chỉ xuất hiện ở lần seed thứ hai trở đi,
    /// vì lần đầu cả form lẫn câu đều là Added nên không lộ ra.
    /// </summary>
    private void AddItems(PlacementForm form, PlacementDocument doc)
    {
        var order = 0;

        foreach (var item in doc.Items)
        {
            // Câu Likert không có answer trong YAML (cổng chất lượng cấm), nhưng vẫn cần
            // mang cờ self_rating xuống DB vì đó là luật chấm.
            var answer = item.Kind == PlacementItemKind.Likert
                ? new PlacementAnswerDocument { SelfRating = item.SelfRating }
                : item.Answer ?? new PlacementAnswerDocument();

            db.PlacementFormItems.Add(new PlacementFormItem
            {
                FormId = form.Id,
                Code = item.Code,
                OrderIndex = order++,
                Kind = item.Kind,
                Skill = item.Skill,
                Weight = item.Weight,
                Difficulty = item.Difficulty,
                SlowAnswerSeconds = item.SlowAnswerSeconds,

                // Prompt đi ra client, Answer thì không. Hai cột tách nhau ở đây để
                // tầng API không bao giờ phải nhớ lọc trường nào.
                PromptJson = JsonSerializer.Serialize(item.Prompt, Json),
                AnswerJson = JsonSerializer.Serialize(answer, Json),
            });
        }
    }

    private static string CombineWithSeederVersion(string fileHash)
    {
        var combined = SHA256.HashData(Encoding.UTF8.GetBytes($"{SeederVersion}:{fileHash}"));
        return Convert.ToHexStringLower(combined);
    }
}
