using EnglishForIT.Application.Learning;
using EnglishForIT.Domain.Enums;

namespace EnglishForIT.UnitTests.Learning;

public class ActivityGraderTests
{
    private static readonly ActivityGrader Grader = new();

    [Theory]
    [InlineData("{\"Answer\":1}")]
    [InlineData("{\"answer\":1}")]
    [InlineData("{\"ANSWER\":1}")]
    public void DocDapAnKhongPhanBietHoaThuong(string answerJson)
    {
        // Bộ ghi dùng PascalCase, JsonDocument tra tên phân biệt hoa thường.
        // Lệch một chữ hoa từng khiến MỌI câu bị chấm sai mà không ném lỗi nào.
        var grade = Grader.GradeMultipleChoice(
            [new ItemResponse("Q1", 1)],
            new Dictionary<string, string> { ["Q1"] = answerJson },
            passScore: 80);

        Assert.Equal(100, grade.Score);
        Assert.True(grade.Passed);
    }

    [Fact]
    public void ChamDungTyLeVaDanhDauTungCau()
    {
        var answers = new Dictionary<string, string>
        {
            ["Q1"] = "{\"Answer\":0}",
            ["Q2"] = "{\"Answer\":1}",
            ["Q3"] = "{\"Answer\":2}",
            ["Q4"] = "{\"Answer\":0}",
        };

        var grade = Grader.GradeMultipleChoice(
            [
                new ItemResponse("Q1", 0),
                new ItemResponse("Q2", 1),
                new ItemResponse("Q3", 0),
                new ItemResponse("Q4", 1),
            ],
            answers,
            passScore: 80);

        Assert.Equal(50, grade.Score);
        Assert.False(grade.Passed);
        Assert.Equal(2, grade.Items.Count(i => i.Correct));
    }

    [Fact]
    public void CauBoTrongTinhLaSai()
    {
        var grade = Grader.GradeMultipleChoice(
            [],
            new Dictionary<string, string> { ["Q1"] = "{\"Answer\":0}" },
            passScore: 80);

        Assert.Equal(0, grade.Score);
        Assert.Equal(-1, grade.Items[0].ChosenIndex);
    }

    [Fact]
    public void BuocKhongCoCauHoiThiCoiNhuXemXongLaDat()
    {
        var grade = Grader.GradeMultipleChoice([], new Dictionary<string, string>(), passScore: 80);

        Assert.Equal(100, grade.Score);
        Assert.True(grade.Passed);
        Assert.True(grade.Graded);
    }

    [Fact]
    public void GopDiemTheoTrucKyNang_LayTrungBinh()
    {
        var scores = Grader.AggregateSkillScores(
        [
            (SkillType.Reading, 100),
            (SkillType.Reading, 50),
            (SkillType.Listening, 80),
        ]);

        Assert.Equal(75, scores[SkillType.Reading]);
        Assert.Equal(80, scores[SkillType.Listening]);
        Assert.False(scores.ContainsKey(SkillType.Speaking));
    }
}

public class WritingGraderTests
{
    private static readonly WritingGrader Grader = new();

    private static WritingRubric FillBlank(params string[][] blanks) =>
        new(WritingTaskKind.FillBlank, blanks, [], [], "as last");

    [Fact]
    public void DienChoTrong_DungHetThiTramDiem()
    {
        var grade = Grader.Grade(FillBlank(["as"], ["last"]), ["as", "last"], 80);

        Assert.Equal(100, grade.Score);
        Assert.True(grade.Passed);
    }

    [Fact]
    public void DienChoTrong_KhongPhanBietHoaThuongVaDauCau()
    {
        var grade = Grader.Grade(FillBlank(["as"], ["last"]), ["As,", " LAST. "], 80);

        Assert.Equal(100, grade.Score);
    }

    [Fact]
    public void DienChoTrong_SaiChinhTaNheVanDuocDiemMotPhan()
    {
        // Mục tiêu là dùng đúng từ, không phải thi chính tả. Sai một ký tự
        // trên từ dài không nên bị 0 điểm.
        var grade = Grader.Grade(FillBlank(["confirm"]), ["confrim"], 80);

        Assert.Equal(80, grade.Score);
        Assert.Contains("chính tả", grade.FeedbackVi, StringComparison.Ordinal);
    }

