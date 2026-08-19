using EnglishForIT.Domain.Enums;

namespace EnglishForIT.Application.Learning;

/// <summary>Tiến độ của một bài, rút gọn về đúng thứ engine cần.</summary>
public record MasterySnapshot(
    Guid LessonId,
    string LessonCode,
    LessonState State,
    double MasteryEffective,
    IReadOnlyDictionary<SkillType, double> SkillScores,
    DateTimeOffset? MasteredAt,
    /// <summary>
    /// Điểm này có được bằng thi vượt chứ không phải học bài. Không tính khi xét tiên quyết:
    /// xem <see cref="PrerequisiteEngine.Evaluate"/>.
    /// </summary>
    bool UnlockedByChallenge = false);

/// <summary>Một cạnh tiên quyết đã rút gọn.</summary>
public record PrerequisiteEdge(string RequiredLessonCode, int MinMastery, PrerequisiteKind Kind);

/// <summary>Bài học rút gọn để engine tính toán mà không cần chạm DB.</summary>
public record LessonNode(
    Guid Id,
    string Code,
    CefrLevel Level,
    ContextLayer Layer,
    LearningTrack Track,
    int OrderIndex,
    bool IsCheckpoint,
    IReadOnlyList<SkillType> SupportedSkills,
    IReadOnlyList<PrerequisiteEdge> Prerequisites);

/// <summary>Một điều kiện chưa đạt, đủ chi tiết để render câu giải thích cho học viên.</summary>
public record UnmetRequirement(
    string RequiredLessonCode,
    int Needed,
    double Have,
    PrerequisiteKind Kind,
    /// <summary>
    /// Bài tiên quyết đã thi vượt nhưng chưa học. Cần cờ riêng vì nếu không, màn hình sẽ nói
    /// "cần 70 điểm, bạn đang có 0" ngay cạnh một bài hiện là đã qua — người đọc tưởng hệ thống hỏng.
    /// </summary>
    bool OnlyChallengePassed = false)
{
    public double Gap => Math.Max(0, Needed - Have);
}

/// <summary>Kết quả đánh giá một bài.</summary>
public record LessonEvaluation(
    string LessonCode,
    LessonState State,
    LessonStateReason Reason,
    IReadOnlyList<UnmetRequirement> Unmet,
    IReadOnlyList<SkillType> SkillsBelowThreshold)
{
    public bool IsOpen => State is LessonState.Available or LessonState.InProgress
        or LessonState.Mastered or LessonState.NeedsReview or LessonState.Previewable;
}

/// <summary>
/// Engine chống nhảy cóc. Thuần tính toán, không chạm DB — nên test được mà không cần Postgres,
/// và cùng một logic dùng được cả trong API lẫn trong worker.
///
/// Ba quy tắc quyết định mọi thứ:
///  1. Tiên quyết cứng chưa đạt thì khoá. Tiên quyết mềm chỉ cảnh báo.
///  2. Ngưỡng xét RIÊNG từng kỹ năng — điểm tổng cao không che được trục yếu.
///  3. Mastery suy giảm theo thời gian, nên "đã thạo" không phải vĩnh viễn.
/// </summary>
public class PrerequisiteEngine(LearningPolicyOptions policy)
{
    /// <summary>
    /// Mastery hiệu dụng sau khi trừ suy giảm theo thời gian.
    ///
    /// Đường cong quên của Ebbinghaus: mất nhanh lúc đầu rồi chậm dần. Ở đây dùng
    /// suy giảm tuyến tính nhẹ có sàn — đơn giản, dễ giải thích cho học viên, và
    /// không bao giờ kéo một bài đã thạo xuống dưới mức mở khoá bài kế.
    /// </summary>
    public double EffectiveMastery(double raw, DateTimeOffset? masteredAt, DateTimeOffset now)
    {
        if (masteredAt is null || raw <= 0)
        {
            return raw;
        }

        var days = (now - masteredAt.Value).TotalDays;
        if (days <= 7)
        {
            return raw;
        }

        // Mỗi tuần sau tuần đầu tiên trừ 1.5 điểm, sàn ở 60% giá trị gốc.
        var decayed = raw - (days - 7) / 7.0 * 1.5;
        return Math.Max(raw * 0.6, Math.Min(raw, decayed));
    }

