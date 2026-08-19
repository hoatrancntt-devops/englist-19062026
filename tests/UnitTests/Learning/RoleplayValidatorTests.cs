using EnglishForIT.Application.Content;

namespace EnglishForIT.UnitTests.Learning;

/// <summary>
/// Cổng chất lượng kịch bản roleplay.
///
/// Kịch bản hỏng nguy hiểm hơn bài học hỏng: học viên đang giữa hội thoại, bấm một lựa chọn
/// rồi rơi vào node không tồn tại thì không có đường nào thoát ra ngoài tải lại trang.
/// </summary>
public class RoleplayValidatorTests
{
    [Fact]
    public void BatDuocLuaChonTroToiNodeKhongTonTai()
    {
        var doc = Valid();
        doc.Nodes[0].Choices[0].Next = "khong-co-node-nay";

        Assert.Contains(Validate(doc), i => i.Code == "R021");
    }

    [Fact]
    public void BatDuocNodeKhongAiToiDuoc()
    {
        var doc = Valid();
        doc.Nodes.Add(new RoleplayNodeDocument
        {
            Code = "mo-coi",
            PartnerLineEn = "Hello?",
            PartnerLineVi = "A lô?",
            Terminal = true,
            SummaryVi = "x",
        });

        // Nội dung viết ra mà không học viên nào gặp là công sức đổ đi.
        Assert.Contains(Validate(doc), i => i.Code == "R023");
    }

    [Fact]
    public void BatDuocKichBanKhongCoDuongThanhCong()
    {
        var doc = Valid();

        foreach (var node in doc.Nodes.Where(n => n.Terminal))
        {
            node.Success = false;
        }

        // Làm đúng hết mà vẫn không qua được là bẫy, không phải bài học.
        Assert.Contains(Validate(doc), i => i.Code == "R025");
    }

    [Fact]
    public void BatDuocLuotKhongCoLuaChonNaoDat()
    {
        var doc = Valid();

        foreach (var choice in doc.Nodes[0].Choices)
        {
            choice.Quality = "wrong";
            choice.FeedbackVi = "x";
        }

        Assert.Contains(Validate(doc), i => i.Code == "R022");
    }

    [Fact]
    public void BatDuocLuaChonSaiMaKhongGiaiThich()
    {
        var doc = Valid();
        doc.Nodes[0].Choices[1].Quality = "curt";
        doc.Nodes[0].Choices[1].FeedbackVi = null;

        // Biết mình sai mà không biết vì sao thì lần sau vẫn sai y hệt.
        Assert.Contains(Validate(doc), i => i.Code == "R019");
    }

    [Fact]
    public void BatDuocNodeKetThucThieuTongKet()
    {
        var doc = Valid();
        doc.Nodes.Last().SummaryVi = null;

        Assert.Contains(Validate(doc), i => i.Code == "R014");
    }

    [Fact]
    public void BatDuocQualityLa()
    {
        var doc = Valid();
        doc.Nodes[0].Choices[0].Quality = "excellent";

        Assert.Contains(Validate(doc), i => i.Code == "R018");
    }

    [Fact]
    public void BatDuocThieuBanDichTiengViet()
    {
        var doc = Valid();
        doc.Nodes[0].PartnerLineVi = "";

        // Học viên mất gốc không đoán được ngữ cảnh từ tiếng Anh, mà đoán sai ngữ cảnh
        // thì lựa chọn nào cũng vô nghĩa.
        Assert.Contains(Validate(doc), i => i.Code == "R012");
    }

    [Fact]
    public void KichBanHopLeThiKhongKeuGi()
    {
        Assert.Empty(Validate(Valid()));
    }

    [Fact]
    public void BatDuocMaKichBanTrung()
    {
        var issues = new RoleplayValidator().ValidateSet([Valid(), Valid()]);

        Assert.Contains(issues, i => i.Code == "R020");
    }

    // -----------------------------------------------------------------------

    private static IReadOnlyList<ValidationIssue> Validate(RoleplayDocument doc) =>
        new RoleplayValidator().ValidateOne(doc);

    /// <summary>Kịch bản tối thiểu hợp lệ: 5 lượt, có đường thành công và đường thất bại.</summary>
    private static RoleplayDocument Valid()
    {
        var doc = new RoleplayDocument
        {
            Code = "RP-TEST",
            TitleVi = "Kịch bản kiểm thử",
            ContextVi = "Bối cảnh kiểm thử.",
            PartnerName = "Người kiểm thử",
            StartNode = "n1",
        };

        for (var i = 1; i <= 3; i++)
        {
            doc.Nodes.Add(new RoleplayNodeDocument
            {
                Code = $"n{i}",
                PartnerLineEn = $"Line {i}?",
                PartnerLineVi = $"Câu {i}?",
                Choices =
                [
                    new RoleplayChoiceDocument
                    {
                        En = "Good answer.",
                        Vi = "Câu đạt.",
                        Next = i < 3 ? $"n{i + 1}" : "nOk",
                        Quality = "good",
                    },
                    new RoleplayChoiceDocument
                    {
                        En = "Bad answer.",
                        Vi = "Câu sai.",
                        Next = "nFail",
                        Quality = "wrong",
                        FeedbackVi = "Vì sao sai.",
                    },
                ],
            });
        }

        doc.Nodes.Add(new RoleplayNodeDocument
        {
            Code = "nOk",
            PartnerLineEn = "Great.",
            PartnerLineVi = "Tốt.",
            Terminal = true,
            Success = true,
            SummaryVi = "Bạn đã xong việc.",
        });

        doc.Nodes.Add(new RoleplayNodeDocument
        {
            Code = "nFail",
            PartnerLineEn = "Sorry.",
            PartnerLineVi = "Xin lỗi.",
            Terminal = true,
            Success = false,
            SummaryVi = "Hội thoại dừng sớm.",
        });

        return doc;
    }
}
