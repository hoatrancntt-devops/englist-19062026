using EnglishForIT.Application.Learning;
using EnglishForIT.Domain.Entities.Identity;
using EnglishForIT.Domain.Entities.Progress;
using EnglishForIT.Domain.Enums;
using EnglishForIT.Infrastructure.Learning;
using EnglishForIT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EnglishForIT.UnitTests.Learning;

/// <summary>
/// Chuỗi ngày.
///
/// Chuỗi là thứ học viên nhìn mỗi ngày và mất một lần là mất lòng tin. Luật ở đây khắt khe
/// hơn cách đếm thông thường (đủ phút VÀ chạm đủ bốn kỹ năng), nên phần lớn test dưới đây
/// kiểm chính các trường hợp chuỗi KHÔNG được tăng.
/// </summary>
public class StreakServiceTests
{
    /// <summary>
    /// Dùng thời điểm THẬT chứ không phải mốc cố định.
    ///
    /// <c>CreatedAt</c> cố ý bất biến: AppDbContext gán lúc thêm mới rồi chặn mọi lần sửa sau đó.
    /// Đó là thiết kế đúng, nên test không gieo được bản ghi vào quá khứ. Thay vào đó test dựng
    /// dữ liệu của hôm nay và diễn tả mọi kỳ vọng theo ngày tương đối.
    /// </summary>
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static DateOnly TodayIn(string timeZone) => LocalDay.DateFor(Now, timeZone);

    [Fact]
    public async Task DuPhutVaDuBonKyNangThiChuoiTang()
    {
        var db = NewDb();
        var userId = await SeedUserAsync(db);
        await SeedDayAsync(db, userId, minutes: 45, AllSkills);

        var day = await NewService(db).RecordStudyAsync(userId, Now);

        Assert.True(day.CountsTowardStreak);
        Assert.Equal(1, (await db.Streaks.SingleAsync()).CurrentStreak);
    }

    [Fact]
    public async Task ThieuPhutThiKhongTinh()
    {
        var db = NewDb();
        var userId = await SeedUserAsync(db);
        await SeedDayAsync(db, userId, minutes: 20, AllSkills);

        var day = await NewService(db).RecordStudyAsync(userId, Now);

        Assert.False(day.CountsTowardStreak);
        Assert.Contains("thiếu 25 phút", day.ReasonVi);

        // Ngày không tính thì không đụng gì tới chuỗi, kể cả tạo dòng.
        Assert.Empty(await db.Streaks.ToListAsync());
    }

    [Fact]
    public async Task HocMotKyNangThiKhongGiuDuocChuoi()
    {
        var db = NewDb();
        var userId = await SeedUserAsync(db);
        await SeedDayAsync(db, userId, minutes: 60, [SkillType.Reading]);

        var day = await NewService(db).RecordStudyAsync(userId, Now);

        // Hệ quả có chủ đích của chế độ đơn kỹ năng. Giao diện phải nói thẳng lý do.
        Assert.False(day.CountsTowardStreak);
        Assert.Contains("Nghe", day.ReasonVi);
        Assert.Contains("Nói", day.ReasonVi);
    }

    [Fact]
    public async Task BuocNoiChuaChamDuocVanTinhLaDaCham()
    {
        var db = NewDb();
        var userId = await SeedUserAsync(db);

        // Ba kỹ năng có điểm, riêng Nói chỉ có bản ghi "đã làm".
        await SeedDayAsync(db, userId, minutes: 45,
            [SkillType.Listening, SkillType.Reading, SkillType.Writing]);

        db.ActivityAttempts.Add(new ActivityAttempt
        {
            UserId = userId,
            LessonAttemptId = Guid.NewGuid(),
            ActivityId = Guid.NewGuid(),
            Kind = ActivityKind.Speak,
            Skill = SkillType.Speaking,
            Graded = false,
            Score = 0,
            DurationSeconds = 60,
            ResultJson = "{}",
        });
        await db.SaveChangesAsync();

        var day = await NewService(db).RecordStudyAsync(userId, Now);

        // Đòi Nói phải CÓ ĐIỂM thì không ai xây được chuỗi cho tới khi có bộ chấm phát âm.
        Assert.True(day.CountsTowardStreak);
        Assert.Contains(SkillType.Speaking, day.SkillsTouched);
    }

    [Fact]
    public async Task GoiHaiLanTrongCungNgayKhongCongHaiLan()
    {
        var db = NewDb();
        var userId = await SeedUserAsync(db);
        await SeedDayAsync(db, userId, minutes: 45, AllSkills);

        var service = NewService(db);
        await service.RecordStudyAsync(userId, Now);
        await service.RecordStudyAsync(userId, Now.AddHours(1));

        Assert.Equal(1, (await db.Streaks.SingleAsync()).CurrentStreak);
    }

