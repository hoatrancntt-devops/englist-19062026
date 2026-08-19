using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnglishForIT.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Bỏ concurrency token khỏi bảng lessons.
    ///
    /// Migration này CỐ Ý rỗng. Bộ sinh migration đề xuất DROP COLUMN "xmin",
    /// nhưng xmin là cột hệ thống của PostgreSQL — nó chưa bao giờ được CREATE
    /// nên cũng không thể DROP, và lệnh đó sẽ làm migration vỡ khi chạy thật.
    ///
    /// Thay đổi ở đây thuần tuý nằm trong mô hình EF: bảng lessons không còn khai
    /// xmin làm concurrency token nữa. Lý do: bảng nội dung chỉ có một người ghi
    /// là seeder, chạy tuần tự lúc khởi động. Seeder lưu làm hai lượt, và token
    /// khiến lượt hai so với giá trị xmin đã cũ của lượt một rồi thất bại.
    ///
    /// Các bảng thật sự có nhiều người ghi đồng thời (lesson_mastery, review_queue,
    /// placement_attempts, streaks, outbox_emails, users, user_profiles) vẫn giữ token.
    /// </summary>
    public partial class RemoveLessonConcurrencyToken : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Không có thao tác schema nào. Xem phần mô tả ở trên.
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Không có gì để hoàn tác.
        }
    }
}
