namespace EnglishForIT.Domain.Common;

/// <summary>Base cho mọi bảng: khoá GUID v7 (sortable theo thời gian) + dấu thời gian.</summary>
public abstract class Entity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Bảng có xoá mềm. Query filter toàn cục sẽ tự loại bản ghi đã xoá.</summary>
public interface ISoftDelete
{
    DateTimeOffset? DeletedAt { get; set; }
}

/// <summary>
/// Bảng cần chống ghi đè đồng thời. Npgsql ánh xạ cột hệ thống xmin làm concurrency token,
/// nên không tốn thêm cột thật nào.
/// </summary>
public interface IConcurrencyStamped
{
    uint RowVersion { get; set; }
}
