using EnglishForIT.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnglishForIT.Infrastructure.Persistence;

public static class ConcurrencyTokenExtensions
{
    /// <summary>
    /// Ánh xạ <see cref="IConcurrencyStamped.RowVersion"/> vào cột hệ thống <c>xmin</c> của Postgres.
    ///
    /// Vì sao không dùng <c>IsRowVersion()</c>: trên Npgsql nó tạo ra một cột <c>row_version</c>
    /// thật, NOT NULL, mà không ai gán giá trị — mọi lệnh INSERT sẽ hỏng với lỗi 23502.
    /// Postgres đã có sẵn <c>xmin</c> ghi mã giao dịch sửa dòng gần nhất, nên dùng thẳng nó:
    /// không tốn cột, không tốn trigger, và luôn chính xác.
    /// </summary>
    public static EntityTypeBuilder<T> UseXminAsConcurrencyToken<T>(this EntityTypeBuilder<T> builder)
        where T : class, IConcurrencyStamped
    {
        builder.Property(x => x.RowVersion)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        return builder;
    }
}
