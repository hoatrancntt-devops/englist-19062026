namespace EnglishForIT.Domain.Common;

/// <summary>Base cho mọi bảng: khoá GUID v7 (sortable theo thời gian) + dấu thời gian.</summary>
public abstract class Entity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    /// <summary>
    /// Bỏ TRỐNG có chủ đích. AppDbContext đóng dấu lúc lưu nếu service chưa tự đặt.
    ///
    /// Gán sẵn DateTimeOffset.UtcNow ở đây thì không phân biệt được "chưa đặt" với "đã đặt",
    /// nên tầng lưu buộc phải ghi đè vô điều kiện — và giờ logic mà service truyền vào bị mất.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
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
