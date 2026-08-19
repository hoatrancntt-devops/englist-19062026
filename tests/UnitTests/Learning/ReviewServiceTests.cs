using EnglishForIT.Domain.Entities.Content;
using EnglishForIT.Domain.Entities.Progress;
using EnglishForIT.Domain.Enums;
using EnglishForIT.Infrastructure.Learning;
using EnglishForIT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace EnglishForIT.UnitTests.Learning;

/// <summary>
/// Công thức giãn cách và luật chọn câu.
///
/// Đây là phần sai thì không ai phát hiện được: lịch ôn lệch chỉ lộ ra sau vài tuần,
/// dưới dạng "học mãi không nhớ", và lúc đó không còn cách nào truy ngược.
/// </summary>
public class ReviewServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 18, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task TraLoiDungThiKhoangCachNhanHeSoGian()
    {
        await using var db = NewDb();
        var (userId, itemCode) = await SeedOneItem(db, dueDaysAgo: 0, intervalDays: 4, ease: 2.5);

        var result = await NewService(db).SubmitAnswerAsync(userId, itemCode, chosenIndex: 1, Now);

        Assert.NotNull(result);
        Assert.True(result.Correct);
        // 4 nhân 2.5 bằng 10.
        Assert.Equal(10, result.NextIntervalDays);
    }

    [Fact]
    public async Task TraLoiSaiThiKeoLichVeMotNgay()
    {
        await using var db = NewDb();
        var (userId, itemCode) = await SeedOneItem(db, dueDaysAgo: 0, intervalDays: 30, ease: 2.5);

        var result = await NewService(db).SubmitAnswerAsync(userId, itemCode, chosenIndex: 0, Now);

        Assert.NotNull(result);
        Assert.False(result.Correct);
        Assert.Equal(1, result.NextIntervalDays);

        var row = await db.ReviewQueue.SingleAsync();
        Assert.Equal(1, row.LapseCount);
        // Sàn 1.3 khớp ràng buộc CHECK ở tầng DB; 2.5 trừ 0.2 vẫn trên sàn.
        Assert.Equal(2.3, row.Ease, precision: 2);
    }

    [Fact]
    public async Task KhoangCachKhongVuotTranSauMuoiNgay()
    {
        await using var db = NewDb();
        var (userId, itemCode) = await SeedOneItem(db, dueDaysAgo: 0, intervalDays: 50, ease: 3.0);

        var result = await NewService(db).SubmitAnswerAsync(userId, itemCode, chosenIndex: 1, Now);

        // 50 nhân 3 là 150, nhưng trần cứng là 60: quá mốc đó việc ôn không còn ý nghĩa.
        Assert.Equal(60, result!.NextIntervalDays);
    }

    [Fact]
    public async Task OnTapKhongDungVaoMasteryCuaBai()
    {
        await using var db = NewDb();
        var (userId, itemCode) = await SeedOneItem(db, dueDaysAgo: 3, intervalDays: 6, ease: 2.5);

        db.LessonMasteries.Add(new LessonMastery
        {
            UserId = userId,
            LessonId = (await db.Lessons.SingleAsync()).Id,
            MasteryRaw = 92,
            MasteryEffective = 92,
            State = LessonState.Mastered,
        });
        await db.SaveChangesAsync();

        // Trả lời sai câu ôn.
        await NewService(db).SubmitAnswerAsync(userId, itemCode, chosenIndex: 0, Now);

        var mastery = await db.LessonMasteries.SingleAsync();

        // Điểm bài phải giữ nguyên. Nếu ôn sai mà hạ điểm thì một buổi ôn tệ có thể
        // tụt bài đã đạt xuống dưới ngưỡng, và engine chống nhảy cóc khoá dây chuyền
        // các bài phía sau — phạt học viên đúng vì họ chịu khó ôn.
        Assert.Equal(92, mastery.MasteryEffective);
        Assert.Equal(LessonState.Mastered, mastery.State);
    }

    [Fact]
    public async Task ChiLayCauDaToiHanChuKhongLayCaHangDoi()
    {
        await using var db = NewDb();
        var (userId, _) = await SeedOneItem(db, dueDaysAgo: 2, intervalDays: 1, ease: 2.5);

        // Thêm một câu còn xa hạn.
        var activity = await db.LessonActivities.SingleAsync();
        var future = new LessonItem
        {
            ActivityId = activity.Id,
            Code = "T-01-Q9",
            OrderIndex = 9,
            PromptJson = """{"Kind":"mcq","Skill":"Reading","Choices":["a","b"],"PromptVi":"?"}""",
            AnswerJson = """{"answer":1}""",
        };
        db.LessonItems.Add(future);
        db.ReviewQueue.Add(new ReviewQueueItem
        {
            UserId = userId,
            ItemId = future.Id,
            DueAt = Now.AddDays(9),
            IntervalDays = 9,
        });
        await db.SaveChangesAsync();

        var session = await NewService(db).GetSessionAsync(userId, Now);

        Assert.Single(session.Cards);
        Assert.Equal(1, session.TotalDue);
    }

    [Fact]
    public async Task HangDoiRongThiNoiRoBaoGioQuayLai()
    {
        await using var db = NewDb();
        var (userId, _) = await SeedOneItem(db, dueDaysAgo: -5, intervalDays: 5, ease: 2.5);

        var session = await NewService(db).GetSessionAsync(userId, Now);

        Assert.Empty(session.Cards);
        Assert.Equal(0, session.TotalDue);
        Assert.NotNull(session.NextDueAt);
        // Thông báo phải có con số ngày, không chỉ nói là trống.
        Assert.Contains("5 ngày", session.MessageVi);
    }

    [Fact]
    public async Task KhongTraLoiDuocCauCuaNguoiKhac()
    {
        await using var db = NewDb();
        var (_, itemCode) = await SeedOneItem(db, dueDaysAgo: 0, intervalDays: 1, ease: 2.5);

        var result = await NewService(db).SubmitAnswerAsync(Guid.NewGuid(), itemCode, 1, Now);

        // Null để tầng trên ra 404, không tiết lộ câu đó có tồn tại hay không.
        Assert.Null(result);
    }

    [Fact]
    public async Task BuoiOnCoTranSoCau()
    {
        await using var db = NewDb();
        var userId = Guid.NewGuid();
        var activity = await SeedLesson(db);

        for (var i = 0; i < ReviewService.SessionSize + 7; i++)
        {
            var item = new LessonItem
            {
                ActivityId = activity.Id,
                Code = $"T-01-Q{i}",
                OrderIndex = i,
                PromptJson = """{"Kind":"mcq","Skill":"Reading","Choices":["a","b"],"PromptVi":"?"}""",
                AnswerJson = """{"answer":1}""",
            };
            db.LessonItems.Add(item);
            db.ReviewQueue.Add(new ReviewQueueItem
            {
                UserId = userId,
                ItemId = item.Id,
                // Câu càng cũ càng quá hạn lâu, để kiểm luôn thứ tự ưu tiên.
                DueAt = Now.AddDays(-i - 1),
                IntervalDays = 1,
            });
        }
        await db.SaveChangesAsync();

        var session = await NewService(db).GetSessionAsync(userId, Now);

        Assert.Equal(ReviewService.SessionSize, session.Cards.Count);
        Assert.Equal(ReviewService.SessionSize + 7, session.TotalDue);
        // Câu quá hạn lâu nhất phải đứng đầu: đó là câu sắp rơi khỏi trí nhớ.
        Assert.Equal(session.Cards.Max(c => c.OverdueDays), session.Cards[0].OverdueDays);
        Assert.Contains("hàng đợi", session.MessageVi);
    }

    // -----------------------------------------------------------------------

    private static AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"review-{Guid.NewGuid()}")
            .Options);

    private static ReviewService NewService(AppDbContext db) =>
        new(db, NullLogger<ReviewService>.Instance);

    private static async Task<LessonActivity> SeedLesson(AppDbContext db)
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
        };
        db.Lessons.Add(lesson);

        var activity = new LessonActivity
        {
            LessonId = lesson.Id,
            Kind = ActivityKind.Quiz,
            Skill = SkillType.Reading,
            OrderIndex = 0,
            PayloadJson = "{}",
        };
        db.LessonActivities.Add(activity);

        await db.SaveChangesAsync();
        return activity;
    }

    /// <summary>Một câu trong hàng đợi. Đáp án đúng luôn là chỉ số 1.</summary>
    private static async Task<(Guid UserId, string ItemCode)> SeedOneItem(
        AppDbContext db, int dueDaysAgo, int intervalDays, double ease)
    {
        var activity = await SeedLesson(db);
        var userId = Guid.NewGuid();

        var item = new LessonItem
        {
            ActivityId = activity.Id,
            Code = "T-01-Q1",
            OrderIndex = 1,
            PromptJson = """{"Kind":"mcq","Skill":"Reading","Choices":["sai","đúng"],"PromptVi":"Câu hỏi?"}""",
            AnswerJson = """{"answer":1}""",
        };
        db.LessonItems.Add(item);

        db.ReviewQueue.Add(new ReviewQueueItem
        {
            UserId = userId,
            ItemId = item.Id,
            DueAt = Now.AddDays(-dueDaysAgo),
            IntervalDays = intervalDays,
            Ease = ease,
        });

        await db.SaveChangesAsync();
        return (userId, item.Code);
    }
}