    /// <summary>
    /// Mastery thô: trung bình có trọng số của các trục kỹ năng.
    /// Kỹ năng không có trọng số thì không tính, để bài chỉ dạy hai kỹ năng
    /// vẫn đạt được 100.
    /// </summary>
    public double ComputeMasteryRaw(
        IReadOnlyDictionary<SkillType, double> skillScores,
        IReadOnlyDictionary<SkillType, double> weights)
    {
        if (weights.Count == 0)
        {
            return 0;
        }

        double total = 0, weightSum = 0;

        foreach (var (skill, weight) in weights.Where(w => w.Value > 0))
        {
            total += skillScores.GetValueOrDefault(skill) * weight;
            weightSum += weight;
        }

        return weightSum <= 0 ? 0 : Math.Round(total / weightSum, 1);
    }

    /// <summary>
    /// Các trục kỹ năng chưa đạt ngưỡng riêng.
    ///
    /// Đây là thứ chặn học viên "cày quiz" để qua bài mà không bao giờ mở miệng nói:
    /// điểm tổng có thể 85 nhưng trục Nói 40 thì bài vẫn chưa thạo.
    /// </summary>
    /// <remarks>
    /// Kỹ năng KHÔNG có điểm nào được ghi thì bỏ qua, không coi là dưới ngưỡng.
    ///
    /// "Chưa có dữ liệu" khác hẳn "làm và bị điểm thấp". Coi hai thứ đó như nhau gây ra hai
    /// hậu quả: bài chưa học lần nào bị báo yếu cả bốn trục, và kỹ năng chưa có bộ chấm
    /// (hiện là Nói) khiến không bài nào qua nổi. Đây cũng là quy tắc duy nhất — cả engine
    /// lẫn màn chấm bài đều gọi vào đây, để hai nơi không kết luận ngược nhau.
    /// </remarks>
    public IReadOnlyList<SkillType> SkillsBelowThreshold(
        IReadOnlyDictionary<SkillType, double> skillScores,
        IReadOnlyList<SkillType> supportedSkills)
    {
        return [.. supportedSkills
            .Where(skillScores.ContainsKey)
            .Where(skill => skillScores[skill] < policy.PerSkillThreshold)
            .OrderBy(skill => skill)];
    }

