using EnglishForIT.Application.Learning;
using EnglishForIT.Domain.Entities.Content;
using EnglishForIT.Domain.Entities.Progress;
using EnglishForIT.Domain.Enums;
using EnglishForIT.Infrastructure.Learning;
using EnglishForIT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EnglishForIT.UnitTests.Learning;

/// <summary>
/// Thi vượt.
///
/// Trọng tâm không phải phép cộng điểm mà là các luật chống lách: ngưỡng cao hơn học thường,
/// một trục hổng vẫn trượt dù điểm tổng cao, và trượt rồi phải chờ mới thi lại.
/// Thiếu bất kỳ luật nào trong ba luật đó thì thi vượt trở thành đường đi vòng dễ hơn đường chính.
/// </summary>
public class ChallengeServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 19, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task DungHetThiQuaVaMoKhoaBai()
    {
        var db = NewDb();
        var (userId, codes) = await SeedLesson(db);

        var result = await NewService(db).SubmitAsync(userId, "T-01", AllCorrect(codes), Now);

        Assert.NotNull(result);
        Assert.True(result!.Passed);
        Assert.Equal(100, result.Score);

        var mastery = await db.LessonMasteries.SingleAsync();
        Assert.Equal(LessonState.Mastered, mastery.State);
        Assert.True(mastery.UnlockedByChallenge);

        // Vé thi vượt phải tồn tại riêng: engine phân biệt "học xong" với "thi vượt qua".
        Assert.Equal(1, await db.ChallengePasses.CountAsync());

        var evt = await db.LessonStateEvents.SingleAsync();
        Assert.Equal(LessonStateReason.ChallengePassed, evt.Reason);
    }

    [Fact]
    public async Task DiemDuNhungMotTrucHongThiVanTruot()
    {
        var db = NewDb();
        // Sáu câu Đọc, hai câu Nghe. Sai cả hai câu Nghe: điểm tổng 75.
        var (userId, codes) = await SeedLesson(db);

        var responses = codes
            .Select((code, i) => new ItemResponse(code, i < 6 ? 1 : 0))
            .ToList();

        var result = await NewService(db).SubmitAsync(userId, "T-01", responses, Now);

        Assert.False(result!.Passed);
        Assert.Contains("Listening", result.SkillsBelowThreshold);
        Assert.Contains("Nghe", result.MessageVi);
        Assert.Empty(await db.ChallengePasses.ToListAsync());
    }

    [Fact]
    public async Task DiemTrenNguongThuongNhungDuoiNguongThiVuotThiTruot()
    {
        var db = NewDb();
        var (userId, codes) = await SeedLesson(db, readingItems: 10, listeningItems: 10);

        // Sai hai câu mỗi trục: cả hai trục đều 80, không trục nào hổng.
        // Điểm tổng 80 — vừa đủ ngưỡng học thường nhưng chưa tới ngưỡng thi vượt 85.
        var wrong = new HashSet<int> { 0, 1, 10, 11 };

        var responses = codes
            .Select((code, i) => new ItemResponse(code, wrong.Contains(i) ? 0 : 1))
            .ToList();

        var result = await NewService(db).SubmitAsync(userId, "T-01", responses, Now);

        Assert.Equal(80, result!.Score);
        Assert.False(result.Passed);
        Assert.Empty(result.SkillsBelowThreshold);
        Assert.Contains("chưa đủ", result.MessageVi);
    }

    [Fact]
    public async Task TruotRoiThiPhaiChoMoiThiLaiDuoc()
    {
        var db = NewDb();
        var (userId, codes) = await SeedLesson(db);
        var service = NewService(db);

        var allWrong = codes.Select(c => new ItemResponse(c, 0)).ToList();
        var first = await service.SubmitAsync(userId, "T-01", allWrong, Now);

        Assert.False(first!.Passed);
        Assert.NotNull(first.RetryAt);

        // Thi lại ngay sau đó: bị chặn, kể cả khi lần này trả lời đúng hết.
        var offer = await service.GetOfferAsync(userId, "T-01", Now.AddHours(1));

        Assert.False(offer!.Eligible);
        Assert.Empty(offer.Items);
        Assert.Contains("trượt", offer.ReasonVi);

        var retry = await service.SubmitAsync(userId, "T-01", AllCorrect(codes), Now.AddHours(1));

        Assert.False(retry!.Passed);
        Assert.Empty(await db.ChallengePasses.ToListAsync());
    }

    [Fact]
    public async Task HetThoiGianChoThiThiLaiDuoc()
    {
        var db = NewDb();
        var (userId, codes) = await SeedLesson(db);
        var service = NewService(db);

        await service.SubmitAsync(userId, "T-01", codes.Select(c => new ItemResponse(c, 0)).ToList(), Now);

        var later = Now.AddHours(13);
        var offer = await service.GetOfferAsync(userId, "T-01", later);

        Assert.True(offer!.Eligible);

        var retry = await service.SubmitAsync(userId, "T-01", AllCorrect(codes), later);

        Assert.True(retry!.Passed);
    }

    [Fact]
    public async Task TruotKhongLamTrangThaiBaiTeDiHon()
    {
        var db = NewDb();
        var (userId, codes) = await SeedLesson(db);

        db.LessonMasteries.Add(new LessonMastery
        {
            UserId = userId,
            LessonId = (await db.Lessons.SingleAsync()).Id,
            State = LessonState.InProgress,
            MasteryRaw = 55,
            MasteryEffective = 55,
        });
        await db.SaveChangesAsync();

        await NewService(db).SubmitAsync(
            userId, "T-01", codes.Select(c => new ItemResponse(c, 0)).ToList(), Now);

        var mastery = await db.LessonMasteries.SingleAsync();

        Assert.Equal(LessonState.InProgress, mastery.State);
        Assert.Equal(55, mastery.MasteryEffective);
        Assert.False(mastery.UnlockedByChallenge);
    }

    [Fact]
    public async Task BaiQuaItCauThiKhongCho()
    {
        var db = NewDb();
        var (userId, _) = await SeedLesson(db, readingItems: 3, listeningItems: 0);

        var offer = await NewService(db).GetOfferAsync(userId, "T-01", Now);

        Assert.False(offer!.Eligible);
        Assert.Contains("3 câu", offer.ReasonVi);
    }

    [Fact]
    public async Task DaThaoRoiThiKhongCanThiVuot()
    {
        var db = NewDb();
        var (userId, _) = await SeedLesson(db);

        db.LessonMasteries.Add(new LessonMastery
        {
            UserId = userId,
            LessonId = (await db.Lessons.SingleAsync()).Id,
            State = LessonState.Mastered,
        });
        await db.SaveChangesAsync();

        var offer = await NewService(db).GetOfferAsync(userId, "T-01", Now);

        Assert.False(offer!.Eligible);
        Assert.Contains("đã thạo", offer.ReasonVi);
    }

    [Fact]
    public async Task DeThiVuotKhongBaoGioKemDapAn()
    {
        var db = NewDb();
        var (userId, _) = await SeedLesson(db);

        var offer = await NewService(db).GetOfferAsync(userId, "T-01", Now);

        var json = System.Text.Json.JsonSerializer.Serialize(offer);

        Assert.True(offer!.Eligible);
        Assert.DoesNotContain("answer", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CauSaiVaoOnNgayMai_CauDungHenXaHon()
    {
        var db = NewDb();
        var (userId, codes) = await SeedLesson(db);

        // Sai đúng một câu Đọc: vẫn đủ 87.5 điểm để qua.
        var responses = codes.Select((c, i) => new ItemResponse(c, i == 0 ? 0 : 1)).ToList();

        var result = await NewService(db).SubmitAsync(userId, "T-01", responses, Now);

        Assert.True(result!.Passed);
        Assert.Equal(8, result.ReviewItemsScheduled);

        var queue = await db.ReviewQueue.ToListAsync();
        var itemsByCode = await db.LessonItems.ToDictionaryAsync(i => i.Id, i => i.Code);

        var wrongRow = queue.Single(r => itemsByCode[r.ItemId] == codes[0]);
        var rightRow = queue.First(r => itemsByCode[r.ItemId] != codes[0]);

        Assert.Equal(1, wrongRow.IntervalDays);
        Assert.Equal(7, rightRow.IntervalDays);
    }

    [Fact]
    public async Task KhongCoBaiThiTraVeNull()
    {
        var db = NewDb();

        Assert.Null(await NewService(db).GetOfferAsync(Guid.NewGuid(), "KHONG-CO", Now));
        Assert.Null(await NewService(db).SubmitAsync(Guid.NewGuid(), "KHONG-CO", [], Now));
    }

    // -----------------------------------------------------------------------

    private static AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"challenge-{Guid.NewGuid()}")
            .Options);

    private static ChallengeService NewService(AppDbContext db) =>
        new(db, Options.Create(new LearningPolicyOptions()), NullLogger<ChallengeService>.Instance);

    private static List<ItemResponse> AllCorrect(IReadOnlyList<string> codes) =>
        [.. codes.Select(c => new ItemResponse(c, 1))];

    /// <summary>
    /// Một bài với hai bước chấm được, cộng một bước Nói để chắc rằng nó bị loại khỏi đề.
    /// Đáp án đúng của mọi câu là chỉ số 1.
    /// </summary>
    private static async Task<(Guid UserId, List<string> Codes)> SeedLesson(
        AppDbContext db, int readingItems = 6, int listeningItems = 2)
    {
        var lesson = new Lesson
        {
            Code = "T-01",
            Slug = "test",
            TitleVi = "Bài kiểm thử",
            TitleEn = "Test lesson",
            Track = LearningTrack.Reading,
            Layer = ContextLayer.Professional,
            Level = CefrLevel.B1,
            UnitCode = "U-T",
            ObjectiveVi = "x",
            ObjectiveObservable = "x",
            ExplanationJson = "{}",
            CommonMistakesJson = "[]",
            BodyJson = "{}",
            SourceHash = "test",
            Status = ContentStatus.Published,
            SupportedSkills = listeningItems > 0
                ? [SkillType.Reading, SkillType.Listening]
                : [SkillType.Reading],
        };
        db.Lessons.Add(lesson);

        var codes = new List<string>();

        void AddActivity(ActivityKind kind, SkillType skill, int order, int count)
        {
            var activity = new LessonActivity
            {
                LessonId = lesson.Id,
                Kind = kind,
                Skill = skill,
                OrderIndex = order,
                PayloadJson = "{}",
            };
            db.LessonActivities.Add(activity);

            for (var i = 0; i < count; i++)
            {
                var code = $"T-01-{skill}-{i}";
                codes.Add(code);

                db.LessonItems.Add(new LessonItem
                {
                    ActivityId = activity.Id,
                    Code = code,
                    OrderIndex = i,
                    PromptJson = """{"Choices":["sai","đúng"],"PromptVi":"Câu hỏi?"}""",
                    AnswerJson = """{"answer":1}""",
                });
            }
        }

        AddActivity(ActivityKind.Quiz, SkillType.Reading, 0, readingItems);

        if (listeningItems > 0)
        {
            AddActivity(ActivityKind.Listen, SkillType.Listening, 1, listeningItems);
        }

        // Bước Nói không chấm được: câu của nó phải không xuất hiện trong đề thi vượt.
        var speak = new LessonActivity
        {
            LessonId = lesson.Id,
            Kind = ActivityKind.Speak,
            Skill = SkillType.Speaking,
            OrderIndex = 9,
            PayloadJson = "{}",
        };
        db.LessonActivities.Add(speak);
        db.LessonItems.Add(new LessonItem
        {
            ActivityId = speak.Id,
            Code = "T-01-SPEAK-0",
            OrderIndex = 0,
            PromptJson = """{"TargetEn":"hello"}""",
            AnswerJson = "{}",
        });

        await db.SaveChangesAsync();

        return (Guid.NewGuid(), codes);
    }
}
