using EnglishForIT.Application.Content;
using EnglishForIT.Infrastructure.Content;
using Microsoft.Extensions.Logging.Abstractions;

namespace EnglishForIT.ContentValidationTests;

/// <summary>
/// Cổng chất lượng chạy trên nội dung thật trong content/.
///
/// Đây là test quan trọng nhất của dự án: nó là thứ duy nhất chặn một bài hỏng
/// đi tới học viên. Chạy trong CI, không cần Docker, không cần DB.
/// </summary>
public class ContentQualityGateTests
{
    private static readonly LoadResult Loaded = LoadAll();
    private static readonly LessonValidator Validator = new();

    [Fact]
    public void MoiFileYamlDeuDocDuoc()
    {
        Assert.Empty(Loaded.Errors.Select(e => $"{Path.GetFileName(e.FilePath)}: {e.Message}"));
    }

    [Fact]
    public void CoItNhatMotBai()
    {
        // Thư mục nội dung rỗng mà test vẫn xanh là cái bẫy tệ nhất:
        // mọi kiểm tra bên dưới sẽ pass một cách vô nghĩa.
        Assert.NotEmpty(Loaded.Lessons);
    }

    [Fact]
    public void ToanBoNoiDungQuaCongChatLuong()
    {
        var issues = Validator.ValidateSet([.. Loaded.Lessons.Select(l => l.Document)]);

        Assert.Empty(issues.Select(i => i.ToString()));
    }

    [Fact]
    public void MoiBaiKhaiDungKyNangMaNoThucSuDay()
    {
        foreach (var lesson in Loaded.Lessons)
        {
            var inferred = LessonValidator.InferSupportedSkills(lesson.Document);

            Assert.True(inferred.Count > 0,
                $"{lesson.Document.Code} không dạy được kỹ năng nào.");
        }
    }

    [Fact]
    public void HashOnDinhGiuaHaiLanDoc()
    {
        // Hash đổi giữa hai lần đọc cùng một file nghĩa là seeder sẽ ghi lại toàn bộ
        // nội dung mỗi lần chạy, và bảng content_versions sẽ phình vô ích.
        var again = LoadAll();

        foreach (var lesson in Loaded.Lessons)
        {
            var match = again.Lessons.Single(l => l.Document.Code == lesson.Document.Code);
            Assert.Equal(lesson.SourceHash, match.SourceHash);
        }
    }

    private static LoadResult LoadAll()
    {
        var loader = new YamlContentLoader(NullLogger<YamlContentLoader>.Instance);
        return loader.LoadLessons(FindContentRoot());
    }

    /// <summary>Đi ngược lên từ thư mục chạy test tới khi thấy thư mục content/.</summary>
    private static string FindContentRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "content");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Không tìm thấy thư mục content/ khi đi ngược từ " + AppContext.BaseDirectory);
    }
}

/// <summary>Kiểm chính bộ luật, bằng dữ liệu dựng sẵn — không phụ thuộc nội dung thật.</summary>
public class LessonValidatorRuleTests
{
    private static LessonDocument Valid(string code = "TEST-01") => new()
    {
        Code = code,
        Slug = code.ToLowerInvariant(),
        TitleVi = "Bài kiểm thử",
        TitleEn = "Test lesson",
        EstimatedMinutes = 11,
        ObjectiveVi = "Mục tiêu",
        ObjectiveObservable = "Đạt 80 điểm",
        MasteryWeights = new()
        {
            [Domain.Enums.SkillType.Listening] = 0.5,
            [Domain.Enums.SkillType.Speaking] = 0.5,
        },
        Vocabulary = [.. Enumerable.Range(1, 6).Select(i => new VocabularyDocument
        {
            Term = $"tu{i}", Ipa = "/x/", MeaningVi = "nghia", Chunk = "cum tu",
        })],
        Listening = new ListeningDocument { TranscriptEn = "Hello.", TranscriptVi = "Xin chào." },
        SpeakingDrills = [.. Enumerable.Range(1, 4).Select(i => new SpeakingDrillDocument
        {
            Kind = "read_aloud", ExpectedText = $"Sentence {i}.", PromptVi = "Đọc to",
        })],
        Explanation = new ExplanationDocument { WhyVi = "Vì sao", HowVi = "Cách làm" },
        CommonMistakes =
        [
            new CommonMistakeDocument { Mistake = "Lỗi 1", WhyVi = "Vì", FixVi = "Sửa" },
            new CommonMistakeDocument { Mistake = "Lỗi 2", WhyVi = "Vì", FixVi = "Sửa" },
        ],
    };

    [Fact]
    public void BaiHopLeThiKhongCoLoi()
    {
        Assert.Empty(new LessonValidator().ValidateOne(Valid()));
    }

    [Fact]
    public void ChanKhiTongTrongSoKhacMot()
    {
        var doc = Valid();
        doc.MasteryWeights[Domain.Enums.SkillType.Listening] = 0.9;

        Assert.Contains(new LessonValidator().ValidateOne(doc), i => i.Code == "E041");
    }

    [Fact]
    public void ChanKhiBaiDaiQuaMuoiHaiPhut()
    {
        var doc = Valid();
        doc.EstimatedMinutes = 15;

        Assert.Contains(new LessonValidator().ValidateOne(doc), i => i.Code == "E020");
    }