    /// <summary>Đánh giá trạng thái của một bài với tiến độ hiện có.</summary>
    public LessonEvaluation Evaluate(
        LessonNode lesson,
        IReadOnlyDictionary<string, MasterySnapshot> progressByCode,
        IReadOnlySet<string> challengePassedCodes,
        DateTimeOffset now)
    {
        // Thi vượt là đường tắt hợp lệ: đã qua thì mở, không cần xét tiên quyết nữa.
        if (challengePassedCodes.Contains(lesson.Code))
        {
            var passed = progressByCode.GetValueOrDefault(lesson.Code);
            return new LessonEvaluation(
                lesson.Code,
                passed?.State is LessonState.Mastered ? LessonState.Mastered : LessonState.Available,
                LessonStateReason.ChallengePassed,
                [],
                []);
        }

        var unmet = new List<UnmetRequirement>();

        foreach (var edge in lesson.Prerequisites)
        {
            var known = progressByCode.TryGetValue(edge.RequiredLessonCode, out var snapshot);

            // Thi vượt KHÔNG mở khoá bài sau.
            //
            // Nếu tính, một người có thể đi hết chuỗi 43 bậc chỉ bằng thi trắc nghiệm mà không
            // học bài nào: mỗi lần qua ghi mastery 85, thừa mức 65-70 mà cổng đòi, nên bài kế
            // tiếp mở ra ngay. Thi vượt chỉ nên có nghĩa "miễn học bài NÀY", không phải "đã có
            // nền để học bài SAU". Người thật sự đã giỏi đi bằng bài xếp lớp, xem
            // <see cref="LessonStateReason.PlacementUnlock"/>.
            var onlyChallenge = known && snapshot!.UnlockedByChallenge;

            var have = known && !onlyChallenge
                ? EffectiveMastery(snapshot!.MasteryEffective, snapshot.MasteredAt, now)
                : 0;

            if (have + 0.001 < edge.MinMastery)
            {
                unmet.Add(new UnmetRequirement(
                    edge.RequiredLessonCode, edge.MinMastery, Math.Round(have, 1), edge.Kind, onlyChallenge));
            }
        }

        var blockingUnmet = unmet.Where(u => u.Kind == PrerequisiteKind.Hard).ToList();

        if (blockingUnmet.Count > 0)
        {
            // Vẫn cho xem trước khi chỉ thiếu một chút: thấy được đích đến thì có động lực học tiếp.
            var closeEnough = blockingUnmet.All(u => u.Gap <= 15);

            return new LessonEvaluation(
                lesson.Code,
                closeEnough ? LessonState.Previewable : LessonState.Locked,
                LessonStateReason.PrerequisiteNotMet,
                unmet,
                []);
        }

        var current = progressByCode.GetValueOrDefault(lesson.Code);

        if (current is null)
        {
            return new LessonEvaluation(lesson.Code, LessonState.Available, LessonStateReason.PrerequisiteMet, unmet, []);
        }

        var below = SkillsBelowThreshold(current.SkillScores, lesson.SupportedSkills);
        var effective = EffectiveMastery(current.MasteryEffective, current.MasteredAt, now);

        // Đã thạo nhưng để lâu không ôn: kéo về cần ôn lại, KHÔNG kéo về khoá.
        // Khoá lại một bài đã học xong là cách nhanh nhất làm học viên bỏ cuộc.
        if (current.State == LessonState.Mastered && effective < policy.MasteryThreshold)
        {
            return new LessonEvaluation(lesson.Code, LessonState.NeedsReview, LessonStateReason.RetentionDecay, unmet, below);
        }

        if (effective >= policy.MasteryThreshold && below.Count == 0)
        {
            return new LessonEvaluation(lesson.Code, LessonState.Mastered, LessonStateReason.MasteryReached, unmet, []);
        }

        // Điểm tổng đủ nhưng có trục yếu: chưa thạo, và nói rõ thiếu trục nào.
        if (effective >= policy.MasteryThreshold && below.Count > 0)
        {
            return new LessonEvaluation(lesson.Code, LessonState.InProgress, LessonStateReason.SkillBelowThreshold, unmet, below);
        }

        var state = current.State == LessonState.Locked ? LessonState.Available : current.State;
        return new LessonEvaluation(lesson.Code, state, LessonStateReason.PrerequisiteMet, unmet, below);
    }

