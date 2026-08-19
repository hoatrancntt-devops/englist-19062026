using EnglishForIT.Application.Content;
using EnglishForIT.Application.Learning;
using EnglishForIT.Domain.Enums;

namespace EnglishForIT.UnitTests.Learning;

/// <summary>
/// Bộ chấm và xếp bậc của bài xếp lớp.
///
/// Đây là chỗ sai thì không ai thấy ngay: học viên bị đặt nhầm bậc vẫn học được,
/// chỉ là học sai chỗ hàng tuần rồi bỏ. Nên từng luật một phải có test riêng.
/// </summary>
public class PlacementScoringTests
{
    private static PlacementScoring.Response Choose(int index) => new(index, null);

    private static PlacementScoring.Response Write(string text) => new(null, text);

    // ---------------------------------------------------------------
    // Chấm từng câu
    // ---------------------------------------------------------------

    [Fact]
    public void ChonDungThi100_ChonSaiThi0()
    {
        var answer = new PlacementAnswerDocument { CorrectIndex = 2 };

        Assert.Equal(100, PlacementScoring.Grade(PlacementItemKind.Mcq, answer, Choose(2)));
        Assert.Equal(0, PlacementScoring.Grade(PlacementItemKind.Mcq, answer, Choose(0)));
    }

    [Fact]
    public void KhongTraLoiThi0()
    {
        var answer = new PlacementAnswerDocument { CorrectIndex = 1 };

        Assert.Equal(0, PlacementScoring.Grade(PlacementItemKind.McqRead, answer, new PlacementScoring.Response(null, null)));
    }

    [Fact]
    public void CauLikertVaCauNoiTraVeNullChuKhongPhai0()
    {
        // Ép null về 0 sẽ kéo trục Nói của mọi học viên xuống đáy, và họ bị đặt
        // thấp hơn thực lực một bậc mà không có gì giải thích.
        var answer = new PlacementAnswerDocument();

        Assert.Null(PlacementScoring.Grade(PlacementItemKind.Likert, answer, Choose(3)));
        Assert.Null(PlacementScoring.Grade(PlacementItemKind.ReadAloud, answer, Write("hello")));
        Assert.Null(PlacementScoring.Grade(PlacementItemKind.Repeat, answer, Write("hello")));
    }

    [Fact]
    public void DienChoTrongBoQuaHoaVaDauCau()
    {
        var answer = new PlacementAnswerDocument { Accepted = ["am"] };

        Assert.Equal(100, PlacementScoring.Grade(PlacementItemKind.FillBlank, answer, Write("  AM  ")));
        Assert.Equal(100, PlacementScoring.Grade(PlacementItemKind.FillBlank, answer, Write("am.")));
    }

    [Fact]
    public void DienChoTrongChapNhanGoNhamMotKyTu()
    {
        // Đo việc chọn đúng từ, không đo chính tả: "recieved" không nên bị 0 điểm.
        var answer = new PlacementAnswerDocument { Accepted = ["received"] };

        Assert.Equal(80, PlacementScoring.Grade(PlacementItemKind.FillBlank, answer, Write("recieved")));
        Assert.Equal(0, PlacementScoring.Grade(PlacementItemKind.FillBlank, answer, Write("rejected")));
    }

    [Fact]
    public void SuaLoiCauKhongChapNhanGanDung()
    {
        // Ở đây viết đúng cả câu CHÍNH LÀ thứ đang được đo, nên không nới tay.
        var answer = new PlacementAnswerDocument
        {
            Accepted = ["He doesn't have access to the shared folder."],
        };

        Assert.Equal(100, PlacementScoring.Grade(
            PlacementItemKind.ErrorCorrection, answer, Write("  he doesn't have access to the shared folder  ")));

        Assert.Equal(0, PlacementScoring.Grade(
            PlacementItemKind.ErrorCorrection, answer, Write("He dont have access to the shared folder.")));
    }

    [Fact]
    public void ThieuDauNhayLaMotCACHVIETKHAC_PhaiLietKeTrongAccepted()
    {
        // Chuẩn hoá cố ý giữ dấu nháy vì "it's" và "its" là hai từ khác nhau.
        // Hệ quả: người soạn đề phải liệt kê cả biến thể không dấu nháy, nếu không
        // học viên gõ "doesnt" trên bàn phím vội sẽ bị 0 điểm cho một lỗi không được đo.
        var strict = new PlacementAnswerDocument
        {
            Accepted = ["He doesn't have access to the shared folder."],
        };

        Assert.Equal(0, PlacementScoring.Grade(
            PlacementItemKind.ErrorCorrection, strict, Write("He doesnt have access to the shared folder.")));

        var listed = new PlacementAnswerDocument
        {
            Accepted =
            [
                "He doesn't have access to the shared folder.",
                "He doesnt have access to the shared folder.",
            ],
        };

        Assert.Equal(100, PlacementScoring.Grade(
            PlacementItemKind.ErrorCorrection, listed, Write("He doesnt have access to the shared folder.")));
    }