    [Fact]
    public void ChanKhiDuoiHaiLoiThuongGap()
    {
        var doc = Valid();
        doc.CommonMistakes.RemoveAt(0);

        Assert.Contains(new LessonValidator().ValidateOne(doc), i => i.Code == "E011");
    }

    [Fact]
    public void ChanKhiThieuGiaiThichTiengViet()
    {
        var doc = Valid();
        doc.Explanation = new ExplanationDocument { WhyVi = "", HowVi = "" };

        Assert.Contains(new LessonValidator().ValidateOne(doc), i => i.Code == "E010");
    }

    [Theory]
    [InlineData(4)]
    [InlineData(9)]
    public void ChanKhiSoTuVungNgoaiKhoangNamToiTam(int count)
    {
        var doc = Valid();
        doc.Vocabulary = [.. Enumerable.Range(1, count).Select(i => new VocabularyDocument
        {
            Term = $"tu{i}", Ipa = "/x/", MeaningVi = "nghia", Chunk = "cum",
        })];

        Assert.Contains(new LessonValidator().ValidateOne(doc), i => i.Code == "E030");
    }

    [Fact]
    public void ChanKhiKhaiKyNangMaBaiKhongDay()
    {
        var doc = Valid();
        doc.SupportedSkills = [Domain.Enums.SkillType.Writing];

        Assert.Contains(new LessonValidator().ValidateOne(doc), i => i.Code == "E051");
    }

    [Fact]
    public void ChanKhiTinhTrongSoChoKyNangKhongDay()
    {
        var doc = Valid();
        doc.MasteryWeights = new()
        {
            [Domain.Enums.SkillType.Listening] = 0.5,
            [Domain.Enums.SkillType.Writing] = 0.5,
        };

        Assert.Contains(new LessonValidator().ValidateOne(doc), i => i.Code == "E052");
    }

    [Fact]
    public void ChanKhiDapAnDonHetVeMotViTri()
    {
        // Sáu trên tám câu cùng ô: chọn mãi ô đó là qua bài mà không cần đọc câu hỏi,
        // và qua được cả bài thi vượt.
        var doc = Valid();
        doc.Quiz =
        [
            .. Enumerable.Range(0, 6).Select(_ => new QuizItemDocument { Choices = ["a", "b", "c"], Answer = 1 }),
            new QuizItemDocument { Choices = ["a", "b", "c"], Answer = 0 },
            new QuizItemDocument { Choices = ["a", "b", "c"], Answer = 2 },
        ];

        Assert.Contains(new LessonValidator().ValidateOne(doc), i => i.Code == "E072");
    }

    [Fact]
    public void DapAnTraiDeuThiKhongKeu()
    {
        var doc = Valid();
        doc.Quiz =
        [
            new QuizItemDocument { Choices = ["a", "b", "c"], Answer = 0 },
            new QuizItemDocument { Choices = ["a", "b", "c"], Answer = 1 },
            new QuizItemDocument { Choices = ["a", "b", "c"], Answer = 2 },
            new QuizItemDocument { Choices = ["a", "b", "c"], Answer = 1 },
            new QuizItemDocument { Choices = ["a", "b", "c"], Answer = 0 },
            new QuizItemDocument { Choices = ["a", "b", "c"], Answer = 2 },
        ];

        Assert.DoesNotContain(new LessonValidator().ValidateOne(doc), i => i.Code == "E072");
    }

    [Fact]
    public void ChanKhiDapAnNgoaiPhamViLuaChon()
    {
        var doc = Valid();
        doc.Quiz = [new QuizItemDocument { Choices = ["a", "b"], Answer = 5 }];

        Assert.Contains(new LessonValidator().ValidateOne(doc), i => i.Code == "E071");
    }

    [Fact]
    public void ChanKhiTienQuyetTroToiBaiKhongTonTai()
    {
        var doc = Valid();
        doc.Prerequisites = [new PrerequisiteDocument { Lesson = "KHONG-CO" }];

        Assert.Contains(new LessonValidator().ValidateSet([doc]), i => i.Code == "E102");
    }

    [Fact]
    public void ChanKhiBaiDeTienQuyetBaiKho()
    {
        var de = Valid("DE-01");
        de.Level = Domain.Enums.CefrLevel.PreA1;
        de.Prerequisites = [new PrerequisiteDocument { Lesson = "KHO-01" }];

        var kho = Valid("KHO-01");
        kho.Level = Domain.Enums.CefrLevel.B1;

        // Bài dễ bị khoá sau bài khó là lỗi chặn publish: học viên sẽ không bao giờ
        // mở được bài nhập môn.
        Assert.Contains(new LessonValidator().ValidateSet([de, kho]), i => i.Code == "E103");
    }

    [Fact]
    public void ChanKhiDagCoChuTrinh()
    {
        var a = Valid("A-01");
        a.Prerequisites = [new PrerequisiteDocument { Lesson = "B-01" }];

        var b = Valid("B-01");
        b.Prerequisites = [new PrerequisiteDocument { Lesson = "A-01" }];

        Assert.Contains(new LessonValidator().ValidateSet([a, b]), i => i.Code == "E104");
    }

    [Fact]
    public void ChanKhiMaBaiTrungNhau()
    {
        Assert.Contains(new LessonValidator().ValidateSet([Valid("X-01"), Valid("X-01")]),
            i => i.Code == "E100");
    }
}
