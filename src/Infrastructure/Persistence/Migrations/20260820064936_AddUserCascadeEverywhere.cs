using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnglishForIT.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserCascadeEverywhere : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Dọn dòng mồ côi TRƯỚC khi gắn khoá ngoại.
            //
            // Mười bảng này từ trước tới nay không có khoá ngoại về users, nên mỗi lần xoá một
            // học viên là để lại rác trỏ về tài khoản không còn tồn tại. Postgres từ chối tạo
            // khoá ngoại khi dữ liệu đang vi phạm, nên không dọn thì migration gãy giữa chừng.
            //
            // Bảng nào cột user_id cho phép rỗng thì CẮT LIÊN KẾT chứ không xoá dòng: ai_usages
            // là số liệu chi phí, mất đi thì không dựng lại được.
            foreach (var table in new[]
            {
                "activity_attempts", "challenge_passes", "lesson_attempts", "lesson_state_events",
                "notification_preferences", "review_queue", "roleplay_attempts", "speech_attempts",
                "streaks",
            })
            {
                migrationBuilder.Sql(
                    $"DELETE FROM {table} t WHERE NOT EXISTS (SELECT 1 FROM users u WHERE u.id = t.user_id);");
            }

            migrationBuilder.Sql(
                "UPDATE ai_usages t SET user_id = NULL "
                + "WHERE t.user_id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM users u WHERE u.id = t.user_id);");

            migrationBuilder.AddForeignKey(
                name: "fk_activity_attempts_users_user_id",
                table: "activity_attempts",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_ai_usages_users_user_id",
                table: "ai_usages",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_challenge_passes_users_user_id",
                table: "challenge_passes",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_lesson_attempts_users_user_id",
                table: "lesson_attempts",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_lesson_state_events_users_user_id",
                table: "lesson_state_events",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_notification_preferences_users_user_id",
                table: "notification_preferences",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_review_queue_users_user_id",
                table: "review_queue",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_roleplay_attempts_users_user_id",
                table: "roleplay_attempts",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_speech_attempts_users_user_id",
                table: "speech_attempts",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_streaks_users_user_id",
                table: "streaks",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_activity_attempts_users_user_id",
                table: "activity_attempts");

            migrationBuilder.DropForeignKey(
                name: "fk_ai_usages_users_user_id",
                table: "ai_usages");

            migrationBuilder.DropForeignKey(
                name: "fk_challenge_passes_users_user_id",
                table: "challenge_passes");

            migrationBuilder.DropForeignKey(
                name: "fk_lesson_attempts_users_user_id",
                table: "lesson_attempts");

            migrationBuilder.DropForeignKey(
                name: "fk_lesson_state_events_users_user_id",
                table: "lesson_state_events");

            migrationBuilder.DropForeignKey(
                name: "fk_notification_preferences_users_user_id",
                table: "notification_preferences");

            migrationBuilder.DropForeignKey(
                name: "fk_review_queue_users_user_id",
                table: "review_queue");

            migrationBuilder.DropForeignKey(
                name: "fk_roleplay_attempts_users_user_id",
                table: "roleplay_attempts");

            migrationBuilder.DropForeignKey(
                name: "fk_speech_attempts_users_user_id",
                table: "speech_attempts");

            migrationBuilder.DropForeignKey(
                name: "fk_streaks_users_user_id",
                table: "streaks");
        }
    }
}