    [Fact]
    public void DauNhayCongCuaBanPhimDienThoaiVanDuocChapNhan()
    {
        // Bàn phím điện thoại tự đổi ' thành ’. Học viên viết đúng mà bị chấm sai
        // thì không có cách nào tự phát hiện.
        var answer = new PlacementAnswerDocument { Accepted = ["He doesn't have access."] };

        Assert.Equal(100, PlacementScoring.Grade(
            PlacementItemKind.ErrorCorrection, answer, Write("He doesn’t have access.")));
    }

    [Fact]
    public void EmailDuYVaDuDaiThi100()
    {
        var answer = new PlacementAnswerDocument
        {
            MustContain = ["print", "9", "update"],
            MinWords = 10,
        };

        var text = "The print server is down since 9 this morning. I will send an update when it is fixed.";

        Assert.Equal(100, PlacementScoring.Grade(PlacementItemKind.GuidedEmail, answer, Write(text)));
    }

    [Fact]
    public void EmailThieuMotYThiMatMotPhanBa()
    {
        var answer = new PlacementAnswerDocument
        {
            MustContain = ["print", "9", "update"],
            MinWords = 10,
        };

        var text = "The print server is down since 9 this morning and we are looking at it.";

        Assert.Equal(66.7, PlacementScoring.Grade(PlacementItemKind.GuidedEmail, answer, Write(text)));
    }

    [Fact]
    public void EmailDuYNhungQuaNganThiBiTruTheoTyLeThieu()
    {
        // Trừ theo tỷ lệ chứ không cho 0: ba ý gói trong mười từ vẫn hơn hẳn
        // một email không có ý nào.
        var answer = new PlacementAnswerDocument
        {
            MustContain = ["print", "9", "update"],
            MinWords = 20,
        };

        var score = PlacementScoring.Grade(
            PlacementItemKind.GuidedEmail, answer, Write("print down 9 update soon"));

        Assert.NotNull(score);
        Assert.InRange(score!.Value, 20, 30);
    }

    [Fact]
    public void NguongDoanMoTinhTheoNguongChamCuaTungCau()
    {
        Assert.True(PlacementScoring.IsFastAnswer(responseSeconds: 4, slowAnswerSeconds: 60));
        Assert.False(PlacementScoring.IsFastAnswer(responseSeconds: 12, slowAnswerSeconds: 60));

        // Câu có ngưỡng chậm rất nhỏ vẫn phải có sàn, nếu không mọi câu đều "nhanh".
        Assert.False(PlacementScoring.IsFastAnswer(responseSeconds: 3, slowAnswerSeconds: 10));
    }

    // ---------------------------------------------------------------
    // Tổng hợp cả lượt thi
    // ---------------------------------------------------------------

    private static ScoredItem Item(SkillType? skill, double score, PlacementItemKind kind = PlacementItemKind.Mcq) =>
        new($"i{Guid.NewGuid():N}", kind, skill, 1.0, score, false, null, null);

    [Fact]
    public void TrucKhongCoCauNaoThiBaoLaChuaDoDuoc()
    {
        var outcome = PlacementScoring.Summarise([
            Item(SkillType.Listening, 80),
            Item(SkillType.Reading, 80),
            Item(SkillType.Writing, 80),
        ]);

        Assert.Contains(SkillType.Speaking, outcome.UnmeasuredSkills);
        Assert.False(outcome.SkillScores.ContainsKey(SkillType.Speaking));
        Assert.Contains(outcome.NotesVi, n => n.Contains("Chưa đo được"));
    }

    [Fact]
    public void TrucChuaDoKhongKeoDiemChungXuong()
    {
        // Ba trục 80 điểm phải ra L4. Nếu trục Nói bị ngầm tính 0 thì điểm chung
        // còn 60 và học viên rơi xuống L3 vì một phép đo không tồn tại.
        var outcome = PlacementScoring.Summarise([
            Item(SkillType.Listening, 85),
            Item(SkillType.Reading, 85),
            Item(SkillType.Writing, 85),
        ]);

        Assert.Equal(85, outcome.OverallScore);
        Assert.Equal("L4", outcome.Band);
    }