    /// <summary>
    /// Chọn bài kế tiếp và giải thích vì sao là bài này.
    ///
    /// Thứ tự ưu tiên:
    ///  1. Bài đang học dở — làm xong việc đang làm trước khi mở việc mới.
    ///  2. Bài cần ôn lại — trả nợ trước khi vay thêm.
    ///  3. Bài mới, ưu tiên đúng tầng và bậc hiện tại, rồi tới thứ tự trong track.
    /// </summary>
    public NextLessonChoice? ChooseNext(
        IReadOnlyList<LessonNode> lessons,
        IReadOnlyDictionary<string, LessonEvaluation> evaluations,
        IReadOnlyDictionary<string, MasterySnapshot> progressByCode,
        ContextLayer currentLayer,
        CefrLevel currentLevel,
        // null nghĩa là "không ưu tiên nhánh nào" — học viên chọn học tất cả lĩnh vực.
        LearningTrack? preferredTrack)
    {
        var open = lessons
            .Where(l => evaluations.TryGetValue(l.Code, out var e) && e.IsOpen)
            .ToList();

        if (open.Count == 0)
        {
            return null;
        }

        var inProgress = open
            .Where(l => evaluations[l.Code].State == LessonState.InProgress)
            .OrderBy(l => l.Level).ThenBy(l => l.OrderIndex)
            .FirstOrDefault();

        if (inProgress is not null)
        {
            var below = evaluations[inProgress.Code].SkillsBelowThreshold;
            var reason = below.Count > 0
                ? $"Bạn đang học dở bài này và còn thiếu ở kỹ năng {string.Join(", ", below.Select(ViName))}. Làm xong trước khi mở bài mới."
                : "Bạn đang học dở bài này. Làm xong trước khi mở bài mới.";

            return new NextLessonChoice(inProgress, reason);
        }

        var needsReview = open
            .Where(l => evaluations[l.Code].State == LessonState.NeedsReview)
            .OrderBy(l => progressByCode.GetValueOrDefault(l.Code)?.MasteredAt ?? DateTimeOffset.MaxValue)
            .FirstOrDefault();

        if (needsReview is not null)
        {
            return new NextLessonChoice(needsReview,
                "Bài này bạn đã thạo nhưng để lâu chưa ôn. Ôn lại mười phút bây giờ rẻ hơn học lại từ đầu sau này.");
        }

        var fresh = open
            .Where(l => evaluations[l.Code].State == LessonState.Available)
            .OrderBy(l => l.Layer == currentLayer ? 0 : 1)
            .ThenBy(l => l.Level == currentLevel ? 0 : 1)
            // Không có nhánh ưu tiên thì bỏ hẳn bước này, để bài kế tiếp đi theo đúng
            // thứ tự lộ trình. Truyền đại một nhánh vào đây thì nhánh đó được ưu tiên
            // ngầm, và người chọn "tất cả" vẫn bị đẩy vào một hướng mà họ không chọn.
            .ThenBy(l => preferredTrack is null || l.Track == preferredTrack ? 0 : 1)
            .ThenBy(l => l.Level)
            .ThenBy(l => l.OrderIndex)
            .FirstOrDefault();

        if (fresh is null)
        {
            return null;
        }

        // Chỉ kể tiên quyết CỨNG. Cạnh mềm là gợi ý nên học trước chứ không khoá bài, và học viên
        // hoàn toàn có thể tới đây mà chưa đụng tới nó — kể tên nó trong câu "bạn vừa đạt đủ điều
        // kiện" là nói với người ta rằng họ đã học một bài họ chưa từng mở.
        var gates = fresh.Prerequisites
            .Where(p => p.Kind == PrerequisiteKind.Hard)
            .Select(p => p.RequiredLessonCode)
            .ToList();

        var reasonText = fresh.IsCheckpoint
            ? "Đây là bài kiểm tra chốt chặng. Qua được thì mở cả nhóm bài tiếp theo."
            : gates.Count == 0
                ? "Đây là bài mở đầu, không cần điều kiện gì trước đó."
                : $"Bạn vừa đạt đủ điều kiện của bài này ({string.Join(", ", gates)}).";

        return new NextLessonChoice(fresh, reasonText);
    }

    /// <summary>Câu giải thích vì sao một bài đang bị khoá, có con số cụ thể.</summary>
    public string ExplainLock(LessonEvaluation evaluation)
    {
        if (evaluation.IsOpen)
        {
            return "Bài này đang mở.";
        }

        var blocking = evaluation.Unmet.Where(u => u.Kind == PrerequisiteKind.Hard).ToList();

        if (blocking.Count == 0)
        {
            return "Bài này chưa mở.";
        }

        // Bài mới thi vượt phải nói riêng. Nói "bạn đang có 0 điểm" về một bài đang hiện là đã qua
        // thì người đọc kết luận hệ thống đếm sai, chứ không hiểu là mình cần học bài đó thật.
        var parts = blocking.Select(u => u.OnlyChallengePassed
            ? $"{u.RequiredLessonCode} bạn mới thi vượt chứ chưa học, cần học bài đó rồi đạt {u.Needed} điểm"
            : $"{u.RequiredLessonCode} cần {u.Needed} điểm, bạn đang có {u.Have:0.#} (còn thiếu {u.Gap:0.#})");

        return "Còn thiếu: " + string.Join("; ", parts) + ".";
    }

    private static string ViName(SkillType skill) => skill switch
    {
        SkillType.Listening => "Nghe",
        SkillType.Speaking => "Nói",
        SkillType.Reading => "Đọc",
        SkillType.Writing => "Viết",
        _ => skill.ToString(),
    };
}

public record NextLessonChoice(LessonNode Lesson, string ReasonVi);
