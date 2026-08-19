using EnglishForIT.Application.Learning;

namespace EnglishForIT.UnitTests.Learning;

/// <summary>
/// Chấm phát âm ở mức từ.
///
/// Giới hạn phải nhớ khi đọc mọi test dưới đây: hệ thống chấm dựa trên BẢN GHI CHỮ của bộ
/// nhận dạng, nên nó biết học viên có nói ra đúng từ hay không, chứ không biết họ phát âm
/// âm nào sai. Test vì vậy kiểm quy tắc quy điểm, không kiểm chất lượng nhận dạng.
/// </summary>
public class SpeechScorerTests
{
    private const string Expected = "I cannot access the shared folder";

    [Fact]
    public void DocDungCaCauThiDatToiDa()
    {
        var score = SpeechScorer.Score(Expected, "I cannot access the shared folder.", 2400);

        Assert.Equal(100, score.PronunciationScore);
        Assert.Equal(100, score.CommunicationScore);
        Assert.Empty(score.MissedWords);
        Assert.Contains("Nói rõ", score.FeedbackVi[0]);
    }

    [Fact]
    public void ThieuTuThiNeuDichDanhTuNaoBiMat()
    {
        var score = SpeechScorer.Score(Expected, "I access folder", 1500);

        Assert.Contains("cannot", score.MissedWords);
        Assert.Contains("shared", score.MissedWords);

        // Nhận xét phải gọi tên từ cụ thể. "Phát âm chưa tốt" không giúp ai sửa được gì.
        Assert.Contains(score.FeedbackVi, f => f.Contains("cannot"));
    }

    [Fact]
    public void MatTuPhuDinhLamDiemTruyenDatTutManh()
    {
        // "cannot" bị nuốt thì câu đảo hẳn nghĩa — đây đúng là thứ trục truyền đạt phải bắt.
        var withNegation = SpeechScorer.Score(Expected, "I cannot access the shared folder", 2400);
        var without = SpeechScorer.Score(Expected, "I access the shared folder", 2400);

        Assert.True(without.CommunicationScore < withNegation.CommunicationScore);
    }

    [Fact]
    public void ThieuMaoTuThiKhongPhatDiemTruyenDat()
    {
        // Thiếu "the" không ai hiểu sai, nên trục truyền đạt phải bỏ qua.
        var score = SpeechScorer.Score(Expected, "I cannot access shared folder", 2400);

        Assert.Equal(100, score.CommunicationScore);
        Assert.True(score.PronunciationScore < 100);
    }

    [Fact]
    public void KhacNhauMotKyTuVanTinhLaDung()
    {
        // Bộ nhận dạng hay trả "folders" thay vì "folder"; đó không phải thứ đang cần dạy.
        var score = SpeechScorer.Score(Expected, "I cannot access the shared folders", 2400);

        Assert.Equal(100, score.PronunciationScore);
    }

    [Theory]
    [InlineData(2400)]   // 6 từ trong 2,4 giây = 150 từ/phút, đúng tốc độ người bản xứ
    [InlineData(5100)]   // ~71 từ/phút, vừa chạm sàn của khoảng chấp nhận
    [InlineData(1800)]   // 200 từ/phút, nhanh nhưng vẫn nghe được
    public void TocDoTrongKhoangChapNhanThiTroiChayDatToiDa(int durationMs)
    {
        var score = SpeechScorer.Score(Expected, "I cannot access the shared folder", durationMs);

        // Khoảng chấp nhận cố ý rộng: học viên mới cần được nói chậm mà không bị phạt nặng.
        Assert.Equal(100, score.FluencyScore);
    }

    [Fact]
    public void NoiQuaChamThiTruDiemTroiChay()
    {
        // 6 từ trong 20 giây = 18 từ mỗi phút, chậm tới mức người nghe bỏ đi.
        var slow = SpeechScorer.Score(Expected, "I cannot access the shared folder", 20_000);
        var normal = SpeechScorer.Score(Expected, "I cannot access the shared folder", 2400);

        Assert.True(slow.FluencyScore < normal.FluencyScore);
        Assert.Contains(slow.FeedbackVi, f => f.Contains("từ mỗi phút"));
    }

    /// <summary>
    /// Bản ghi rỗng vẫn phải nói thẳng là do micro chứ không phải do người học đọc sai.
    ///
    /// Tầng dịch vụ còn đi xa hơn: nó KHÔNG lưu lượt này thành 0 điểm, vì im lặng gần như
    /// luôn là micro tắt hoặc phòng ồn. Ở đây chỉ kiểm phần câu chữ.
    /// </summary>
    [Fact]
    public void KhongNgheThayGiThiDoLoiChoMicroChuKhongChoNguoiHoc()
    {
        var score = SpeechScorer.Score(Expected, "", 3000);

        Assert.Equal(0, score.PronunciationScore);
        Assert.Contains(score.FeedbackVi, f => f.Contains("micro"));
        Assert.DoesNotContain(score.FeedbackVi, f => f.Contains("Đọc chậm lại"));
    }

    [Fact]
    public void CauMauRongThiBaoKhongChamDuoc()
    {
        var score = SpeechScorer.Score("", "anything", 1000);

        Assert.Contains(score.FeedbackVi, f => f.Contains("không chấm được"));
    }
}
