namespace EnglishForIT.Domain.Enums;

/// <summary>Mục tiêu học chọn ở onboarding. Quyết định thứ tự ưu tiên nhánh nghề trên roadmap.</summary>
public enum LearningGoal
{
    TeamCommunication = 0,
    VendorCommunication = 1,
    InternalCustomerSupport = 2,
    TechnicalWriting = 3,
    CloudPresentation = 4,
    AiUseCaseDiscussion = 5
}

/// <summary>Chín dạng câu của đề xếp lớp.</summary>
public enum PlacementItemKind
{
    Mcq = 0,
    McqRead = 1,
    Likert = 2,
    ReadAloud = 3,
    Repeat = 4,
    FillBlank = 5,
    ErrorCorrection = 6,
    GuidedEmail = 7,
    ShortAnswer = 8
}

public enum PlacementAttemptStatus
{
    InProgress = 0,
    Submitted = 1,
    Abandoned = 2
}

/// <summary>Mốc nghề nghiệp hiển thị trên roadmap. Đây là thứ học viên đo được, không phải điểm số.</summary>
public enum CareerMilestone
{
    JoinStandupConfidently = 0,
    WriteIncidentReport = 1,
    CallVendorSupport = 2,
    PresentCloudSolution = 3,
    ProposeAiUseCase = 4
}

public enum WritingTaskKind
{
    FillBlank = 0,
    Reorder = 1,
    GuidedEmail = 2
}

public enum RoleplayOutcome
{
    Incomplete = 0,
    Completed = 1,
    CompletedWithHints = 2
}
