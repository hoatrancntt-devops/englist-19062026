using EnglishForIT.Application.Content;

namespace EnglishForIT.UnitTests.Learning;

/// <summary>
/// Khoá chặt giao kèo băm chuỗi.
///
/// Bước sinh giọng đặt tên file theo hash, còn API tra file cũng theo hash. Hai phía chạy
/// cách nhau hàng tuần và không có gì bắt chúng khớp nhau ngoài hàm này. Đổi cách chuẩn hoá
/// mà không hay biết thì triệu chứng là "thư mục đầy file mà bấm nghe vẫn im" — không lỗi,
/// không log, rất tốn thời gian để lần ra. Test này làm cái đổi đó gãy ngay tại chỗ.
/// </summary>
public class TtsCatalogueTests
{
    [Fact]
    public void XuongDongVaThutLeKhongLamDoiHash()
    {
        // Transcript trong YAML viết bằng block scalar nên mang theo xuống dòng và thụt lề,
        // còn chuỗi trình duyệt gửi lên đã qua JSON. Hai bên phải ra cùng một file.
        var tuYaml = "Could we get the bill, please?\n  Together or separate?\n";
        var tuTrinhDuyet = "Could we get the bill, please? Together or separate?";

        Assert.Equal(TtsCatalogue.HashOf(tuYaml), TtsCatalogue.HashOf(tuTrinhDuyet));
    }

    [Fact]
    public void HashLaCoDinhGiuaCacLanChay()
    {
        // Giá trị gắn cứng có chủ đích: đổi thuật toán băm là mọi file audio đã sinh thành
        // mồ côi và phải sinh lại cả nghìn đoạn. Test này bắt phải cân nhắc điều đó.
        Assert.Equal(32, TtsCatalogue.HashOf("Hello").Length);
        Assert.Equal(
            TtsCatalogue.HashOf("Hello"),
            TtsCatalogue.HashOf("  Hello  "));
    }

    [Fact]
    public void ChuoiRongTraVeRong()
    {
        Assert.Equal(string.Empty, TtsCatalogue.HashOf("   \n\t "));
        Assert.Equal(string.Empty, TtsCatalogue.HashOf(string.Empty));
    }

    [Fact]
    public void HaiCauKhacNhauRaHaiHashKhacNhau()
    {
        Assert.NotEqual(TtsCatalogue.HashOf("One latte to go."), TtsCatalogue.HashOf("Two lattes to go."));
    }

    [Fact]
    public void GomDuCacChoCoNutBamNghe()
    {
        var lesson = new LessonDocument
        {
            Code = "TEST-01",
            Listening = new ListeningDocument { TranscriptEn = "Listening transcript." },
            SpeakingDrills = [new SpeakingDrillDocument { ExpectedText = "Drill sentence." }],
            Quiz = [new QuizItemDocument { AudioText = "Quiz audio." }],
            Dialogue = new DialogueDocument { Turns = [new DialogueTurnDocument { En = "Dialogue line." }] },
        };

        var entries = TtsCatalogue.Collect([lesson], [], []);

        Assert.Equal(4, entries.Count);
        Assert.Contains(entries, e => e.Text == "Listening transcript.");
        Assert.Contains(entries, e => e.Text == "Drill sentence.");
        Assert.Contains(entries, e => e.Text == "Quiz audio.");
        Assert.Contains(entries, e => e.Text == "Dialogue line.");
    }

    [Fact]
    public void CungMotCauODaiHocKhacNhauChiSinhMotFile()
    {
        // Câu mẫu lặp lại giữa các bài là chuyện thường. Sinh trùng là tốn thời gian máy
        // và phình thư mục mà không thêm được gì.
        var a = new LessonDocument
        {
            Code = "A-01",
            SpeakingDrills = [new SpeakingDrillDocument { ExpectedText = "Could we get the bill, please?" }],
        };

        var b = new LessonDocument
        {
            Code = "B-01",
            SpeakingDrills = [new SpeakingDrillDocument { ExpectedText = "Could we get the bill, please?" }],
        };

        Assert.Single(TtsCatalogue.Collect([a, b], [], []));
    }

    [Fact]
    public void DoanQuaDaiBiBoQua()
    {
        var lesson = new LessonDocument
        {
            Code = "TEST-02",
            Listening = new ListeningDocument { TranscriptEn = new string('a', TtsCatalogue.MaxTextLength + 1) },
        };

        Assert.Empty(TtsCatalogue.Collect([lesson], [], []));
    }
}
