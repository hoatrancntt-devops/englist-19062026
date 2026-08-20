namespace EnglishForIT.Application.Content;

/// <summary>
/// Một bộ từ vựng tần suất cao, đọc từ YAML.
///
/// Vì sao KHÔNG nhồi vào khuôn bài học: cổng kiểm định giới hạn mỗi bài tối đa 8 từ và 12 phút,
/// nên 1.000 từ sẽ thành 125 bài — mỗi bài phải soạn thêm phần giảng, hai lỗi thường gặp, trọng
/// số kỹ năng và drill nói. Phần khung đó lớn hơn chính từ vựng, và không dạy thêm được gì.
///
/// Bộ từ vựng là một loại nội dung khác hẳn bài học: nó không dạy tình huống, nó dựng vốn từ.
/// Đổi lại nó KHÔNG có quyền mở khoá gì trên lộ trình — xem <see cref="VocabDeckService"/>.
///
/// Dùng lại nguyên <see cref="VocabularyDocument"/> cho từng từ, nên người soạn chỉ phải học một
/// hình dạng và giao diện thẻ từ của bài học dùng được luôn không sửa gì.
/// </summary>
public class VocabDeckDocument
{
    public string Code { get; set; } = string.Empty;
    public string TitleVi { get; set; } = string.Empty;

    /// <summary>Vì sao nhóm từ này đáng học trước, viết cho học viên đọc.</summary>
    public string ContextVi { get; set; } = string.Empty;

    /// <summary>
    /// Bậc tần suất, đếm từ 1.
    ///
    /// Bậc 1 là 100 từ thông dụng nhất — riêng nhóm đó đã phủ khoảng một nửa số từ gặp trong
    /// lời nói hàng ngày. Thứ tự học đi theo tần suất chứ không theo chủ đề, vì tần suất mới
    /// là thứ quyết định học viên hiểu được bao nhiêu phần trăm những gì nghe thấy.
    /// </summary>
    public int Band { get; set; }

    public List<VocabularyDocument> Words { get; set; } = [];
}
