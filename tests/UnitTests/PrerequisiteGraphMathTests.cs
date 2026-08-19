using EnglishForIT.Application.Content;

namespace EnglishForIT.UnitTests;

/// <summary>
/// Toán đồ thị tiên quyết.
///
/// Trọng tâm là hai chỗ sai mà mắt không bắt được: đường dài nhất bị tính thành đường ngắn nhất
/// ở đồ thị chia nhánh rồi nhập lại, và chu trình làm treo phép sắp xếp tô-pô.
/// </summary>
public class PrerequisiteGraphMathTests
{
    [Fact]
    public void ChuoiThangThiDoSauTangDan()
    {
        var shape = PrerequisiteGraphMath.Measure(
            ["A", "B", "C"],
            [new GraphLink("A", "B"), new GraphLink("B", "C")]);

        Assert.Equal(0, shape.Depth["A"]);
        Assert.Equal(1, shape.Depth["B"]);
        Assert.Equal(2, shape.Depth["C"]);
    }

    [Fact]
    public void ChiaNhanhRoiNhapLaiThiLayNhanhDaiNhat()
    {
        // A ─→ B ─→ C ─┐
        //  └────────────┴─→ D
        //
        // D tới được bằng đường ngắn (A→D, dài 1) lẫn đường dài (A→B→C→D, dài 3).
        // Học viên phải qua HẾT cạnh cứng nên đáp án đúng là 3. Thuật toán nào trả 1 là
        // đang đo đường ngắn nhất, và mọi con số trên màn quản trị sẽ nhỏ hơn thực tế.
        var shape = PrerequisiteGraphMath.Measure(
            ["A", "B", "C", "D"],
            [
                new GraphLink("A", "B"),
                new GraphLink("B", "C"),
                new GraphLink("C", "D"),
                new GraphLink("A", "D"),
            ]);

        Assert.Equal(3, shape.Depth["D"]);
    }

    [Fact]
    public void SoBaiBiChanTinhCaGianTiep()
    {
        var shape = PrerequisiteGraphMath.Measure(
            ["A", "B", "C", "D"],
            [new GraphLink("A", "B"), new GraphLink("B", "C"), new GraphLink("B", "D")]);

        Assert.Equal(3, shape.Gates["A"]);
        Assert.Equal(2, shape.Gates["B"]);
        Assert.Equal(0, shape.Gates["C"]);
    }

    [Fact]
    public void NhanhNhapLaiKhongBiDemHaiLan()
    {
        // Cả B lẫn C đều dẫn tới D. A chặn đúng ba bài, không phải bốn.
        var shape = PrerequisiteGraphMath.Measure(
            ["A", "B", "C", "D"],
            [
                new GraphLink("A", "B"),
                new GraphLink("A", "C"),
                new GraphLink("B", "D"),
                new GraphLink("C", "D"),
            ]);

        Assert.Equal(3, shape.Gates["A"]);
    }

    [Fact]
    public void ChuTrinhDuocChiRaChuKhongLamTreo()
    {
        var shape = PrerequisiteGraphMath.Measure(
            ["A", "B", "C", "D"],
            [new GraphLink("A", "B"), new GraphLink("B", "C"), new GraphLink("C", "B")]);

        // B và C khoá lẫn nhau nên không bao giờ mở được; A và D vẫn lành.
        Assert.Equal(["B", "C"], shape.InCycle.OrderBy(c => c));
        Assert.DoesNotContain("A", shape.InCycle);
        Assert.DoesNotContain("D", shape.InCycle);
    }

    [Fact]
    public void DoThiRongKhongNemLoi()
    {
        var shape = PrerequisiteGraphMath.Measure([], []);

        Assert.Empty(shape.Depth);
        Assert.Empty(shape.InCycle);
    }

    [Fact]
    public void BaiRoiKhongCoCanhNaoThiSauBangKhong()
    {
        var shape = PrerequisiteGraphMath.Measure(["A", "B"], []);

        Assert.Equal(0, shape.Depth["A"]);
        Assert.Equal(0, shape.Gates["B"]);
        Assert.Empty(shape.InCycle);
    }
}
