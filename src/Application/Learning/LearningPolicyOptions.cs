namespace EnglishForIT.Application.Learning;

/// <summary>
/// Các con số chính sách của sản phẩm, gom về một chỗ.
///
/// Đây không phải hằng số kỹ thuật mà là quyết định sản phẩm. Rải chúng khắp nơi
/// nghĩa là sau này mỗi module lại diễn giải một kiểu.
/// </summary>
public class LearningPolicyOptions
{
    public const string SectionName = "LearningPolicy";

    // -----------------------------------------------------------------------
    // Buổi học và chuỗi ngày
    // -----------------------------------------------------------------------

    /// <summary>
    /// Mục tiêu phút mỗi ngày mặc định. Học viên đổi được trong cài đặt.
    ///
    /// 45 phút tương ứng khoảng ba buổi 15 phút, hoặc bốn bài liền.
    /// Bài dài tối đa 12 phút nên 45 phút luôn gói trọn được vài bài chứ không cắt giữa bài.
    /// </summary>
    public int DailyMinutesTarget { get; set; } = 45;

    /// <summary>
    /// Một ngày chỉ tính vào chuỗi khi đã học đủ <see cref="DailyMinutesTarget"/> phút.
    /// Học năm phút rồi thoát không giữ được chuỗi.
    /// </summary>
    public bool StreakRequiresDailyTarget { get; set; } = true;

    /// <summary>
    /// Ngày tính chuỗi phải chạm đủ bốn kỹ năng.
    ///
    /// Hệ quả có chủ đích: học chế độ một kỹ năng vẫn cộng phút và vẫn lên điểm trục đó,
    /// nhưng KHÔNG giữ được chuỗi. Giao diện phải nói thẳng điều này ngay lúc chọn chế độ,
    /// không để học viên phát hiện sau ba tuần.
    /// </summary>
    public bool StreakRequiresAllFourSkills { get; set; } = true;

    /// <summary>Số lượt nghỉ tối đa mà không đứt chuỗi. Trên 2 thì chuỗi mất ý nghĩa.</summary>
    public int MaxStreakFreezeTokens { get; set; } = 2;

    // -----------------------------------------------------------------------
    // Lưu trữ ghi âm
    // -----------------------------------------------------------------------

    /// <summary>
    /// Giữ file ghi âm của học viên bao nhiêu ngày rồi xoá.
    ///
    /// 45 ngày là điểm cân bằng: đủ dài để gom dữ liệu hiệu chỉnh ngưỡng chấm phát âm
    /// qua trọn một chu kỳ học, đủ ngắn để không tích luỹ giọng nói vô thời hạn.
    ///
    /// Job dọn dẹp chỉ xoá FILE, giữ lại bản ghi điểm trong speech_attempts —
    /// điểm số vẫn dùng để vẽ tiến bộ sau khi âm thanh đã bị xoá.
    /// </summary>
    public int SpeechAudioRetentionDays { get; set; } = 45;

    // -----------------------------------------------------------------------
    // Ngưỡng học tập
    // -----------------------------------------------------------------------

    /// <summary>Mastery tối thiểu để một bài coi là đã thạo.</summary>
    public int MasteryThreshold { get; set; } = 80;

    /// <summary>
    /// Ngưỡng riêng cho từng trục kỹ năng. Thấp hơn ngưỡng tổng vì một trục yếu
    /// không nên chặn cả bài, nhưng vẫn phải đủ để không lọt qua bằng cách bỏ hẳn một kỹ năng.
    /// </summary>
    public int PerSkillThreshold { get; set; } = 65;

    /// <summary>Điểm tối thiểu để qua bài thi vượt. Cao hơn ngưỡng thường vì bỏ qua cả quá trình học.</summary>
    public int ChallengePassThreshold { get; set; } = 85;

    /// <summary>
    /// Trượt thi vượt rồi phải chờ bao lâu mới được thi lại bài đó.
    ///
    /// Không có khoảng chờ thì thi vượt biến thành trò dò đáp án: một bài chỉ có chừng
    /// mười câu, thi lại liên tục là đoán ra hết. 12 tiếng đủ để một lần trượt buộc học viên
    /// quay lại học bài thật, mà không khoá họ cả ngày nếu họ trượt vì lý do vớ vẩn.
    /// </summary>
    public int ChallengeCooldownHours { get; set; } = 12;

    /// <summary>
    /// Số câu tối thiểu để một bài được phép thi vượt.
    ///
    /// Dưới mức này thì một câu sai đã đánh tụt quá nhiều điểm, và đoán mò cũng dễ trúng —
    /// bài như vậy phải học chứ không cho thi vượt.
    /// </summary>
    public int ChallengeMinItems { get; set; } = 6;

    /// <summary>
    /// Có phục vụ các câu nói trong đề xếp lớp hay không.
    ///
    /// Mặc định TẮT vì chưa có dịch vụ chấm phát âm. Bốn câu nói đã soạn sẵn trong
    /// content/placement/*.yaml và nằm im trong DB; bật cờ này lên là chúng xuất hiện,
    /// không phải soạn lại đề. Khi tắt, trục Nói được báo là "chưa đo được" chứ
    /// KHÔNG bị tính 0 điểm — tính 0 sẽ kéo mọi học viên xuống một bậc.
    /// </summary>
    public bool PlacementSpeakingEnabled { get; set; }

    /// <summary>Thời gian cộng thêm sau hạn nộp lý thuyết, phòng mạng chậm lúc nộp bài.</summary>
    public int PlacementGraceMinutes { get; set; } = 5;

    /// <summary>
    /// Trần thời gian cho một lượt làm bài, tính từ lúc mở bài. Quá giờ thì mọi bước đã làm
    /// trong lượt đó bị huỷ và học viên bắt đầu lại từ đầu.
    ///
    /// Bài dài nhất được thiết kế 12 phút, nên 30 phút là rộng rãi cho người làm chậm.
    /// Nó tồn tại để chặn kiểu mở bài rồi để đó cả ngày, làm rải rác vài bước một lúc —
    /// điểm thu được kiểu đó không phản ánh việc học viên nhớ được gì trong một buổi.
    /// </summary>
    public int LessonTimeLimitMinutes { get; set; } = 30;

    /// <summary>
    /// Cứ bấy nhiêu bài thạo thì phải qua một bài tổng hợp ôn lại đúng bấy nhiêu bài đó.
    /// </summary>
    public int ConsolidationGroupSize { get; set; } = 3;

    /// <summary>
    /// Ngưỡng qua bài tổng hợp. Bằng ngưỡng thạo bài thường chứ không cao hơn: đây là bài ôn
    /// lại thứ đã học, không phải bài thi vượt.
    /// </summary>
    public int ConsolidationPassThreshold { get; set; } = 80;

    /// <summary>Số câu lấy từ mỗi bài trong nhóm. Ba bài, bốn câu mỗi bài là mười hai câu.</summary>
    public int ConsolidationItemsPerLesson { get; set; } = 4;
}
