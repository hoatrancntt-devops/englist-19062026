using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnglishForIT.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVocabDecks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "vocab_decks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    title_vi = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    context_vi = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    band = table.Column<int>(type: "integer", nullable: false),
                    source_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vocab_decks", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "vocab_words",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    deck_id = table.Column<Guid>(type: "uuid", nullable: false),
                    term = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ipa = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    meaning_vi = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    chunk = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    emoji = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    mnemonic_vi = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    order_index = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vocab_words", x => x.id);
                    table.ForeignKey(
                        name: "fk_vocab_words_vocab_decks_deck_id",
                        column: x => x.deck_id,
                        principalTable: "vocab_decks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vocab_word_progresses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    word_id = table.Column<Guid>(type: "uuid", nullable: false),
                    best_score = table.Column<double>(type: "double precision", nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    first_learned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    interval_days = table.Column<int>(type: "integer", nullable: false),
                    ease = table.Column<double>(type: "double precision", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vocab_word_progresses", x => x.id);
                    table.ForeignKey(
                        name: "fk_vocab_word_progresses_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_vocab_word_progresses_vocab_words_word_id",
                        column: x => x.word_id,
                        principalTable: "vocab_words",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_vocab_decks_band",
                table: "vocab_decks",
                column: "band");

            migrationBuilder.CreateIndex(
                name: "ix_vocab_decks_code",
                table: "vocab_decks",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vocab_word_progresses_user_id_due_at",
                table: "vocab_word_progresses",
                columns: new[] { "user_id", "due_at" });

            migrationBuilder.CreateIndex(
                name: "ix_vocab_word_progresses_user_id_word_id",
                table: "vocab_word_progresses",
                columns: new[] { "user_id", "word_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vocab_word_progresses_word_id",
                table: "vocab_word_progresses",
                column: "word_id");

            migrationBuilder.CreateIndex(
                name: "ix_vocab_words_deck_id_term",
                table: "vocab_words",
                columns: new[] { "deck_id", "term" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vocab_word_progresses");

            migrationBuilder.DropTable(
                name: "vocab_words");

            migrationBuilder.DropTable(
                name: "vocab_decks");
        }
    }
}
