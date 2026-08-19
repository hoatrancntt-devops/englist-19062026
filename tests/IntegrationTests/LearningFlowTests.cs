using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace EnglishForIT.IntegrationTests;

/// <summary>
/// Vòng học lõi qua HTTP thật.
///
/// Bất biến quan trọng nhất ở đây là đáp án không rời máy chủ. Nó từng bị vi phạm một lần
/// (payload bước Nghe mang nguyên đối tượng câu hỏi kèm answer) và không có gì báo động —
/// người dùng chỉ cần mở tab Network là thấy hết.
/// </summary>
[Collection(ApiCollection.Name)]
public class LearningFlowTests(ApiFactory api)
{
    [Fact]
    public async Task NoiDungBaiKhongBaoGioKemDapAn()
    {
        var client = await api.NewLearnerAsync();

        var response = await client.GetAsync("/api/v1/learning/lessons/LIFE-01");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadAsStringAsync();

        // Tra chuỗi thô chứ không tra theo DTO: chỗ rò trước đây nằm trong một cột JSON
        // được ghi nguyên khối, mà đọc theo DTO thì không nhìn thấy.
        Assert.DoesNotContain("\"answer\"", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("correctIndex", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoTrinhSapDungThuTuBacVaNeuConSoConThieu()
    {
        var client = await api.NewLearnerAsync();

        var roadmap = await client.GetFromJsonAsync<JsonElement>("/api/v1/learning/roadmap");
        var lessons = roadmap.GetProperty("lessons").EnumerateArray().ToList();

        Assert.Equal(86, lessons.Count);

        // Bài đầu tiên phải mở sẵn, nếu không học viên mới không có chỗ nào bắt đầu.
        var first = lessons.First(l => l.GetProperty("code").GetString() == "LIFE-01");
        Assert.Equal("Available", first.GetProperty("state").GetString());

        // LIFE-02 cũng mở sẵn: tiên quyết của nó là loại MỀM, chỉ gợi ý thứ tự chứ không chặn.
        // Hai bài đầu đều là cửa vào, cố ý như vậy.
        Assert.Equal("Available", lessons.First(l => l.GetProperty("code").GetString() == "LIFE-02")
            .GetProperty("state").GetString());

        // LIFE-03 tiên quyết CỨNG nên bị chặn, và lý do phải kèm con số cụ thể
        // chứ không phải câu "chưa đủ điều kiện".
        var third = lessons.First(l => l.GetProperty("code").GetString() == "LIFE-03");
        var reason = third.GetProperty("lockExplanationVi").GetString() ?? "";

        Assert.NotEqual("Available", third.GetProperty("state").GetString());
        Assert.Contains("LIFE-02", reason);
        Assert.Matches(@"\d+", reason);

        // Cột level lưu dạng chuỗi nên ORDER BY của Postgres từng sắp "A1" trước "PreA1",
        // đẩy LIFE-06 lên trên LIFE-01. Thứ tự phải đúng theo giá trị enum.
        var lifeCodes = lessons
            .Where(l => l.GetProperty("layer").GetString() == "Life")
            .Select(l => l.GetProperty("code").GetString()!)
            .ToList();

        Assert.Equal("LIFE-01", lifeCodes[0]);
        Assert.Equal("LIFE-02", lifeCodes[1]);
    }

    [Fact]
    public async Task ChamTracNghiemTaiMayChuVaTraVeDapAnDungSauKhiNop()
    {
        var client = await api.NewLearnerAsync();

        var lesson = await client.GetFromJsonAsync<JsonElement>("/api/v1/learning/lessons/LIFE-01");
        var quiz = lesson.GetProperty("activities")
            .EnumerateArray()
            .First(a => a.GetProperty("items").GetArrayLength() > 0);

        var items = quiz.GetProperty("items").EnumerateArray().ToList();

        // Cố tình chọn sai hết để chắc rằng điểm do máy chủ tính chứ không do client gửi lên.
        var responses = items
            .Select(i => new { itemCode = i.GetProperty("code").GetString(), chosenIndex = 99 })
            .ToList();

        var graded = await client.PostAsJsonAsync(
            $"/api/v1/learning/lessons/LIFE-01/activities",
            new
            {
                activityId = quiz.GetProperty("id").GetGuid(),
                responses,
                durationSeconds = 30,
            });

        graded.EnsureSuccessStatusCode();
        var grade = await graded.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(0, grade.GetProperty("score").GetDouble());
        Assert.False(grade.GetProperty("passed").GetBoolean());

        // Sau khi chấm thì được biết đáp án đúng — đó là lúc nó có ích cho việc học.
        Assert.All(
            grade.GetProperty("items").EnumerateArray(),
            item => Assert.True(item.GetProperty("correctIndex").GetInt32() >= 0));
    }

    [Fact]
    public async Task HocTronBaiThiBaiKeTiepMoKhoa()
    {
        var client = await api.NewLearnerAsync();

        await CompleteLessonAsync(client, "LIFE-01");

        var roadmap = await client.GetFromJsonAsync<JsonElement>("/api/v1/learning/roadmap");
        var lessons = roadmap.GetProperty("lessons").EnumerateArray().ToList();

        var first = lessons.First(l => l.GetProperty("code").GetString() == "LIFE-01");
        var third = lessons.First(l => l.GetProperty("code").GetString() == "LIFE-03");

        Assert.Equal("Mastered", first.GetProperty("state").GetString());
        Assert.Equal(1, roadmap.GetProperty("mastered").GetInt32());

        // LIFE-03 vẫn khoá vì nó phụ thuộc LIFE-02 chứ không phải LIFE-01 —
        // mở khoá phải theo đúng đồ thị, không phải theo thứ tự đánh số.
        Assert.NotEqual("Available", third.GetProperty("state").GetString());
    }

    [Fact]
    public async Task HocThatSauKhiDanhDauBietThiMoDuocBaiKeTiep()
    {
        var client = await api.NewLearnerAsync();

        // Danh sách câu lấy thẳng từ API thi vượt, không dựng lại bộ lọc riêng: bài thi vượt
        // chỉ gồm phần chấm được, còn AnswerKeyAsync trả cả bài nên phải giao hai bên.
        var full = await AnswerKeyAsync("LIFE-03");
        var paper = await client.GetFromJsonAsync<JsonElement>("/api/v1/learning/lessons/LIFE-03/challenge");

        // Đề trả về dùng "code"; chỉ phía nộp bài mới gọi là "itemCode".
        var responses = paper.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("code").GetString()!)
            .Where(full.ContainsKey)
            .Select(code => new { itemCode = code, chosenIndex = full[code] })
            .ToList();

        var passed = await client.PostAsJsonAsync(
            "/api/v1/learning/lessons/LIFE-03/challenge", new { responses });

        Assert.True((await passed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("passed").GetBoolean());
        Assert.Equal("Locked", await StateAsync(client, "LIFE-04"));

        // Học thật bài đã đánh dấu. Đây là đường THOÁT duy nhất, và nếu nó hỏng thì người từng
        // đánh dấu biết sẽ kẹt vĩnh viễn: cờ chỉ được bật, không bao giờ tắt, và họ không có
        // cách nào tự gỡ.
        await CompleteLessonAsync(client, "LIFE-03");

        var roadmap = await client.GetFromJsonAsync<JsonElement>("/api/v1/learning/roadmap");
        var life03 = roadmap.GetProperty("lessons").EnumerateArray()
            .First(l => l.GetProperty("code").GetString() == "LIFE-03");

        Assert.False(life03.GetProperty("unlockedByChallenge").GetBoolean());
        Assert.Equal(1, roadmap.GetProperty("mastered").GetInt32());
        Assert.Equal("Available", await StateAsync(client, "LIFE-04"));
    }

    [Fact]
    public async Task ChonLinhVucVaCheDoHocThiLuuLaiVaLocDungBuoc()
    {
        var client = await api.NewLearnerAsync();

        // Chỉ được chọn lĩnh vực CÓ bài thật. Liệt kê thẳng từ enum thì học viên chọn phải
        // một nhánh rỗng rồi nhìn lộ trình trắng trơn mà không hiểu vì sao.
        var before = await client.GetFromJsonAsync<JsonElement>("/api/v1/learning/preferences");
        var tracks = before.GetProperty("tracks").EnumerateArray().ToList();

        Assert.All(tracks, t => Assert.True(t.GetProperty("lessonCount").GetInt32() > 0));
        Assert.Contains(tracks, t => t.GetProperty("value").GetString() == "Restaurant");
        Assert.False(before.GetProperty("onboardingCompleted").GetBoolean());

        // Đủ bảy bước khi để chế độ mặc định.
        var full = await client.GetFromJsonAsync<JsonElement>("/api/v1/learning/lessons/LIFE-01");
        Assert.Equal(7, full.GetProperty("activities").GetArrayLength());

        var saved = await client.PutAsJsonAsync("/api/v1/learning/preferences",
            new { primaryTrack = "Restaurant", studyMode = "ListeningOnly" });

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        // Chọn "chỉ nghe" thì bài chỉ còn bước nghe, từ vựng và quiz.
        //
        // Đây là chỗ trước đây hỏng trong im lặng: StudyMode được lưu và hiển thị trên bảng
        // điều khiển nhưng KHÔNG lọc gì, nên lựa chọn của học viên không có tác dụng nào.
        var filtered = await client.GetFromJsonAsync<JsonElement>("/api/v1/learning/lessons/LIFE-01");
        var kinds = filtered.GetProperty("activities").EnumerateArray()
            .Select(a => a.GetProperty("kind").GetString())
            .ToList();

        Assert.DoesNotContain("Speak", kinds);
        Assert.DoesNotContain("Write", kinds);
        Assert.DoesNotContain("Read", kinds);
        Assert.Contains("Listen", kinds);

        var after = await client.GetFromJsonAsync<JsonElement>("/api/v1/learning/preferences");
        Assert.Equal("Restaurant", after.GetProperty("primaryTrack").GetString());
        Assert.True(after.GetProperty("onboardingCompleted").GetBoolean());
    }

    [Fact]
    public async Task ChonLinhVucKhongCoBaiThiBiTuChoi()
    {
        var client = await api.NewLearnerAsync();

        // Foundation có bài nên hợp lệ; tên bịa thì phải bị chặn ngay ở API chứ không
        // được lưu vào hồ sơ rồi mới lộ ra là lộ trình trắng.
        var bad = await client.PutAsJsonAsync("/api/v1/learning/preferences",
            new { primaryTrack = "KhongTonTai", studyMode = "Mixed" });

        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);

        var badMode = await client.PutAsJsonAsync("/api/v1/learning/preferences",
            new { primaryTrack = "Foundation", studyMode = "KhongTonTai" });

        Assert.Equal(HttpStatusCode.BadRequest, badMode.StatusCode);
    }

    private static async Task<string?> StateAsync(HttpClient client, string code)
    {
        var roadmap = await client.GetFromJsonAsync<JsonElement>("/api/v1/learning/roadmap");

        return roadmap.GetProperty("lessons").EnumerateArray()
            .First(l => l.GetProperty("code").GetString() == code)
            .GetProperty("state").GetString();
    }

    [Fact]
    public async Task HocXongThiCauVaoHangDoiOnTap()
    {
        var client = await api.NewLearnerAsync();

        await CompleteLessonAsync(client, "LIFE-01");

        var review = await client.GetFromJsonAsync<JsonElement>("/api/v1/learning/review");

        // Câu vừa học hẹn ôn sau một ngày nên phiên ôn hôm nay còn rỗng —
        // đúng thiết kế, và thông điệp phải nói được điều đó.
        Assert.False(string.IsNullOrWhiteSpace(review.GetProperty("messageVi").GetString()));

        await using var scope = api.NewScope();
        var db = ApiFactory.Db(scope);

        Assert.True(await db.ReviewQueue.AnyAsync());
    }

    [Fact]
    public async Task BangDieuKhienChuaXepLopThiBaoDungTrangThai()
    {
        var client = await api.NewLearnerAsync();

        var dashboard = await client.GetFromJsonAsync<JsonElement>("/api/v1/learning/dashboard");

        Assert.False(dashboard.GetProperty("placementCompleted").GetBoolean());
    }

    [Fact]
    public async Task HocXongThiChuoiNgayGhiNhanVaNoiRoConThieuGi()
    {
        var client = await api.NewLearnerAsync();

        await CompleteLessonAsync(client, "LIFE-01");

        var dashboard = await client.GetFromJsonAsync<JsonElement>("/api/v1/learning/dashboard");
        var streak = dashboard.GetProperty("streak");

        // Một bài ~11 phút nên chưa đủ mục tiêu 45 phút: chuỗi chưa tăng, và màn hình
        // phải nói thẳng còn thiếu bao nhiêu thay vì hiện số 0 im lặng.
        Assert.True(streak.GetProperty("minutesToday").GetInt32() > 0);
        Assert.Equal(0, streak.GetProperty("current").GetInt32());
        Assert.Contains("phút", streak.GetProperty("reasonVi").GetString() ?? "");

        // Bước Nói không chấm được nhưng vẫn phải tính là ĐÃ CHẠM, nếu không
        // luật "đủ bốn kỹ năng" khiến không ai xây được chuỗi.
        var touched = streak.GetProperty("skillsTouchedToday").EnumerateArray()
            .Select(s => s.GetString()).ToList();

        Assert.Contains("Speaking", touched);
    }

    [Fact]
    public async Task BaiKhongCoThatTraVe404()
    {
        var client = await api.NewLearnerAsync();

        var response = await client.GetAsync("/api/v1/learning/lessons/KHONG-CO-BAI-NAY");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Làm hết các bước chấm được của một bài rồi chốt bài.</summary>
    private async Task CompleteLessonAsync(HttpClient client, string code)
    {
        var key = await AnswerKeyAsync(code);
        var lesson = await client.GetFromJsonAsync<JsonElement>($"/api/v1/learning/lessons/{code}");

        foreach (var activity in lesson.GetProperty("activities").EnumerateArray())
        {
            var kind = activity.GetProperty("kind").GetString();
            var activityId = activity.GetProperty("id").GetGuid();

            // Bước Nói vẫn phải NỘP: nó không được chấm nhưng có ghi lại là đã làm,
            // và chuỗi ngày đọc chính bản ghi đó.
            if (kind is "Shadow" or "Speak")
            {
                await client.PostAsJsonAsync($"/api/v1/learning/lessons/{code}/activities", new
                {
                    activityId,
                    responses = Array.Empty<object>(),
                    durationSeconds = 60,
                    textAnswers = Array.Empty<string>(),
                });

                continue;
            }

            if (kind == "Write")
            {
                await client.PostAsJsonAsync($"/api/v1/learning/lessons/{code}/activities", new
                {
                    activityId,
                    responses = Array.Empty<object>(),
                    durationSeconds = 30,
                    textAnswers = await WritingAnswersAsync(code),
                });

                continue;
            }

            var items = activity.GetProperty("items").EnumerateArray().ToList();

            var responses = items
                .Select(i => new
                {
                    itemCode = i.GetProperty("code").GetString(),
                    chosenIndex = key.GetValueOrDefault(i.GetProperty("code").GetString()!, 0),
                })
                .ToList();

            await client.PostAsJsonAsync($"/api/v1/learning/lessons/{code}/activities", new
            {
                activityId,
                responses,
                durationSeconds = 30,
                textAnswers = Array.Empty<string>(),
            });
        }

        await client.PostAsync($"/api/v1/learning/lessons/{code}/submit", null);
    }

    /// <summary>
    /// Câu trả lời đúng cho bước Viết, dựng từ rubric trong DB.
    ///
    /// Bỏ trống bước này thì trục Viết bị 0 điểm và bài không bao giờ đạt — đó là hành vi
    /// đúng của engine, nên test muốn dựng một lượt học đạt thì phải làm cả bước Viết.
    /// </summary>
    private async Task<string[]> WritingAnswersAsync(string lessonCode)
    {
        await using var scope = api.NewScope();
        var db = ApiFactory.Db(scope);

        var payload = await db.LessonActivities
            .Where(a => a.Lesson!.Code == lessonCode && a.Kind == Domain.Enums.ActivityKind.Write)
            .Select(a => a.PayloadJson)
            .FirstOrDefaultAsync();

        if (payload is null)
        {
            return [];
        }

        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;

        var kind = root.TryGetProperty("Kind", out var k) ? k.GetString() : null;

        return kind switch
        {
            // Mỗi chỗ trống lấy phương án được chấp nhận đầu tiên.
            "FillBlank" => [.. root.GetProperty("Blanks").EnumerateArray()
                .Select(b => b.EnumerateArray().First().GetString() ?? "")],

            "Reorder" => [.. root.GetProperty("CorrectOrder").EnumerateArray()
                .Select(o => o.GetString() ?? "")],

            // Email có hướng dẫn chấm theo từ khoá bắt buộc, nên bài mẫu là câu trả lời đạt.
            "GuidedEmail" => [root.GetProperty("SampleEn").GetString() ?? ""],

            _ => [],
        };
    }

    /// <summary>
    /// Đáp án đúng của một bài, đọc thẳng từ DB.
    ///
    /// Test phải biết đáp án mới dựng được một lượt học đạt. API cố ý không cho biết,
    /// và đó chính là điều <see cref="NoiDungBaiKhongBaoGioKemDapAn"/> canh giữ.
    /// </summary>
    private async Task<Dictionary<string, int>> AnswerKeyAsync(string lessonCode)
    {
        await using var scope = api.NewScope();
        var db = ApiFactory.Db(scope);

        var rows = await db.LessonItems
            .Where(i => i.Activity!.Lesson!.Code == lessonCode)
            .Select(i => new { i.Code, i.AnswerJson })
            .ToListAsync();

        var key = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            using var doc = JsonDocument.Parse(row.AnswerJson);

            if (doc.RootElement.TryGetProperty("Answer", out var answer) && answer.TryGetInt32(out var index))
            {
                key[row.Code] = index;
            }
        }

        return key;
    }
}
