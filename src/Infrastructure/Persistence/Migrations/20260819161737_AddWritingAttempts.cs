using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnglishForIT.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWritingAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "writing_attempts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    score = table.Column<double>(type: "double precision", nullable: false),
                    passed = table.Column<bool>(type: "boolean", nullable: false),
                    submission_json = table.Column<string>(type: "jsonb", nullable: false),
                    feedback_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_writing_attempts", x => x.id);
                    table.ForeignKey(
                        name: "fk_writing_attempts_writing_tasks_task_id",
                        column: x => x.task_id,
                        principalTable: "writing_tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_writing_attempts_task_id",
                table: "writing_attempts",
                column: "task_id");

            migrationBuilder.CreateIndex(
                name: "ix_writing_attempts_user_id_task_id_submitted_at",
                table: "writing_attempts",
                columns: new[] { "user_id", "task_id", "submitted_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "writing_attempts");
        }
    }
}