    [Fact]
    public void DienChoTrong_TuHoanToanKhacThiKhongDiem()
    {
        var grade = Grader.Grade(FillBlank(["confirm"]), ["banana"], 80);

        Assert.Equal(0, grade.Score);
        Assert.False(grade.Passed);
    }

    [Fact]
    public void DienChoTrong_ChapNhanNhieuDapAn()
    {
        var grade = Grader.Grade(FillBlank(["thanks", "thank you"]), ["thank you"], 80);

        Assert.Equal(100, grade.Score);
    }

    [Fact]
    public void BoTrongThiKhongDiem()
    {
        var grade = Grader.Grade(FillBlank(["as"]), [""], 80);

        Assert.Equal(0, grade.Score);
        Assert.Contains("chưa viết gì", grade.FeedbackVi, StringComparison.Ordinal);
    }

    [Fact]
    public void SapCau_DungThuTuThiTramDiem()
    {
        var rubric = new WritingRubric(
            WritingTaskKind.Reorder, [], ["Could", "you", "speak", "more", "slowly"], [],
            "Could you speak more slowly?");

        var grade = Grader.Grade(rubric, ["Could", "you", "speak", "more", "slowly"], 80);

        Assert.Equal(100, grade.Score);
    }

    [Fact]
    public void SapCau_SaiMotManhVanGiuDuocPhanDungConLai()
    {
        // Chấm theo cặp liền kề: đặt sai một mảnh không xoá sạch công sắp đúng phần còn lại.
        var rubric = new WritingRubric(
            WritingTaskKind.Reorder, [], ["a", "b", "c", "d", "e"], [], "a b c d e");

        var grade = Grader.Grade(rubric, ["a", "b", "c", "e", "d"], 80);

        Assert.InRange(grade.Score, 40, 60);
        Assert.False(grade.Passed);
    }

    [Fact]
    public void SapCau_ThieuManhThiBaoRoSoLuong()
    {
        var rubric = new WritingRubric(WritingTaskKind.Reorder, [], ["a", "b", "c"], [], "a b c");

        var grade = Grader.Grade(rubric, ["a", "b"], 80);

        Assert.Equal(0, grade.Score);
        Assert.Contains("3", grade.FeedbackVi, StringComparison.Ordinal);
    }

    [Fact]
    public void EmailCoHuongDan_DuYThiDat()
    {
        var rubric = new WritingRubric(
            WritingTaskKind.GuidedEmail, [], [],
            ["portal is down", "since two", "twenty users"],
            "The portal is down since two, twenty users affected.");

        var grade = Grader.Grade(rubric,
            ["Hi Linh, the portal is down since two this morning and about twenty users cannot log in."],
            80);

        Assert.True(grade.Passed);
    }

    [Fact]
    public void EmailCoHuongDan_ThieuYThiBaoThieuMay()
    {
        var rubric = new WritingRubric(
            WritingTaskKind.GuidedEmail, [], [],
            ["portal is down", "since two", "twenty users"],
            "mau");

        var grade = Grader.Grade(rubric, ["The portal is down."], 80);

        Assert.False(grade.Passed);
        Assert.Contains("Thiếu", grade.FeedbackVi, StringComparison.Ordinal);
    }

    [Fact]
    public void EmailCoHuongDan_DuYNhungQuaNganThiBiTru()
    {
        var rubric = new WritingRubric(
            WritingTaskKind.GuidedEmail, [], [],
            ["portal down", "since two", "twenty users"],
            "mau");

        // Đủ từ khoá nhưng không thành câu thì chưa phải một email.
        var grade = Grader.Grade(rubric, ["portal down since two twenty users"], 80);

        Assert.Equal(80, grade.Score);
        Assert.Contains("quá ngắn", grade.FeedbackVi, StringComparison.Ordinal);
    }
}
