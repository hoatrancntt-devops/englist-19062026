using EnglishForIT.Application.Content;
using EnglishForIT.Domain.Enums;

namespace EnglishForIT.UnitTests.Learning;

/// <summary>
/// Cổng chất lượng đề xếp lớp.
///
/// Test ở đây chứng minh từng luật thật sự chặn được đề hỏng. Chỉ chạy cổng trên
/// nội dung tốt rồi thấy xanh thì không kết luận được gì: một luật viết sai
/// cũng cho ra đúng kết quả đó.
/// </summary>
public class PlacementValidatorTests
{
    private static readonly PlacementValidator Validator = new();

    private static PlacementItemDocument Mcq(string code, int correctIndex, SkillType? skill = SkillType.Reading) =>
        new()
        {
            Code = code,
            Kind = PlacementItemKind.Mcq,
            Skill = skill,
            Difficulty = 2,
            Prompt = new PlacementPromptDocument
            {
                InstructionVi = "Chọn câu đúng.",
                Choices = ["a", "b", "c", "d"],
            },
            Answer = new PlacementAnswerDocument { CorrectIndex = correctIndex },
        };

    private static PlacementDocument FormWith(params PlacementItemDocument[] items) =>
        new()
        {
            Code = "T",
            TitleVi = "Đề thử",
            Items = [.. items],
        };

    [Fact]
    public void BatDuocDapAnDonVeMotViTri()
    {
        // Sáu trên tám câu cùng đáp án ở ô đầu: chọn mãi ô đầu là qua được đề.
        var doc = FormWith(
            Mcq("q1", 0), Mcq("q2", 0), Mcq("q3", 0), Mcq("q4", 0),
            Mcq("q5", 0), Mcq("q6", 0), Mcq("q7", 1), Mcq("q8", 2));

        var issues = Validator.ValidateOne(doc);

        Assert.Contains(issues, i => i.Code == "P025");
    }

    [Fact]
    public void DapAnTraiDeuThiKhongKeu()
    {
        var doc = FormWith(
            Mcq("q1", 0), Mcq("q2", 1), Mcq("q3", 2), Mcq("q4", 3),
            Mcq("q5", 0), Mcq("q6", 2), Mcq("q7", 1), Mcq("q8", 3));

        var issues = Validator.ValidateOne(doc);

        Assert.DoesNotContain(issues, i => i.Code == "P025");
    }

    [Fact]
    public void BatDuocCauLikertBiGanTrucKyNang()
    {
        var doc = FormWith(new PlacementItemDocument
        {
            Code = "self",
            Kind = PlacementItemKind.Likert,
            Skill = SkillType.Speaking,
            Difficulty = 1,
            Prompt = new PlacementPromptDocument { InstructionVi = "Tự đánh giá", Choices = ["a", "b"] },
        });

        Assert.Contains(Validator.ValidateOne(doc), i => i.Code == "P009");
    }

    [Fact]
    public void BatDuocCorrectIndexNamNgoaiSoLuaChon()
    {
        Assert.Contains(Validator.ValidateOne(FormWith(Mcq("q1", 9))), i => i.Code == "P011");
    }

    [Fact]
    public void BatDuocCauDienChoTrongKhongCoChoTrong()
    {
        var doc = FormWith(new PlacementItemDocument
        {
            Code = "w1",
            Kind = PlacementItemKind.FillBlank,
            Skill = SkillType.Writing,
            Difficulty = 2,
            Prompt = new PlacementPromptDocument
            {
                InstructionVi = "Điền vào chỗ trống.",
                SentenceEn = "I am working now.",
            },
            Answer = new PlacementAnswerDocument { Accepted = ["am"] },
        });

        Assert.Contains(Validator.ValidateOne(doc), i => i.Code == "P017");
    }

    [Fact]
    public void BatDuocEmailCoSoYKhongKhopSoTuKhoaCham()
    {
        // Học viên viết đủ ba ý được yêu cầu nhưng bộ chấm dò bốn từ khoá:
        // mất điểm vì một lý do không ai nói cho họ biết.
        var doc = FormWith(new PlacementItemDocument
        {
            Code = "e1",
            Kind = PlacementItemKind.GuidedEmail,
            Skill = SkillType.Writing,
            Difficulty = 5,
            Prompt = new PlacementPromptDocument
            {
                InstructionVi = "Viết email.",
                ScenarioVi = "Máy in hỏng.",
                RequiredPointsVi = ["ý một", "ý hai", "ý ba"],
            },
            Answer = new PlacementAnswerDocument { MustContain = ["a", "b", "c", "d"] },
        });

        Assert.Contains(Validator.ValidateOne(doc), i => i.Code == "P019");
    }

    [Fact]
    public void BatDuocDeThieuSoCau()
    {
        Assert.Contains(Validator.ValidateOne(FormWith(Mcq("q1", 0))), i => i.Code == "P003");
    }

    [Fact]
    public void BatDuocChiCoMotDeDangBat()
    {
        // Một đề duy nhất thì người thi lại gặp nguyên đề cũ và điểm tăng vì nhớ đáp án.
        var issues = Validator.ValidateSet([FormWith(Mcq("q1", 0))]);

        Assert.Contains(issues, i => i.Code == "P031");
    }
}
