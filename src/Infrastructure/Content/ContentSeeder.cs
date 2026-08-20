using System.Text.Json;
using System.Text.Json.Serialization;
using EnglishForIT.Application.Content;
using EnglishForIT.Domain.Entities.Content;
using EnglishForIT.Domain.Enums;
using EnglishForIT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EnglishForIT.Infrastructure.Content;

public record SeedReport(
    int Inserted,
    int Updated,
    int Unchanged,
    int Skipped,
    IReadOnlyList<string> Problems)
{
    public bool HasProblems => Problems.Count > 0;

    public override string ToString() =>
        $"them {Inserted}, cap nhat {Updated}, khong doi {Unchanged}, bo qua {Skipped}, van de {Problems.Count}";
}

/// <summary>
/// Nạp nội dung từ YAML vào DB.
///
/// Bất biến quan trọng nhất của cả hệ thống: <b>seed không bao giờ xoá tiến độ học viên</b>.
/// Cách bảo đảm:
///  1. Upsert theo <see cref="Lesson.Code"/>, không xoá rồi tạo lại.
///  2. Bài biến mất khỏi YAML thì đánh dấu Archived, KHÔNG xoá dòng —
///     xoá dòng sẽ cascade sang lesson_mastery và mất sạch tiến độ.
///  3. Bài không đổi hash thì bỏ qua hoàn toàn, không chạm tới.
/// </summary>
public class ContentSeeder(
    AppDbContext db,
    YamlContentLoader loader,
    LessonValidator validator,
    ILogger<ContentSeeder> logger)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Phiên bản của chính logic dựng nội dung.
    ///
    /// Hash lưu trong DB là hash của (phiên bản này + nội dung file). Nghĩa là đổi cách
    /// dựng bước học hay cách sinh câu hỏi thì mọi bài tự dựng lại ở lần khởi động sau,
    /// dù file YAML không đổi một ký tự.
    ///
    /// BẮT BUỘC tăng số này mỗi khi sửa PlanActivities, SyncItems, hay ApplyDocument.
    /// Quên tăng thì nội dung cũ nằm lại trong DB và không ai biết.
    /// </summary>
    private const string SeederVersion = "3";

    public async Task<SeedReport> SeedAsync(string contentRoot, CancellationToken ct = default)
    {
        var problems = new List<string>();

        var load = loader.LoadLessons(contentRoot);
        problems.AddRange(load.Errors.Select(e => $"Doc file that bai {Path.GetFileName(e.FilePath)}: {e.Message}"));

        var docs = load.Lessons.Select(l => l.Document).ToList();

        // Cổng chất lượng chạy TRƯỚC khi chạm DB. Nội dung hỏng không được vào DB
        // rồi mới phát hiện — lúc đó học viên đã nhìn thấy nó.
        var issues = validator.ValidateSet(docs);
        if (issues.Count > 0)
        {
            problems.AddRange(issues.Select(i => i.ToString()));
            logger.LogError("Nội dung không qua cổng chất lượng, huỷ seed. {Count} vấn đề", issues.Count);
            return new SeedReport(0, 0, 0, load.Lessons.Count, problems);
        }

        // CỐ Ý không Include(Activities). Nạp navigation đó rồi Clear() sẽ khiến EF coi
        // các bước học là bị cắt khỏi cha và phát sinh lệnh DELETE cho từng dòng —
        // trong khi RebuildActivitiesAsync đã xoá chúng bằng ExecuteDelete, nên những
        // lệnh DELETE thừa đó khớp 0 dòng và ném DbUpdateConcurrencyException.
        var existing = await db.Lessons
            .Include(l => l.Prerequisites)
            .ToDictionaryAsync(l => l.Code, StringComparer.OrdinalIgnoreCase, ct);

        // Đếm bước học bằng truy vấn riêng thay vì Include: chỉ cần biết bài đã hoàn chỉnh chưa.
        var activityCounts = await db.LessonActivities
            .GroupBy(a => a.LessonId)
            .Select(g => new { LessonId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.LessonId, x => x.Count, ct);

        int inserted = 0, updated = 0, unchanged = 0;

        // Chỉ những bài thật sự đổi mới cần dựng lại cạnh và các bước học.
        // Dựng lại bài không đổi là vừa lãng phí vừa nguy hiểm: xoá bước học sẽ
        // cascade xuống lesson_items và kéo theo cả hàng đợi ôn tập của học viên.
        var touched = new List<LoadedLesson>();

        // Vòng 1: tạo hoặc cập nhật bản thân các bài.
        foreach (var loaded in load.Lessons)
        {
            var doc = loaded.Document;
            var effectiveHash = CombineWithSeederVersion(loaded.SourceHash);

            if (existing.TryGetValue(doc.Code, out var lesson))
            {
                // Hash khớp CHƯA đủ để kết luận bài đã hoàn chỉnh: seed lưu làm hai lượt,
                // nên một lần crash giữa hai lượt để lại bài có hash đúng mà không có bước học nào.
                // Kiểm thêm sự tồn tại của bước học thì lần chạy sau tự chữa được trạng thái đó.
                var complete = activityCounts.GetValueOrDefault(lesson.Id) > 0;

                if (lesson.SourceHash == effectiveHash
                    && lesson.Status == ContentStatus.Published
                    && complete)
                {
                    unchanged++;
                    continue;
                }

                if (!complete)
                {
                    logger.LogWarning(
                        "Bài {Code} có hash đúng nhưng thiếu bước học, dựng lại. Nhiều khả năng lần seed trước bị gián đoạn.",
                        doc.Code);
                }

                ApplyDocument(lesson, doc, effectiveHash);
                updated++;
            }
            else
            {
                lesson = NewLesson(doc, effectiveHash);
                db.Lessons.Add(lesson);
                existing[doc.Code] = lesson;
                inserted++;
            }

            touched.Add(loaded);
        }

        // Vòng 2: dựng lại DAG tiên quyết và các bước học cho những bài đã đổi.
        //
        // KHÔNG lưu giữa hai vòng. Khoá chính là GUID v7 sinh phía client nên bài mới đã có Id
        // ngay khi tạo đối tượng, không cần INSERT trước mới trỏ tiên quyết được.
        // Lưu hai lượt từng gây hai lỗi thật: trạng thái nửa vời khi lượt hai hỏng,
        // và EF so nhầm concurrency token giữa hai lượt.
        foreach (var loaded in touched)
        {
            var lesson = existing[loaded.Document.Code];
            SyncPrerequisites(lesson, loaded.Document, existing);
            await SyncActivitiesAsync(lesson, loaded.Document, ct);
        }

        // Bài không còn trong YAML: lưu trữ chứ không xoá.
        var codesInYaml = docs.Select(d => d.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var orphans = existing.Values
            .Where(l => !codesInYaml.Contains(l.Code) && l.Status != ContentStatus.Archived)
            .ToList();

        foreach (var orphan in orphans)
        {
            orphan.Status = ContentStatus.Archived;
            logger.LogWarning(
                "Bài {Code} không còn trong YAML, đã chuyển sang Archived. Tiến độ học viên giữ nguyên.",
                orphan.Code);
        }

        await db.SaveChangesAsync(ct);

        var report = new SeedReport(inserted, updated, unchanged, 0, problems);
        logger.LogInformation("Seed xong: {Report}", report);

        return report;
    }

    private static string CombineWithSeederVersion(string fileHash)
    {
        var combined = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"{SeederVersion}:{fileHash}"));

        return Convert.ToHexStringLower(combined);
    }

    private static Lesson NewLesson(LessonDocument doc, string hash)
    {
        var lesson = new Lesson
        {
            Code = doc.Code,
            Slug = doc.Slug,
            TitleVi = doc.TitleVi,
            TitleEn = doc.TitleEn,
            ObjectiveVi = doc.ObjectiveVi,
            ObjectiveObservable = doc.ObjectiveObservable,
            ExplanationJson = "{}",
            CommonMistakesJson = "[]",
            BodyJson = "{}",
            SourceHash = hash,
        };

        ApplyDocument(lesson, doc, hash);
        return lesson;
    }

    private static void ApplyDocument(Lesson lesson, LessonDocument doc, string hash)
    {
        lesson.Slug = doc.Slug;
        lesson.TitleVi = doc.TitleVi;
        lesson.TitleEn = doc.TitleEn;
        lesson.Track = doc.Track;
        lesson.Level = doc.Level;
        lesson.Layer = doc.Layer;
        lesson.UnitCode = doc.UnitCode;
        lesson.OrderIndex = doc.OrderIndex;
        lesson.EstimatedMinutes = doc.EstimatedMinutes;
        lesson.IsCheckpoint = doc.IsCheckpoint;
        lesson.Illustration = doc.Illustration;
        lesson.ObjectiveVi = doc.ObjectiveVi;
        lesson.ObjectiveObservable = doc.ObjectiveObservable;
        lesson.MasteryWeights = doc.MasteryWeights;

        // Suy ra từ phần thực có thay vì tin vào khai báo tay: khai tay hay quên
        // cập nhật khi người soạn thêm hoặc bớt một phần của bài.
        lesson.SupportedSkills = [.. LessonValidator.InferSupportedSkills(doc)];

        lesson.ExplanationJson = JsonSerializer.Serialize(doc.Explanation, Json);
        lesson.CommonMistakesJson = JsonSerializer.Serialize(doc.CommonMistakes, Json);

        // Toàn bộ phần hiển thị gói vào một cột JSONB: lesson player chỉ cần một truy vấn.
        lesson.BodyJson = JsonSerializer.Serialize(new
        {
            doc.Vocabulary,
            doc.SentencePatterns,
            doc.Dialogue,
            doc.MemoryTrickVi,
        }, Json);

        lesson.SourceHash = hash;
        lesson.Status = ContentStatus.Published;
    }

    private void SyncPrerequisites(Lesson lesson, LessonDocument doc, Dictionary<string, Lesson> byCode)
    {
        var desired = doc.Prerequisites
            .Where(p => byCode.ContainsKey(p.Lesson))
            .ToDictionary(p => byCode[p.Lesson].Id, p => p);

        // Bỏ cạnh không còn trong YAML.
        foreach (var stale in lesson.Prerequisites.Where(p => !desired.ContainsKey(p.RequiredLessonId)).ToList())
        {
            db.LessonPrerequisites.Remove(stale);
            lesson.Prerequisites.Remove(stale);
        }

        foreach (var (requiredId, spec) in desired)
        {
            var edge = lesson.Prerequisites.FirstOrDefault(p => p.RequiredLessonId == requiredId);

            if (edge is null)
            {
                var added = new LessonPrerequisite
                {
                    LessonId = lesson.Id,
                    RequiredLessonId = requiredId,
                    Kind = spec.Kind,
                    MinMastery = spec.MinMastery,
                };

                // Phải gọi Add trên DbSet, KHÔNG chỉ thêm vào navigation.
                //
                // Khoá chính là GUID v7 sinh sẵn lúc dựng đối tượng, nên khi EF dò thay đổi và
                // gặp bản ghi mới trong navigation, nó thấy khoá đã có giá trị và đoán đây là
                // bản ghi cũ: trạng thái thành Modified, lệnh sinh ra là UPDATE cho một dòng
                // chưa hề tồn tại, khớp 0 dòng, ném DbUpdateConcurrencyException và API chết
                // ngay lúc khởi động. Add trên DbSet nói thẳng đây là bản ghi mới.
                //
                // Lỗi này chỉ nổ khi YAML THÊM một cạnh tiên quyết mới. Đổi loại hay đổi ngưỡng
                // của cạnh đã có thì không sao, nên nó nằm im cho tới lần đầu ai đó sửa DAG.
                db.LessonPrerequisites.Add(added);
                lesson.Prerequisites.Add(added);
            }
            else
            {
                edge.Kind = spec.Kind;
                edge.MinMastery = spec.MinMastery;
            }
        }
    }

    /// <summary>Bản mô tả một bước học đã dựng xong, chưa gắn vào DB.</summary>
    private record PlannedActivity(
        ActivityKind Kind,
        SkillType Skill,
        object Payload,
        int PassScore,
        IReadOnlyList<QuizItemDocument> Items);

    /// <summary>
    /// Đồng bộ các bước học theo đúng thứ tự ưu tiên kỹ năng:
    /// nghe, từ vựng, nhắc lại, nói, đọc, viết, kiểm tra.
    ///
    /// Upsert theo (lesson_id, order_index) chứ KHÔNG xoá rồi tạo lại. Lý do là dây chuyền
    /// khoá ngoại: lesson_items trỏ tới activity theo cascade, và review_queue trỏ tới
    /// lesson_items cũng theo cascade. Xoá một activity vì sửa một dấu phẩy trong bài
    /// sẽ kéo theo cả hàng đợi ôn tập của mọi học viên đang học bài đó.
    /// </summary>
    private async Task SyncActivitiesAsync(Lesson lesson, LessonDocument doc, CancellationToken ct)
    {
        var planned = PlanActivities(doc);

        var current = await db.LessonActivities
            .Where(a => a.LessonId == lesson.Id)
            .Include(a => a.Items)
            .OrderBy(a => a.OrderIndex)
            .ToListAsync(ct);

        for (var index = 0; index < planned.Count; index++)
        {
            var plan = planned[index];
            var activity = current.FirstOrDefault(a => a.OrderIndex == index);

            if (activity is null)
            {
                activity = new LessonActivity
                {
                    LessonId = lesson.Id,
                    OrderIndex = index,
                    Kind = plan.Kind,
                    Skill = plan.Skill,
                    PayloadJson = "{}",
                    PassScore = plan.PassScore,
                };

                db.LessonActivities.Add(activity);
            }

            activity.Kind = plan.Kind;
            activity.Skill = plan.Skill;
            activity.PassScore = plan.PassScore;
            activity.PayloadJson = JsonSerializer.Serialize(plan.Payload, Json);

            SyncItems(lesson, activity, plan, index);
        }

        // Bài rút gọn lại (ví dụ bỏ hẳn phần viết) thì các bước dôi ra mới bị xoá.
        foreach (var extra in current.Where(a => a.OrderIndex >= planned.Count))
        {
            db.LessonActivities.Remove(extra);
        }
    }

    /// <summary>
    /// Sinh câu hỏi chấm được thành lesson_items, upsert theo mã.
    ///
    /// Chỉ những phần có đáp án xác định mới thành item: câu hỏi nghe, câu hỏi đọc, và quiz.
    /// Phần nói chấm bằng âm vị và phần viết chấm bằng luật, cả hai không hợp với mô hình
    /// một-đáp-án-đúng nên không đưa vào hàng đợi ôn tập.
    /// </summary>
    private void SyncItems(Lesson lesson, LessonActivity activity, PlannedActivity plan, int activityIndex)
    {
        var existingItems = activity.Items.ToDictionary(i => i.Code, StringComparer.OrdinalIgnoreCase);

        for (var order = 0; order < plan.Items.Count; order++)
        {
            var source = plan.Items[order];
            var code = $"{lesson.Code}-A{activityIndex}-Q{order + 1}";

            // Đề bài tách hẳn khỏi đáp án. Chỉ cột prompt được map ra DTO trả về client;
            // cột answer không bao giờ rời máy chủ.
            var prompt = JsonSerializer.Serialize(new
            {
                source.Kind,
                source.PromptVi,
                source.PromptEn,
                source.AudioText,
                source.Choices,
                source.ReadingSkill,
                Skill = source.Skill.ToString(),
            }, Json);

            var answer = JsonSerializer.Serialize(new { source.Answer }, Json);

            if (existingItems.TryGetValue(code, out var item))
            {
                item.OrderIndex = order;
                item.PromptJson = prompt;
                item.AnswerJson = answer;
                item.Difficulty = source.Difficulty;
                existingItems.Remove(code);
                continue;
            }

            db.LessonItems.Add(new LessonItem
            {
                ActivityId = activity.Id,
                Code = code,
                OrderIndex = order,
                PromptJson = prompt,
                AnswerJson = answer,
                Difficulty = source.Difficulty,
            });
        }

        // Câu bị xoá khỏi YAML thì mới gỡ khỏi DB.
        foreach (var stale in existingItems.Values)
        {
            db.LessonItems.Remove(stale);
        }
    }

    /// <summary>
    /// Ngưỡng đạt của MỌI bước, không phân biệt loại.
    ///
    /// Trước đây từ vựng và nhắc lại chỉ cần 70, nói cần 75, còn lại 80. Ngưỡng thấp ở những
    /// bước đầu nghe thì nhân văn, nhưng hệ quả là học viên đi hết bài với vốn từ nhớ lõm bõm
    /// rồi vấp ở bước kiểm tra — và không hiểu vì sao, vì mọi bước trước đều báo "đạt".
    /// </summary>
    private const int StepPassScore = 80;

    private static List<PlannedActivity> PlanActivities(LessonDocument doc)
    {
        var planned = new List<PlannedActivity>();

        // Payload CỐ Ý không chứa câu hỏi. Câu hỏi và đáp án chỉ sống trong lesson_items,
        // và chỉ cột prompt của item mới được trả ra client. Nhét nguyên đối tượng
        // có kèm `answer` vào payload là cách rò đáp án mà không ai nhận ra.
        // TỪ VỰNG ĐỨNG ĐẦU, trước cả phần nghe.
        //
        // Trước đây bài mở bằng đoạn hội thoại, tức là bắt học viên nghe một đoạn chứa những từ
        // họ chưa từng gặp rồi mới cho xem nghĩa. Người đã biết vốn từ thì thấy bình thường,
        // người mất gốc thì nghe xong không bắt được chữ nào và kết luận mình không học nổi.
        // Biết mặt chữ và phát âm trước, rồi mới nghe chúng trong câu.
        if (doc.Vocabulary.Count > 0)
        {
            planned.Add(new PlannedActivity(
                ActivityKind.Vocab, SkillType.Reading,
                new { doc.Vocabulary, doc.SentencePatterns }, StepPassScore, []));
        }

        if (doc.Listening is { } listening)
        {
            planned.Add(new PlannedActivity(
                ActivityKind.Listen,
                SkillType.Listening,
                new
                {
                    listening.ContextVi,
                    listening.TranscriptEn,
                    listening.TranscriptVi,
                    listening.Speed,
                    listening.Voice,
                },
                StepPassScore,
                listening.Questions));
        }

        var shadowDrills = doc.SpeakingDrills.Where(d => d.Kind == "shadow").ToList();
        if (shadowDrills.Count > 0)
        {
            planned.Add(new PlannedActivity(
                ActivityKind.Shadow, SkillType.Speaking, new { Drills = shadowDrills }, StepPassScore, []));
        }

        var speakDrills = doc.SpeakingDrills.Where(d => d.Kind != "shadow").ToList();
        if (speakDrills.Count > 0)
        {
            planned.Add(new PlannedActivity(
                ActivityKind.Speak, SkillType.Speaking, new { Drills = speakDrills }, StepPassScore, []));
        }

        if (doc.Reading is { } reading)
        {
            planned.Add(new PlannedActivity(
                ActivityKind.Read,
                SkillType.Reading,
                new { reading.Kind, reading.ContextVi, reading.TextEn, reading.TextVi },
                StepPassScore,
                reading.Questions));
        }

        if (doc.Writing is { } writing)
        {
            planned.Add(new PlannedActivity(
                ActivityKind.Write, SkillType.Writing, writing, StepPassScore, []));
        }

        if (doc.Quiz.Count > 0)
        {
            // Bước kiểm tra không có payload riêng: toàn bộ nội dung nằm ở item.
            planned.Add(new PlannedActivity(
                ActivityKind.Quiz, SkillType.Listening, new { }, StepPassScore, doc.Quiz));
        }

        return planned;
    }
}
