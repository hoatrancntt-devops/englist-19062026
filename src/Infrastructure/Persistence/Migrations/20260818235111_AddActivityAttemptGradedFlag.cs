using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnglishForIT.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityAttemptGradedFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Mặc định TRUE, không phải false: trước khi có cột này, bản ghi chỉ được tạo
            // cho bước đã chấm được. Để false thì toàn bộ điểm quá khứ biến mất khỏi các
            // truy vấn tính mastery mà không có lỗi nào báo.
            migrationBuilder.AddColumn<bool>(
                name: "graded",
                table: "activity_attempts",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "graded",
                table: "activity_attempts");
        }
    }
}
