using EnglishForIT.Application.Learning;
using EnglishForIT.Domain.Enums;

namespace EnglishForIT.UnitTests.Learning;

public class PrerequisiteEngineTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 18, 9, 0, 0, TimeSpan.Zero);

    private static PrerequisiteEngine Engine(Action<LearningPolicyOptions>? tweak = null)
    {
        var options = new LearningPolicyOptions();
        tweak?.Invoke(options);
        return new PrerequisiteEngine(options);
    }

    private static LessonNode Node(
        string code,
        params PrerequisiteEdge[] prerequisites) => new(
        Guid.CreateVersion7(),
        code,
        CefrLevel.PreA1,
        ContextLayer.Life,
        LearningTrack.Foundation,
        0,
        false,
        [SkillType.Listening, SkillType.Speaking],
        prerequisites);

    private static MasterySnapshot Snapshot(
        string code,
        double mastery,
        LessonState state = LessonState.InProgress,
        double listening = 80,
        double speaking = 80,
        DateTimeOffset? masteredAt = null) => new(
        Guid.CreateVersion7(),
        code,
        state,
        mastery,
        new Dictionary<SkillType, double>
        {
            [SkillType.Listening] = listening,
            [SkillType.Speaking] = speaking,
        },
        masteredAt);

    // ---------------------------------------------------------------------
    // Mastery
    // ---------------------------------------------------------------------

    [Fact]
    public void ComputeMasteryRaw_TinhTrungBinhCoTrongSo()
    {
        var scores = new Dictionary<SkillType, double>
        {
            [SkillType.Listening] = 90,
            [SkillType.Speaking] = 50,
        };

        var weights = new Dictionary<SkillType, double>
        {
            [SkillType.Listening] = 0.7,
            [SkillType.Speaking] = 0.3,
        };

        Assert.Equal(78, Engine().ComputeMasteryRaw(scores, weights));
    }

    [Fact]
    public void ComputeMasteryRaw_BaiChiDayHaiKyNangVanDatDuocMotTram()
    {
        // Nếu chia cho tổng bốn kỹ năng thì bài chỉ dạy nghe và nói mãi mãi không đạt 100,
        // và học viên sẽ không bao giờ mở được bài kế.
        var scores = new Dictionary<SkillType, double>
        {
            [SkillType.Listening] = 100,
            [SkillType.Speaking] = 100,
        };

        var weights = new Dictionary<SkillType, double>
        {
            [SkillType.Listening] = 0.5,
            [SkillType.Speaking] = 0.5,
        };

        Assert.Equal(100, Engine().ComputeMasteryRaw(scores, weights));
    }

    [Fact]
    public void EffectiveMastery_TuanDauKhongSuyGiam()
    {
        var engine = Engine();
        var masteredAt = Now.AddDays(-5);

        Assert.Equal(90, engine.EffectiveMastery(90, masteredAt, Now));
    }

    [Fact]
    public void EffectiveMastery_DeLauThiGiamNhungCoSan()
    {
        var engine = Engine();

        var afterMonth = engine.EffectiveMastery(90, Now.AddDays(-37), Now);
        var afterYear = engine.EffectiveMastery(90, Now.AddDays(-365), Now);

        Assert.True(afterMonth < 90, "Để một tháng không ôn thì phải giảm.");
        // Sàn 60% giữ cho một bài đã học không bao giờ tụt xuống mức khoá lại bài sau nó.
        Assert.Equal(54, afterYear);
    }

    // ---------------------------------------------------------------------
    // Ngưỡng riêng từng kỹ năng
    // ---------------------------------------------------------------------

    [Fact]
    public void SkillsBelowThreshold_ChiBaoKyNangMaBaiThucSuDay()
    {
        var scores = new Dictionary<SkillType, double>
        {
            [SkillType.Listening] = 90,
            [SkillType.Speaking] = 40,
            [SkillType.Writing] = 10,
        };

        var below = Engine().SkillsBelowThreshold(scores, [SkillType.Listening, SkillType.Speaking]);

        // Writing thấp nhưng bài không dạy viết, nên không được tính là thiếu.
        Assert.Equal([SkillType.Speaking], below);
    }

    [Fact]
    public void SkillsBelowThreshold_KyNangChuaCoDiemThiKhongTinhLaYeu()
    {
        // "Chưa có dữ liệu" khác "làm và bị điểm thấp". Gộp hai thứ này khiến bài chưa học
        // lần nào bị báo yếu cả bốn trục, và kỹ năng chưa có bộ chấm khiến không bài nào qua nổi.
        var scores = new Dictionary<SkillType, double> { [SkillType.Listening] = 90 };

        var below = Engine().SkillsBelowThreshold(scores, [SkillType.Listening, SkillType.Speaking]);

        Assert.Empty(below);
    }

    [Fact]
    public void DiemTongCaoNhungTrucNoiYeuThiChuaThao()
    {
        // Đây là kịch bản cày quiz để qua bài mà không bao giờ mở miệng nói.
        var lesson = Node("L2", new PrerequisiteEdge("L1", 60, PrerequisiteKind.Hard));

        var progress = new Dictionary<string, MasterySnapshot>
        {
            ["L1"] = Snapshot("L1", 90, LessonState.Mastered),
            ["L2"] = Snapshot("L2", 88, LessonState.InProgress, listening: 95, speaking: 40),
        };

        var result = Engine().Evaluate(lesson, progress, new HashSet<string>(), Now);

        Assert.Equal(LessonState.InProgress, result.State);
        Assert.Equal(LessonStateReason.SkillBelowThreshold, result.Reason);
        Assert.Contains(SkillType.Speaking, result.SkillsBelowThreshold);
    }

    // ---------------------------------------------------------------------
    // Khoá và mở
    // ---------------------------------------------------------------------

    [Fact]
    public void BaiKhongCoTienQuyetThiMoNgay()
    {
        var result = Engine().Evaluate(Node("L1"), new Dictionary<string, MasterySnapshot>(), new HashSet<string>(), Now);

        Assert.Equal(LessonState.Available, result.State);
    }

    [Fact]
    public void TienQuyetCungChuaDatThiKhoa()
    {
        var lesson = Node("L2", new PrerequisiteEdge("L1", 60, PrerequisiteKind.Hard));

        var result = Engine().Evaluate(lesson, new Dictionary<string, MasterySnapshot>(), new HashSet<string>(), Now);

        Assert.Equal(LessonState.Locked, result.State);
        Assert.Equal(LessonStateReason.PrerequisiteNotMet, result.Reason);
        Assert.Single(result.Unmet);
        Assert.Equal(60, result.Unmet[0].Gap);
    }

    [Fact]
    public void ConThieuItThiChoXemTruocChuKhongKhoaHan()
    {
        // Thấy được đích đến thì có động lực học nốt bài trước.
        var lesson = Node("L2", new PrerequisiteEdge("L1", 60, PrerequisiteKind.Hard));

        var progress = new Dictionary<string, MasterySnapshot> { ["L1"] = Snapshot("L1", 50) };

        var result = Engine().Evaluate(lesson, progress, new HashSet<string>(), Now);

        Assert.Equal(LessonState.Previewable, result.State);
    }

    [Fact]
    public void TienQuyetMemChiCanhBaoChuKhongChan()
    {
        var lesson = Node("L2", new PrerequisiteEdge("L1", 60, PrerequisiteKind.Soft));

        var result = Engine().Evaluate(lesson, new Dictionary<string, MasterySnapshot>(), new HashSet<string>(), Now);

        Assert.Equal(LessonState.Available, result.State);
        Assert.Single(result.Unmet);
    }

    [Fact]
    public void ThiVuotQuaThiMoDuChuaDatTienQuyet()
    {
        var lesson = Node("L4", new PrerequisiteEdge("L3", 60, PrerequisiteKind.Hard));

        var result = Engine().Evaluate(
            lesson,
            new Dictionary<string, MasterySnapshot>(),
            new HashSet<string> { "L4" },
            Now);

        Assert.Equal(LessonState.Available, result.State);
        Assert.Equal(LessonStateReason.ChallengePassed, result.Reason);
    }

    [Fact]
    public void BaiDaThaoDeLauThiChuyenSangCanOnChuKhongBiKhoaLai()
    {
        // Khoá lại một bài đã học xong là cách nhanh nhất làm học viên bỏ cuộc.
        var lesson = Node("L1");

        var progress = new Dictionary<string, MasterySnapshot>
        {
            ["L1"] = Snapshot("L1", 82, LessonState.Mastered, masteredAt: Now.AddDays(-120)),
        };

        var result = Engine().Evaluate(lesson, progress, new HashSet<string>(), Now);

        Assert.Equal(LessonState.NeedsReview, result.State);
        Assert.Equal(LessonStateReason.RetentionDecay, result.Reason);
    }

    [Fact]
    public void ExplainLock_NoiRoConThieuBaoNhieu()
    {
        var lesson = Node("L2", new PrerequisiteEdge("L1", 60, PrerequisiteKind.Hard));

        var progress = new Dictionary<string, MasterySnapshot> { ["L1"] = Snapshot("L1", 25) };

        var engine = Engine();
        var text = engine.ExplainLock(engine.Evaluate(lesson, progress, new HashSet<string>(), Now));

        // Học viên phải thấy con số, không phải câu "chưa đủ điều kiện".
        Assert.Contains("L1", text, StringComparison.Ordinal);
        Assert.Contains("60", text, StringComparison.Ordinal);
        Assert.Contains("25", text, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------
    // Chọn bài kế
    // ---------------------------------------------------------------------

    [Fact]
    public void ChonBaiKe_UuTienBaiDangHocDoTruoc()
    {
        var l1 = Node("L1");
        var l2 = Node("L2");
        var nodes = new List<LessonNode> { l1, l2 };

        var evaluations = new Dictionary<string, LessonEvaluation>
        {
            ["L1"] = new("L1", LessonState.InProgress, LessonStateReason.PrerequisiteMet, [], []),
            ["L2"] = new("L2", LessonState.Available, LessonStateReason.PrerequisiteMet, [], []),
        };

        var choice = Engine().ChooseNext(
            nodes, evaluations, new Dictionary<string, MasterySnapshot>(),
            ContextLayer.Life, CefrLevel.PreA1, LearningTrack.Foundation);

        Assert.Equal("L1", choice!.Lesson.Code);
        Assert.Contains("đang học dở", choice.ReasonVi, StringComparison.Ordinal);
    }

    [Fact]
    public void ChonBaiKe_TraNoOnTapTruocKhiMoBaiMoi()
    {
        var nodes = new List<LessonNode> { Node("L1"), Node("L2") };

        var evaluations = new Dictionary<string, LessonEvaluation>
        {
            ["L1"] = new("L1", LessonState.NeedsReview, LessonStateReason.RetentionDecay, [], []),
            ["L2"] = new("L2", LessonState.Available, LessonStateReason.PrerequisiteMet, [], []),
        };

        var choice = Engine().ChooseNext(
            nodes, evaluations, new Dictionary<string, MasterySnapshot>(),
            ContextLayer.Life, CefrLevel.PreA1, LearningTrack.Foundation);

        Assert.Equal("L1", choice!.Lesson.Code);
    }

    [Fact]
    public void ChonBaiKe_KhongConBaiNaoMoThiTraNull()
    {
        var nodes = new List<LessonNode> { Node("L1") };

        var evaluations = new Dictionary<string, LessonEvaluation>
        {
            ["L1"] = new("L1", LessonState.Locked, LessonStateReason.PrerequisiteNotMet, [], []),
        };

        var choice = Engine().ChooseNext(
            nodes, evaluations, new Dictionary<string, MasterySnapshot>(),
            ContextLayer.Life, CefrLevel.PreA1, LearningTrack.Foundation);

        Assert.Null(choice);
    }
}
