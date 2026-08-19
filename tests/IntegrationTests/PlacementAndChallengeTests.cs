using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace EnglishForIT.IntegrationTests;

/// <summary>
/// Xếp lớp và thi vượt.
///
/// Hai tính năng này quyết định học viên bắt đầu ở đâu và được bỏ qua cái gì, nên sai ở đây
/// tốn của họ hàng tuần học sai chỗ. Cả hai đều có đường tắt hấp dẫn để lách, nên phần lớn
/// test dưới đây kiểm chính các luật chặn chứ không kiểm đường hạnh phúc.
/// </summary>
[Collection(ApiCollection.Name)]
public class PlacementAndChallengeTests(ApiFactory api)
{
    // -----------------------------------------------------------------------
    // Xếp lớp
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DeXepLopPhucVu22CauVaKhongKemDapAn()
    {
        var client = await api.NewLearnerAsync();

        var response = await client.PostAsync("/api/v1/placement/start", null);
        response.EnsureSuccessStatusCode();

        var raw = await response.Content.ReadAsStringAsync();
        var session = JsonDocument.Parse(raw).RootElement;

        var cards = session.GetProperty("cards").EnumerateArray().ToList();

        // Soạn 26 câu, phục vụ 22: bốn câu Nói bị giữ lại vì chưa có bộ chấm phát âm.
        Assert.Equal(22, cards.Count);
        Assert.DoesNotContain(cards, c => c.GetProperty("kind").GetString() is "ReadAloud" or "Repeat");

        Assert.DoesNotContain("correctIndex", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("accepted", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("mustContain", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GoiStartLanHaiThiMoLaiDungLuotDangLam()
    {
        var client = await api.NewLearnerAsync();

        var first = await StartAsync(client);
        var attemptId = first.GetProperty("attemptId").GetGuid();
        var itemCode = first.GetProperty("cards")[0].GetProperty("itemCode").GetString();

        await client.PostAsJsonAsync("/api/v1/placement/answer", new
        {
            attemptId,
            itemCode,
            response = new { choiceIndex = 0 },
            responseSeconds = 20,
        });

        var second = await StartAsync(client);

        Assert.Equal(attemptId, second.GetProperty("attemptId").GetGuid());
        Assert.True(second.GetProperty("resumed").GetBoolean());
        Assert.Contains(
            itemCode,
            second.GetProperty("answeredItemCodes").EnumerateArray().Select(c => c.GetString()));
    }

    [Fact]
    public async Task CauKhongThuocLuotThiTraVe404KemCauChuTrungTinh()
    {
        var client = await api.NewLearnerAsync();
        var session = await StartAsync(client);

        // Câu Nói có thật trong đề nhưng không được phục vụ. Phản hồi phải giống hệt
        // trường hợp mã bịa, nếu không nó thành kênh dò xem đề có những câu gì.
        var speaking = await client.PostAsJsonAsync("/api/v1/placement/answer", new
        {
            attemptId = session.GetProperty("attemptId").GetGuid(),
            itemCode = "A-SPK-1",
            response = new { text = "hello" },
            responseSeconds = 5,
        });

        var nonsense = await client.PostAsJsonAsync("/api/v1/placement/answer", new
        {
            attemptId = session.GetProperty("attemptId").GetGuid(),
            itemCode = "KHONG-CO-MA-NAY",
            response = new { choiceIndex = 0 },
            responseSeconds = 5,
        });

        Assert.Equal(HttpStatusCode.NotFound, speaking.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, nonsense.StatusCode);
        Assert.Equal(
            await speaking.Content.ReadAsStringAsync(),
            await nonsense.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task TrucNoiBaoChuaDoDuocChuKhongPhaiKhongDiem()
    {
        var client = await api.NewLearnerAsync();
        var session = await StartAsync(client);

        var result = await SubmitPlacementAsync(client, session, allCorrect: true);

        Assert.Contains("Speaking", result.GetProperty("unmeasuredSkills").EnumerateArray().Select(s => s.GetString()));

        // Không có khoá Speaking trong bảng điểm: có mặt với giá trị 0 sẽ kéo mọi học viên
        // xuống một bậc bằng một phép đo không tồn tại.
        Assert.False(result.GetProperty("skillScores").TryGetProperty("Speaking", out _));
    }

    [Fact]
    public async Task LamDungHetThiRaBacCaoNhatVaGhiVaoHoSo()
    {
        var client = await api.NewLearnerAsync();
        var session = await StartAsync(client);

        var result = await SubmitPlacementAsync(client, session, allCorrect: true);

        Assert.Equal("L4", result.GetProperty("band").GetString());
        Assert.Equal(22, result.GetProperty("answered").GetInt32());

        var dashboard = await client.GetFromJsonAsync<JsonElement>("/api/v1/learning/dashboard");
        Assert.True(dashboard.GetProperty("placementCompleted").GetBoolean());
    }

    [Fact]
    public async Task ThiLaiThiBocDeCon_lai()
    {
        var client = await api.NewLearnerAsync();

        var first = await StartAsync(client);
        var firstForm = first.GetProperty("formCode").GetString();
        await SubmitPlacementAsync(client, first, allCorrect: true);

        var second = await StartAsync(client);

        // Gặp lại nguyên đề cũ thì điểm tăng vì nhớ đáp án, không phải vì giỏi lên.
        Assert.NotEqual(firstForm, second.GetProperty("formCode").GetString());
    }

    // -----------------------------------------------------------------------
    // Thi vượt
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DeThiVuotKhongKemDapAn()
    {
        var client = await api.NewLearnerAsync();

        var response = await client.GetAsync("/api/v1/learning/lessons/SEC-01/challenge");
        response.EnsureSuccessStatusCode();

        var raw = await response.Content.ReadAsStringAsync();

        Assert.True(JsonDocument.Parse(raw).RootElement.GetProperty("eligible").GetBoolean());
        Assert.DoesNotContain("\"answer\"", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TruotThiVuotThiPhaiChoVaTrangThaiBaiKhongTeDiHon()
    {
        var client = await api.NewLearnerAsync();

        var offer = await client.GetFromJsonAsync<JsonElement>("/api/v1/learning/lessons/SEC-01/challenge");

        var allWrong = offer.GetProperty("items").EnumerateArray()
            .Select(i => new { itemCode = i.GetProperty("code").GetString(), chosenIndex = -1 })
            .ToList();

        var failed = await client.PostAsJsonAsync(
            "/api/v1/learning/lessons/SEC-01/challenge", new { responses = allWrong });

        var result = await failed.Content.ReadFromJsonAsync<JsonElement>();

        Assert.False(result.GetProperty("passed").GetBoolean());
        Assert.NotNull(result.GetProperty("retryAt").GetString());

        // Thi lại ngay với đáp án ĐÚNG HẾT vẫn phải bị chặn, nếu không thi vượt
        // biến thành trò dò đáp án.
        var key = await ChallengeAnswerKeyAsync("SEC-01");

        var retry = await client.PostAsJsonAsync("/api/v1/learning/lessons/SEC-01/challenge", new
        {
            responses = key.Select(kv => new { itemCode = kv.Key, chosenIndex = kv.Value }).ToList(),
        });

        var retryResult = await retry.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(retryResult.GetProperty("passed").GetBoolean());

        var blocked = await client.GetFromJsonAsync<JsonElement>("/api/v1/learning/lessons/SEC-01/challenge");
        Assert.False(blocked.GetProperty("eligible").GetBoolean());
        Assert.Empty(blocked.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task QuaThiVuotThiMoBaiDuBoQuaMoiTienQuyet()
    {
        var client = await api.NewLearnerAsync();

        var key = await ChallengeAnswerKeyAsync("CLD-06");

        var response = await client.PostAsJsonAsync("/api/v1/learning/lessons/CLD-06/challenge", new
        {
            responses = key.Select(kv => new { itemCode = kv.Key, chosenIndex = kv.Value }).ToList(),
        });

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(result.GetProperty("passed").GetBoolean());
        Assert.Equal(100, result.GetProperty("score").GetDouble());

        var roadmap = await client.GetFromJsonAsync<JsonElement>("/api/v1/learning/roadmap");
        var lessons = roadmap.GetProperty("lessons").EnumerateArray().ToList();

        // CLD-06 thạo trong khi CLD-05 vẫn khoá: đó đúng là ý nghĩa của đường tắt.
        Assert.Equal("Mastered", lessons.First(l => l.GetProperty("code").GetString() == "CLD-06")
            .GetProperty("state").GetString());
        Assert.NotEqual("Mastered", lessons.First(l => l.GetProperty("code").GetString() == "CLD-05")
            .GetProperty("state").GetString());
    }

    [Fact]
    public async Task ThiVuotKhongMoKhoaBaiKeTiep()
    {
        var client = await api.NewLearnerAsync();

        var key = await ChallengeAnswerKeyAsync("HD-01");

        var response = await client.PostAsJsonAsync("/api/v1/learning/lessons/HD-01/challenge", new
        {
            responses = key.Select(kv => new { itemCode = kv.Key, chosenIndex = kv.Value }).ToList(),
        });

        Assert.True((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("passed").GetBoolean());

        var roadmap = await client.GetFromJsonAsync<JsonElement>("/api/v1/learning/roadmap");
        var lessons = roadmap.GetProperty("lessons").EnumerateArray().ToList();

        var hd01 = lessons.First(l => l.GetProperty("code").GetString() == "HD-01");
        var hd02 = lessons.First(l => l.GetProperty("code").GetString() == "HD-02");

        // Thi vượt chỉ miễn học BÀI ĐÓ.
        Assert.Equal("Mastered", hd01.GetProperty("state").GetString());

        // Nhưng không được tính là nền để mở bài sau. Nếu tính, một người đi hết chuỗi bài
        // chỉ bằng trắc nghiệm mà không học bài nào.
        Assert.Equal("Locked", hd02.GetProperty("state").GetString());

        // Câu giải thích phải nói đúng lý do. Nói "bạn đang có 0 điểm" về một bài vừa hiện là
        // đã thạo thì người đọc kết luận hệ thống đếm sai chứ không hiểu mình cần học bài đó.
        Assert.Contains("thi vượt", hd02.GetProperty("lockExplanationVi").GetString());

        // Thẻ bài phải tự khai là qua bằng thi vượt, để giao diện hiện khác bài học thật.
        // Thiếu cờ này thì màn hình nói "đã thạo" ngay cạnh "bạn mới thi vượt chứ chưa học".
        Assert.True(hd01.GetProperty("unlockedByChallenge").GetBoolean());

        // Và không được cộng vào con số "đã thạo". Ô tóm tắt ghi rõ chữ "đã thạo" cạnh số này,
        // mà thi vượt cố ý không có nghĩa là đã thạo.
        Assert.Equal(0, roadmap.GetProperty("mastered").GetInt32());
    }

    [Fact]
    public async Task XepLopGhiCongBacDuoiVaKhongChamToiBacB1()
    {
        var client = await api.NewLearnerAsync();

        var result = await SubmitPlacementAsync(client, await StartAsync(client), allCorrect: true);
        Assert.Equal("B1", result.GetProperty("level").GetString());

        var roadmap = await client.GetFromJsonAsync<JsonElement>("/api/v1/learning/roadmap");

        var masteredLevels = roadmap.GetProperty("lessons").EnumerateArray()
            .Where(l => l.GetProperty("state").GetString() == "Mastered")
            .Select(l => l.GetProperty("level").GetString())
            .ToList();

        // Ghi công bậc THẤP HƠN bậc đạt được, không phải thấp hơn hoặc bằng. Quy tắc lỏng thành
        // "hoặc bằng" thì cả tầng Professional (đều B1) mở toang sau một lần thi.
        Assert.DoesNotContain("B1", masteredLevels);

        // Và phải đủ MỌI bậc dưới, kể cả bậc thấp nhất.
        //
        // Kiểm riêng PreA1 chứ không chỉ đếm tổng: cột level lưu dạng chuỗi, nên một phép so
        // sánh bậc lỡ chạy dưới SQL sẽ sắp theo bảng chữ cái và 'PreA1' rơi ra ngoài 'B1'.
        // Lỗi đó không ném gì cả, chỉ lặng lẽ bắt người đã đạt B1 ngồi học đánh vần.
        Assert.Contains("PreA1", masteredLevels);
        Assert.Contains("A1", masteredLevels);
        Assert.Contains("A2", masteredLevels);
    }

    [Fact]
    public async Task XepLopLanHaiKhongCongThemLanNua()
    {
        var client = await api.NewLearnerAsync();

        await SubmitPlacementAsync(client, await StartAsync(client), allCorrect: true);
        var before = await CountMasteredAsync(client);

        await SubmitPlacementAsync(client, await StartAsync(client), allCorrect: true);

        Assert.Equal(before, await CountMasteredAsync(client));
    }

    private static async Task<int> CountMasteredAsync(HttpClient client)
    {
        var roadmap = await client.GetFromJsonAsync<JsonElement>("/api/v1/learning/roadmap");

        return roadmap.GetProperty("lessons").EnumerateArray()
            .Count(l => l.GetProperty("state").GetString() == "Mastered");
    }

    [Fact]
    public async Task QuaThiVuotThiCauDungHenXaHonCauSai()
    {
        var client = await api.NewLearnerAsync();
        var key = await ChallengeAnswerKeyAsync("CLD-06");

        // Sai đúng một câu: vẫn thừa điểm để qua.
        var wrongCode = key.Keys.First();

        await client.PostAsJsonAsync("/api/v1/learning/lessons/CLD-06/challenge", new
        {
            responses = key.Select(kv => new
            {
                itemCode = kv.Key,
                chosenIndex = kv.Key == wrongCode ? (kv.Value + 1) % 3 : kv.Value,
            }).ToList(),
        });

        var me = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/me");
        var userId = me.GetProperty("id").GetGuid();

        await using var scope = api.NewScope();
        var db = ApiFactory.Db(scope);

        var rows = await db.ReviewQueue
            .Where(r => r.UserId == userId)
            .Join(db.LessonItems, r => r.ItemId, i => i.Id, (r, i) => new { i.Code, r.IntervalDays })
            .ToListAsync();

        Assert.Equal(1, rows.Single(r => r.Code == wrongCode).IntervalDays);
        Assert.All(rows.Where(r => r.Code != wrongCode), r => Assert.Equal(7, r.IntervalDays));
    }

    [Fact]
    public async Task DaThaoRoiThiKhongConThiVuotDuocNua()
    {
        var client = await api.NewLearnerAsync();
        var key = await ChallengeAnswerKeyAsync("CLD-06");

        await client.PostAsJsonAsync("/api/v1/learning/lessons/CLD-06/challenge", new
        {
            responses = key.Select(kv => new { itemCode = kv.Key, chosenIndex = kv.Value }).ToList(),
        });

        var offer = await client.GetFromJsonAsync<JsonElement>("/api/v1/learning/lessons/CLD-06/challenge");

        Assert.False(offer.GetProperty("eligible").GetBoolean());

        // Lý do từ chối phải khớp với những gì lộ trình đang nói. Người này thi vượt chứ chưa
        // học, mà lộ trình vẫn khoá bài sau đúng vì lẽ đó — nên bảo họ "đã thạo" ở đây là hệ
        // thống tự cãi nhau giữa hai màn hình liền nhau.
        var reason = offer.GetProperty("reasonVi").GetString();

        Assert.DoesNotContain("đã thạo", reason);
        Assert.Contains("thi vượt", reason);
    }

    // -----------------------------------------------------------------------
    // Roleplay
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DeRoleplayKhongLoChatLuongLuaChon()
    {
        var client = await api.NewLearnerAsync();

        var response = await client.PostAsync("/api/v1/roleplay/RP-01/start", null);
        response.EnsureSuccessStatusCode();

        var raw = await response.Content.ReadAsStringAsync();

        // Thấy trước nhãn "good" thì bài này thành trò bấm nhãn.
        Assert.DoesNotContain("quality", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("feedbackVi", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("curt", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ChoiTronMotLuotRoleplayVaDuocCham()
    {
        var client = await api.NewLearnerAsync();

        var start = await (await client.PostAsync("/api/v1/roleplay/RP-02/start", null))
            .Content.ReadFromJsonAsync<JsonElement>();

        var attemptId = start.GetProperty("attemptId").GetGuid();
        var turn = start.GetProperty("turn");

        JsonElement? result = null;

        // Luôn chọn phương án đầu — kịch bản soạn sao cho đó là đường đạt.
        for (var i = 0; i < 10 && !turn.GetProperty("isTerminal").GetBoolean(); i++)
        {
            var answer = await (await client.PostAsJsonAsync("/api/v1/roleplay/choose", new
            {
                attemptId,
                nodeCode = turn.GetProperty("nodeCode").GetString(),
                choiceIndex = 0,
            })).Content.ReadFromJsonAsync<JsonElement>();

            turn = answer.GetProperty("next");

            if (answer.TryGetProperty("result", out var r) && r.ValueKind != JsonValueKind.Null)
            {
                result = r;
            }
        }

        Assert.NotNull(result);
        Assert.Equal(100, result!.Value.GetProperty("score").GetDouble());
        Assert.Equal("Completed", result.Value.GetProperty("outcome").GetString());
        Assert.Equal(0, result.Value.GetProperty("curtChoices").GetInt32());
    }

    [Fact]
    public async Task LuaChonKhongThuocLuotChoiTraVe404()
    {
        var client = await api.NewLearnerAsync();

        var response = await client.PostAsJsonAsync("/api/v1/roleplay/choose", new
        {
            attemptId = Guid.NewGuid(),
            nodeCode = "n1",
            choiceIndex = 0,
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // -----------------------------------------------------------------------

    private static async Task<JsonElement> StartAsync(HttpClient client)
    {
        var response = await client.PostAsync("/api/v1/placement/start", null);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>Trả lời toàn bộ câu của lượt thi rồi nộp.</summary>
    private async Task<JsonElement> SubmitPlacementAsync(
        HttpClient client, JsonElement session, bool allCorrect)
    {
        var attemptId = session.GetProperty("attemptId").GetGuid();
        var key = await PlacementAnswerKeyAsync(session.GetProperty("formCode").GetString()!);

        foreach (var card in session.GetProperty("cards").EnumerateArray())
        {
            var code = card.GetProperty("itemCode").GetString()!;
            var kind = card.GetProperty("kind").GetString();

            object answer = key.TryGetValue(code, out var value) && allCorrect
                ? value
                : kind == "Likert" ? new { choiceIndex = 2 } : new { choiceIndex = 0 };

            await client.PostAsJsonAsync("/api/v1/placement/answer", new
            {
                attemptId,
                itemCode = code,
                response = answer,
                responseSeconds = 30,
            });
        }

        var submit = await client.PostAsJsonAsync("/api/v1/placement/submit", new { attemptId });
        submit.EnsureSuccessStatusCode();

        return await submit.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>Đáp án của một đề xếp lớp, đọc thẳng từ DB vì API cố ý không trả ra.</summary>
    private async Task<Dictionary<string, object>> PlacementAnswerKeyAsync(string formCode)
    {
        await using var scope = api.NewScope();
        var db = ApiFactory.Db(scope);

        var rows = await db.PlacementFormItems
            .Where(i => i.Form!.Code == formCode)
            .Select(i => new { i.Code, i.AnswerJson })
            .ToListAsync();

        var key = new Dictionary<string, object>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            using var doc = JsonDocument.Parse(row.AnswerJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("correctIndex", out var index) && index.ValueKind == JsonValueKind.Number)
            {
                key[row.Code] = new { choiceIndex = index.GetInt32() };
            }
            else if (root.TryGetProperty("accepted", out var accepted) && accepted.GetArrayLength() > 0)
            {
                key[row.Code] = new { text = accepted[0].GetString() ?? "" };
            }
        }

        return key;
    }

    /// <summary>Đáp án các câu được dùng trong bài thi vượt của một bài học.</summary>
    private async Task<Dictionary<string, int>> ChallengeAnswerKeyAsync(string lessonCode)
    {
        await using var scope = api.NewScope();
        var db = ApiFactory.Db(scope);

        var rows = await db.LessonItems
            .Where(i => i.Activity!.Lesson!.Code == lessonCode
                        && i.Activity.Kind != Domain.Enums.ActivityKind.Shadow
                        && i.Activity.Kind != Domain.Enums.ActivityKind.Speak
                        && i.Activity.Kind != Domain.Enums.ActivityKind.Write)
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