    [Fact]
    public void DiemTongCaoKhongCheDuocTrucYeu()
    {
        // Đọc và viết rất tốt, nghe gần như không được. Người này vào L4 sẽ gặp
        // bài đầu tiên đã không theo nổi.
        var outcome = PlacementScoring.Summarise([
            Item(SkillType.Listening, 15),
            Item(SkillType.Reading, 100),
            Item(SkillType.Writing, 100),
        ]);

        Assert.Equal("L1", outcome.Band);
        Assert.Contains(outcome.NotesVi, n => n.Contains("Nghe"));
    }

    [Fact]
    public void BacQuyRaTangDeXuatDungTheoGiaoTrinh()
    {
        Assert.Equal(ContextLayer.Life, Band(10).SuggestedLayer);
        Assert.Equal(ContextLayer.Life, Band(50).SuggestedLayer);
        Assert.Equal(ContextLayer.Office, Band(70).SuggestedLayer);
        Assert.Equal(ContextLayer.Professional, Band(90).SuggestedLayer);

        static PlacementOutcome Band(double score) => PlacementScoring.Summarise([
            Item(SkillType.Listening, score),
            Item(SkillType.Reading, score),
            Item(SkillType.Writing, score),
        ]);
    }

    [Fact]
    public void L0VaL1CungAnhXaVePreA1()
    {
        // Enum CefrLevel không có mức nào dưới PreA1. Khác biệt giữa hai bậc nằm ở
        // câu giải thích và bài mở đầu, không ở bậc engine đọc.
        var l0 = PlacementScoring.Summarise([Item(SkillType.Reading, 5)]);
        var l1 = PlacementScoring.Summarise([Item(SkillType.Reading, 30)]);

        Assert.Equal("L0", l0.Band);
        Assert.Equal("L1", l1.Band);
        Assert.Equal(CefrLevel.PreA1, l0.Level);
        Assert.Equal(CefrLevel.PreA1, l1.Level);
    }

    [Fact]
    public void ChiCauLikertBatSelfRatingMoiTinhVaoDiemTuDanhGia()
    {
        var confidence = new ScoredItem("self1", PlacementItemKind.Likert, null, 1, 0, false,
            SelfRatingIndex: 4, SelfRatingChoiceCount: 5);

        // Câu "yếu nhất kỹ năng nào" không mang chỉ số nào xuống đây.
        var weakest = new ScoredItem("self2", PlacementItemKind.Likert, null, 1, 0, false,
            SelfRatingIndex: null, SelfRatingChoiceCount: null);

        var outcome = PlacementScoring.Summarise([confidence, weakest, Item(SkillType.Reading, 50)]);

        Assert.Equal(100, outcome.SelfRatedScore);
    }

    [Fact]
    public void CauLikertKhongTinhVaoTrucKyNangNao()
    {
        var outcome = PlacementScoring.Summarise([
            new ScoredItem("self1", PlacementItemKind.Likert, null, 1, 0, false, 0, 5),
            Item(null, 90),
        ]);

        // Câu Likert 0 điểm không được kéo trục phụ từ vựng–ngữ pháp xuống.
        Assert.Equal(90, outcome.VocabGrammarScore);
    }

    [Fact]
    public void TraLoiQuaNhanhOPhanLonSoCauThiKemCanhBao()
    {
        var fast = new ScoredItem("q", PlacementItemKind.Mcq, SkillType.Reading, 1, 0, true, null, null);

        var outcome = PlacementScoring.Summarise([fast, fast, fast, Item(SkillType.Reading, 100)]);

        Assert.True(outcome.FastAnswerRatio > 0.4);
        Assert.Contains(outcome.NotesVi, n => n.Contains("rất nhanh"));
    }

    [Fact]
    public void TrongSoCaoHonThiAnhHuongNhieuHonToiDiemTruc()
    {
        var outcome = PlacementScoring.Summarise([
            new ScoredItem("nhe", PlacementItemKind.Mcq, SkillType.Reading, 1.0, 0, false, null, null),
            new ScoredItem("nang", PlacementItemKind.Mcq, SkillType.Reading, 3.0, 100, false, null, null),
        ]);

        Assert.Equal(75, outcome.SkillScores[SkillType.Reading]);
    }
}
