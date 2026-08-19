using System.Text.Json;
using System.Text.Json.Serialization;
using EnglishForIT.Domain.Enums;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EnglishForIT.Infrastructure.Persistence;

/// <summary>
/// Chuyển đổi kiểu .NET sang cột jsonb bằng converter tường minh.
///
/// Vì sao không dùng POCO mapping tự động của Npgsql: nó cần bật dynamic JSON trên data source,
/// và khi bật thì mọi kiểu đều có thể lọt vào jsonb mà không ai review. Converter tường minh
/// buộc mỗi cột jsonb phải được khai báo có chủ đích.
/// </summary>
public static class JsonbConverters
{
    private static readonly JsonSerializerOptions Options = new()
    {
        // Enum ghi thành chuỗi để đọc dữ liệu thô trong psql không phải tra bảng số.
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static readonly ValueConverter<Dictionary<SkillType, double>, string> SkillScoreMap =
        new(
            v => JsonSerializer.Serialize(v, Options),
            v => JsonSerializer.Deserialize<Dictionary<SkillType, double>>(v, Options)
                 ?? new Dictionary<SkillType, double>());

    public static readonly ValueComparer<Dictionary<SkillType, double>> SkillScoreMapComparer =
        new(
            (a, b) => a != null && b != null && a.Count == b.Count && !a.Except(b).Any(),
            v => v.Aggregate(0, (acc, kv) => HashCode.Combine(acc, kv.Key, kv.Value)),
            v => new Dictionary<SkillType, double>(v));

    public static readonly ValueConverter<List<SkillType>, string> SkillList =
        new(
            v => JsonSerializer.Serialize(v, Options),
            v => JsonSerializer.Deserialize<List<SkillType>>(v, Options) ?? new List<SkillType>());

    public static readonly ValueComparer<List<SkillType>> SkillListComparer =
        new(
            (a, b) => a != null && b != null && a.SequenceEqual(b),
            v => v.Aggregate(0, (acc, item) => HashCode.Combine(acc, item)),
            v => v.ToList());

    public static readonly ValueConverter<List<LearningGoal>, string> GoalList =
        new(
            v => JsonSerializer.Serialize(v, Options),
            v => JsonSerializer.Deserialize<List<LearningGoal>>(v, Options) ?? new List<LearningGoal>());

    public static readonly ValueComparer<List<LearningGoal>> GoalListComparer =
        new(
            (a, b) => a != null && b != null && a.SequenceEqual(b),
            v => v.Aggregate(0, (acc, item) => HashCode.Combine(acc, item)),
            v => v.ToList());
}
