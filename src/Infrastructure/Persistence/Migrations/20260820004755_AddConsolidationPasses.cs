using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnglishForIT.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConsolidationPasses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "consolidation_passes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_index = table.Column<int>(type: "integer", nullable: false),
                    score = table.Column<double>(type: "double precision", nullable: false),
                    passed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    lesson_codes_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_consolidation_passes", x => x.id);
                    table.ForeignKey(
                        name: "fk_consolidation_passes_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_consolidation_passes_user_id_group_index",
                table: "consolidation_passes",
                columns: new[] { "user_id", "group_index" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "consolidation_passes");
        }
    }
}
