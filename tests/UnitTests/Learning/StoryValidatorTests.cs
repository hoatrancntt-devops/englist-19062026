using EnglishForIT.Application.Content;
using EnglishForIT.Domain.Enums;

namespace EnglishForIT.UnitTests.Learning;

/// <summary>
/// Luật của cổng chương truyện.
///
/// Tập trung vào ba lỗi không ai báo được: chương khoá vĩnh viễn vì mốc sai, hai chương
/// bật cùng lúc rồi im bặt, và số chương thủng lỗ giữa danh sách.
/// </summary>
public class StoryValidatorTests
{
    private readonly StoryValidator _validator = new();

    [Fact]
    public void MocMoTroToiBaiKhongTonTaiThiBiChan()
    {
        var doc = Valid("ST-01", 1, unlockAfter: "KHONG-CO-01");

        var issues = _validator.ValidateSet([doc], new HashSet<string> { "LIFE-04" });

        Assert.Contains(issues, i => i.Code == "S024");
    }

    [Fact]
    public void MocMoCoThatThiQua()
    {
        var doc = Valid("ST-01", 1, unlockAfter: "LIFE-04");

        var issues = _validator.ValidateSet([doc], new HashSet<string> { "LIFE-04" });

        Assert.Empty(issues);
    }

    [Fact]
    public void HaiChuongCungMocMoThiBiChan()
    {
        var a = Valid("ST-01", 1, unlockAfter: "LIFE-04");
        var b = Valid("ST-02", 2, unlockAfter: "LIFE-04");

        var issues = _validator.ValidateSet([a, b], new HashSet<string> { "LIFE-04" });

        Assert.Contains(issues, i => i.Code == "S023");
    }

    [Fact]
    public void SoChuongThungLoGiuaChungThiBiChan()
    {
        var a = Valid("ST-01", 1, unlockAfter: "LIFE-04");
        var c = Valid("ST-03", 3, unlockAfter: "LIFE-09");

        var issues = _validator.ValidateSet([a, c], new HashSet<string> { "LIFE-04", "LIFE-09" });

        Assert.Contains(issues, i => i.Code == "S022");
    }

    [Fact]
    public void TrungSoThuTuThiBiChan()
    {
        var a = Valid("ST-01", 1, unlockAfter: "LIFE-04");
        var b = Valid("ST-02", 1, unlockAfter: "LIFE-09");

        var issues = _validator.ValidateSet([a, b], new HashSet<string> { "LIFE-04", "LIFE-09" });

        Assert.Contains(issues, i => i.Code == "S021");
    }

    [Fact]
    public void ThanChuongQuaNganThiBiChan()
    {
        var doc = Valid("ST-01", 1, unlockAfter: "LIFE-04");
        doc.BodyVi = "Ngắn quá.";

        var issues = _validator.ValidateOne(doc);

        Assert.Contains(issues, i => i.Code == "S006");
    }

    [Fact]
    public void NhanVatThieuVaiThiBiChan()
    {
        var doc = Valid("ST-01", 1, unlockAfter: "LIFE-04");
        doc.NewCharacters = ["Mai"];

        var issues = _validator.ValidateOne(doc);

        Assert.Contains(issues, i => i.Code == "S010");
    }

    [Fact]
    public void KhongTruyenDanhSachBaiThiBoQuaPhepTraMoc()
    {
        var doc = Valid("ST-01", 1, unlockAfter: "KHONG-CO-01");

        var issues = _validator.ValidateSet([doc]);

        Assert.DoesNotContain(issues, i => i.Code == "S024");
    }

    private static StoryDocument Valid(string code, int number, string unlockAfter) => new()
    {
        Code = code,
        Number = number,
        TitleVi = "Chương kiểm thử",
        Track = LearningTrack.Foundation,
        UnlockAfterLesson = unlockAfter,
        HookVi = "Một câu mở có sức kéo.",
        BodyVi = new string('a', 400),
        EndsVi = "Câu kết nối sang bài sau.",
        NewCharacters = ["Mai — kỹ sư cùng nhóm"],
    };
}
