using System.Text;

namespace EnglishForIT.Application.Learning;

/// <summary>
/// So khớp câu trả lời viết tay.
///
/// Dùng chung cho bài viết trong màn học và câu viết trong đề xếp lớp. Hai chỗ này
/// bắt buộc phải chấm giống hệt nhau: cùng một câu trả lời mà nơi cho đúng nơi cho sai
/// thì học viên mất lòng tin vào toàn bộ điểm số, và không có cách nào giải thích được.
/// </summary>
public static class TextMatching
{
    /// <summary>Bỏ dấu câu, gộp khoảng trắng, hạ chữ thường. Giữ lại dấu nháy vì "don't" khác "dont".</summary>
    public static string Normalize(string value)
    {
        var builder = new StringBuilder(value.Length);
        var lastWasSpace = false;

        foreach (var c in value.Trim().ToLowerInvariant())
        {
            // Cả ba loại nháy đơn quy về một: bàn phím điện thoại tự đổi ' thành ’,
            // và học viên không có cách nào biết vì sao câu đúng lại bị chấm sai.
            var normalized = c is '’' or 'ʼ' ? '\'' : c;

            if (char.IsLetterOrDigit(normalized) || normalized == '\'')
            {
                builder.Append(normalized);
                lastWasSpace = false;
            }
            else if (!lastWasSpace)
            {
                builder.Append(' ');
                lastWasSpace = true;
            }
        }

        return builder.ToString().Trim();
    }

    /// <summary>
    /// Một ý coi là có mặt khi phần lớn từ khoá của nó xuất hiện. So khớp nguyên cụm
    /// sẽ đòi học viên chép đúng từng chữ, mà đó không phải mục tiêu của bài viết.
    /// </summary>
    public static bool ContainsPoint(string normalizedText, string point)
    {
        var keywords = Normalize(point)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2)
            .ToList();

        if (keywords.Count == 0)
        {
            return true;
        }

        var hits = keywords.Count(k => normalizedText.Contains(k, StringComparison.Ordinal));
        return hits * 2 >= keywords.Count;
    }

    /// <summary>
    /// Khoảng cách sửa lỗi Damerau-Levenshtein (dạng optimal string alignment).
    ///
    /// Khác Levenshtein thường ở đúng một điểm: đảo hai ký tự liền nhau tính là MỘT lỗi,
    /// không phải hai. Đó là lỗi gõ phổ biến nhất ("confrim" thay vì "confirm"), và tính
    /// nó thành hai lỗi sẽ đẩy từ gõ nhầm ra ngoài ngưỡng chấp nhận rồi cho 0 điểm oan.
    /// </summary>
    public static int EditDistance(string a, string b)
    {
        if (a.Length == 0)
        {
            return b.Length;
        }

        if (b.Length == 0)
        {
            return a.Length;
        }

        var d = new int[a.Length + 1, b.Length + 1];

        for (var i = 0; i <= a.Length; i++)
        {
            d[i, 0] = i;
        }

        for (var j = 0; j <= b.Length; j++)
        {
            d[0, j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;

                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);

                // Đảo hai ký tự liền nhau.
                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                {
                    d[i, j] = Math.Min(d[i, j], d[i - 2, j - 2] + 1);
                }
            }
        }

        return d[a.Length, b.Length];
    }
}
