using EnglishForIT.Application.Content;
using EnglishForIT.Domain.Enums;

namespace EnglishForIT.UnitTests.Learning;

/// <summary>
/// Luật của cổng bộ bài luyện viết.
///
/// Tập trung vào những lỗi làm chấm sai điểm mà vẫn trông như đang chạy đúng — loại lỗi
/// khiến học viên viết đúng bị 0 rồi tin rằng mình sai.
/// </summary>
public class WritingSetValidatorTests
{
    private readonly WritingValidator _validator = new();

    [Fact]
    public void BoHopLeThiQua()
    {
        Assert.Empty(_validator.ValidateOne(ValidSet()));
    }

    [Fact]
    public void ChoTrongKhongCoDapAnThiBiChan()
    {
        var set = ValidSet();
        set.Tasks[0].Blanks = [[]];

        var issues = _validator.ValidateOne(set);

        Assert.Contains(issues, i => i.Code == "W014");
    }

    [Fact]
    public void ThuTuHienThiTrungDapAnThiBiChan()
    {
        var set = ValidSet();
        var reorder = set.Tasks[1];
        reorder.Fragments = [.. reorder.CorrectOrder];

        var issues = _validator.ValidateOne(set);

        Assert.Contains(issues, i => i.Code == "W019");
    }

    [Fact]
    public void FragmentsKhacTapVoiCorrectOrderThiBiChan()
    {
        var set = ValidSet();
        set.Tasks[1].Fragments = ["một mảnh lạ", "mảnh nữa", "mảnh thứ ba"];

        var issues = _validator.ValidateOne(set);

        Assert.Contains(issues, i => i.Code == "W018");
    }

    [Fact]
    public void EmailChiCoMotYBatBuocThiBiChan()
    {
        var set = ValidSet();
        set.Tasks[2].RequiredPoints = ["only one"];

        var issues = _validator.ValidateOne(set);

        Assert.Contains(issues, i => i.Code == "W020");
    }

    [Fact]
    public void ThieuCauMauThiBiChan()
    {
        var set = ValidSet();
        set.Tasks[0].SampleEn = string.Empty;

        var issues = _validator.ValidateOne(set);

        Assert.Contains(issues, i => i.Code == "W011");
    }

    [Fact]
    public void MaBoSaiDangThiBiChan()
    {
        var set = ValidSet();
        set.Code = "WRITING-1";

        var issues = _validator.ValidateOne(set);

        Assert.Contains(issues, i => i.Code == "W002");
    }

    [Fact]
    public void DuoiBonBaiThiBiChan()
    {
        var set = ValidSet();
        set.Tasks = [set.Tasks[0]];

        var issues = _validator.ValidateOne(set);

        Assert.Contains(issues, i => i.Code == "W005");
    }

    [Fact]
    public void TrungMaBoGiuaHaiFileThiBiChan()
    {
        var issues = _validator.ValidateSet([ValidSet(), ValidSet()]);

        Assert.Contains(issues, i => i.Code == "W030");
    }

    private static WritingSetDocument ValidSet() => new()
    {
        Code = "WR-TEST",
        TitleVi = "Bộ kiểm thử",
        ContextVi = "Bối cảnh kiểm thử.",
        Track = LearningTrack.Infrastructure,
        Level = CefrLevel.A2,
        Tasks =
        [
            new()
            {
                Code = "t1",
                Kind = WritingTaskKind.FillBlank,
                PromptVi = "Điền chỗ trống.",
                PromptEn = "We are ___ the issue.",
                Blanks = [["investigating"]],
                SampleEn = "We are investigating the issue.",
            },
            new()
            {
                Code = "t2",
                Kind = WritingTaskKind.Reorder,
                PromptVi = "Sắp thứ tự.",
                Fragments = ["by 16:00", "we expect", "a fix"],
                CorrectOrder = ["we expect", "a fix", "by 16:00"],
                SampleEn = "We expect a fix by 16:00.",
            },
            new()
            {
                Code = "t3",
                Kind = WritingTaskKind.GuidedEmail,
                PromptVi = "Viết email.",
                RequiredPoints = ["affected", "next update"],
                SampleEn = "Customers are affected since 09:00 and the next update is at 11:00.",
            },
            new()
            {
                Code = "t4",
                Kind = WritingTaskKind.FillBlank,
                PromptVi = "Điền chỗ trống.",
                PromptEn = "The fix is ___ now.",
                Blanks = [["deploying", "rolling out"]],
                SampleEn = "The fix is deploying now.",
            },
        ],
    };
}