    [Fact]
    public async Task HocLienTiepHaiNgayThiChuoiLenHai()
    {
        var db = NewDb();
        var userId = await SeedUserAsync(db);

        db.Streaks.Add(new Streak
        {
            UserId = userId,
            CurrentStreak = 1,
            LongestStreak = 1,
            LastStudyDateLocal = TodayIn("Asia/Ho_Chi_Minh").AddDays(-1),   // hôm qua đã học
        });
        await db.SaveChangesAsync();

        await SeedDayAsync(db, userId, minutes: 45, AllSkills);
        await NewService(db).RecordStudyAsync(userId, Now);

        var streak = await db.Streaks.SingleAsync();
        Assert.Equal(2, streak.CurrentStreak);
        Assert.Equal(2, streak.LongestStreak);
    }

    [Fact]
    public async Task NghiMotNgayNhungConVeThiChuoiKhongDut()
    {
        var db = NewDb();
        var userId = await SeedUserAsync(db);

        db.Streaks.Add(new Streak
        {
            UserId = userId,
            CurrentStreak = 9,
            LongestStreak = 9,
            FreezeTokens = 2,
            LastStudyDateLocal = TodayIn("Asia/Ho_Chi_Minh").AddDays(-2),   // nghỉ đúng một ngày
        });
        await db.SaveChangesAsync();

        await SeedDayAsync(db, userId, minutes: 45, AllSkills);
        await NewService(db).RecordStudyAsync(userId, Now);

        var streak = await db.Streaks.SingleAsync();
        Assert.Equal(10, streak.CurrentStreak);
        Assert.Equal(1, streak.FreezeTokens);
    }

    [Fact]
    public async Task NghiQuaNhieuNgayThiChuoiVeMot()
    {
        var db = NewDb();
        var userId = await SeedUserAsync(db);

        db.Streaks.Add(new Streak
        {
            UserId = userId,
            CurrentStreak = 30,
            LongestStreak = 30,
            FreezeTokens = 1,
            LastStudyDateLocal = TodayIn("Asia/Ho_Chi_Minh").AddDays(-5),   // nghỉ bốn ngày, chỉ một vé
        });
        await db.SaveChangesAsync();

        await SeedDayAsync(db, userId, minutes: 45, AllSkills);
        await NewService(db).RecordStudyAsync(userId, Now);

        var streak = await db.Streaks.SingleAsync();
        Assert.Equal(1, streak.CurrentStreak);
        Assert.Equal(1, streak.FreezeTokens);   // Không đủ vé thì không tiêu vé nào.
        Assert.Equal(30, streak.LongestStreak); // Kỷ lục cũ phải giữ nguyên.
    }

    [Fact]
    public async Task NguoiOMuiGioKhacDuocTinhTheoNgayCuaHo()
    {
        var db = NewDb();

        var userId = await SeedUserAsync(db, "America/Los_Angeles");
        await SeedDayAsync(db, userId, minutes: 45, AllSkills);

        var day = await NewService(db).RecordStudyAsync(userId, Now);

        // Ngày phải theo lịch Los Angeles. Chênh Việt Nam 14-15 tiếng nên hai bên rất hay khác ngày;
        // trong chế độ invariant globalization thì cả hai đều rơi về UTC và test này bắt được.
        Assert.Equal(TodayIn("America/Los_Angeles"), day.DateLocal);
    }

    [Fact]
    public async Task MuiGioHongTrongHoSoKhongLamHongCaHam()
    {
        var db = NewDb();
        var userId = await SeedUserAsync(db, "Khong/Co-That");
        await SeedDayAsync(db, userId, minutes: 45, AllSkills);

        var day = await NewService(db).RecordStudyAsync(userId, Now);

        // Rơi về múi giờ mặc định thay vì ném lỗi: một hồ sơ hỏng không được làm
        // job nhắc học dừng cho toàn bộ học viên còn lại.
        Assert.True(day.CountsTowardStreak);
    }

    // -----------------------------------------------------------------------

    private static readonly SkillType[] AllSkills =
        [SkillType.Listening, SkillType.Speaking, SkillType.Reading, SkillType.Writing];

    private static AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"streak-{Guid.NewGuid()}")
            .Options);

    private static StreakService NewService(AppDbContext db) =>
        new(db, Options.Create(new LearningPolicyOptions()), NullLogger<StreakService>.Instance);

    private static async Task<Guid> SeedUserAsync(AppDbContext db, string timeZone = "Asia/Ho_Chi_Minh")
    {
        var userId = Guid.CreateVersion7();

        db.UserProfiles.Add(new UserProfile
        {
            UserId = userId,
            TimeZone = timeZone,
            DailyMinutesTarget = 45,
        });

        await db.SaveChangesAsync();
        return userId;
    }

    /// <summary>Dựng các bước đã làm trong ngày, chia đều số phút cho từng kỹ năng.</summary>
    private static async Task SeedDayAsync(
        AppDbContext db, Guid userId, int minutes, IReadOnlyList<SkillType> skills)
    {
        var secondsEach = minutes * 60 / skills.Count;

        foreach (var skill in skills)
        {
            db.ActivityAttempts.Add(new ActivityAttempt
            {
                UserId = userId,
                LessonAttemptId = Guid.NewGuid(),
                ActivityId = Guid.NewGuid(),
                Kind = ActivityKind.Quiz,
                Skill = skill,
                Graded = true,
                Score = 100,
                Passed = true,
                DurationSeconds = secondsEach,
                ResultJson = "{}",
            });
        }

        await db.SaveChangesAsync();
    }
}
