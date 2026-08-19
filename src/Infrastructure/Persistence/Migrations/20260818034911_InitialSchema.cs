using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnglishForIT.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "activity_attempts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lesson_attempt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    activity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    skill = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    score = table.Column<double>(type: "double precision", nullable: false),
                    passed = table.Column<bool>(type: "boolean", nullable: false),
                    result_json = table.Column<string>(type: "jsonb", nullable: false),
                    duration_seconds = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_activity_attempts", x => x.id);
                    table.CheckConstraint("ck_activity_attempts_score", "score BETWEEN 0 AND 100");
                });

            migrationBuilder.CreateTable(
                name: "ai_cache_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cache_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    task_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    prompt_version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    response_json = table.Column<string>(type: "jsonb", nullable: false),
                    provider = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    model = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    hit_count = table.Column<int>(type: "integer", nullable: false),
                    last_hit_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_cache_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ai_provider_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    api_key_encrypted = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    base_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    extra_json = table.Column<string>(type: "jsonb", nullable: true),
                    last_verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_verify_succeeded = table.Column<bool>(type: "boolean", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_provider_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ai_usages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    task_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tier = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    provider = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    model = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    input_tokens = table.Column<int>(type: "integer", nullable: false),
                    output_tokens = table.Column<int>(type: "integer", nullable: false),
                    estimated_cost_usd = table.Column<decimal>(type: "numeric(12,6)", precision: 12, scale: 6, nullable: false),
                    cache_hit = table.Column<bool>(type: "boolean", nullable: false),
                    succeeded = table.Column<bool>(type: "boolean", nullable: false),
                    error_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    latency_ms = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_usages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    target_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    target_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    metadata_json = table.Column<string>(type: "jsonb", nullable: true),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "challenge_passes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lesson_id = table.Column<Guid>(type: "uuid", nullable: false),
                    score = table.Column<double>(type: "double precision", nullable: false),
                    passed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    item_codes_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_challenge_passes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "content_versions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    entity_code = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    source_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    snapshot_json = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    published_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    change_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_content_versions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "lesson_attempts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lesson_id = table.Column<Guid>(type: "uuid", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    current_activity_index = table.Column<int>(type: "integer", nullable: false),
                    draft_state_json = table.Column<string>(type: "jsonb", nullable: true),
                    score = table.Column<double>(type: "double precision", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lesson_attempts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "lesson_state_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lesson_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_state = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    to_state = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    reason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    detail_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lesson_state_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "lessons",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    slug = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    title_vi = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    title_en = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    track = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    level = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    layer = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    supported_skills = table.Column<string>(type: "jsonb", nullable: false),
                    unit_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    order_index = table.Column<int>(type: "integer", nullable: false),
                    estimated_minutes = table.Column<int>(type: "integer", nullable: false),
                    is_checkpoint = table.Column<bool>(type: "boolean", nullable: false),
                    objective_vi = table.Column<string>(type: "text", nullable: false),
                    objective_observable = table.Column<string>(type: "text", nullable: false),
                    mastery_weights = table.Column<string>(type: "jsonb", nullable: false),
                    explanation_json = table.Column<string>(type: "jsonb", nullable: false),
                    common_mistakes_json = table.Column<string>(type: "jsonb", nullable: false),
                    body_json = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    source_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lessons", x => x.id);
                    table.CheckConstraint("ck_lessons_est_minutes", "estimated_minutes BETWEEN 3 AND 12");
                });

            migrationBuilder.CreateTable(
                name: "mail_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    from_address = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    from_display_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    client_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    client_secret_encrypted = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    smtp_host = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    smtp_port = table.Column<int>(type: "integer", nullable: true),
                    smtp_use_start_tls = table.Column<bool>(type: "boolean", nullable: false),
                    smtp_username = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    smtp_password_encrypted = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    last_test_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_test_succeeded = table.Column<bool>(type: "boolean", nullable: true),
                    last_test_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mail_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "media_assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    relative_path = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    mime_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    duration_ms = table.Column<int>(type: "integer", nullable: false),
                    source_text = table.Column<string>(type: "text", nullable: false),
                    voice = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    speed = table.Column<double>(type: "double precision", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_media_assets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notification_preferences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    daily_reminder_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    streak_alerts_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    review_due_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    weekly_report_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    email_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    quiet_hours_start = table.Column<int>(type: "integer", nullable: false),
                    quiet_hours_end = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_preferences", x => x.id);
                    table.CheckConstraint("ck_notification_preferences_quiet_end", "quiet_hours_end BETWEEN 0 AND 23");
                    table.CheckConstraint("ck_notification_preferences_quiet_start", "quiet_hours_start BETWEEN 0 AND 23");
                });

            migrationBuilder.CreateTable(
                name: "outbox_emails",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_address = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    to_display_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    subject = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    html_body = table.Column<string>(type: "text", nullable: false),
                    text_body = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    idempotency_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_emails", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "placement_forms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    title_vi = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    estimated_minutes = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    source_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_placement_forms", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "placement_speaking_scores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    attempt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pronunciation_score = table.Column<double>(type: "double precision", nullable: false),
                    fluency_score = table.Column<double>(type: "double precision", nullable: false),
                    communication_score = table.Column<double>(type: "double precision", nullable: false),
                    transcript_en = table.Column<string>(type: "text", nullable: true),
                    phoneme_issues_json = table.Column<string>(type: "jsonb", nullable: true),
                    scored = table.Column<bool>(type: "boolean", nullable: false),
                    scored_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_placement_speaking_scores", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "roleplay_scenarios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    title_vi = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    context_vi = table.Column<string>(type: "text", nullable: false),
                    track = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    level = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    partner_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    start_node_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_roleplay_scenarios", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "speech_attempts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    context_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    context_id = table.Column<Guid>(type: "uuid", nullable: true),
                    expected_text = table.Column<string>(type: "text", nullable: false),
                    transcript_en = table.Column<string>(type: "text", nullable: true),
                    pronunciation_score = table.Column<double>(type: "double precision", nullable: false),
                    fluency_score = table.Column<double>(type: "double precision", nullable: false),
                    communication_score = table.Column<double>(type: "double precision", nullable: false),
                    feedback_vi_json = table.Column<string>(type: "jsonb", nullable: true),
                    audio_relative_path = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    duration_ms = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_speech_attempts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "story_chapters",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    title_vi = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    hook_vi = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    body_vi = table.Column<string>(type: "text", nullable: false),
                    ends_vi = table.Column<string>(type: "text", nullable: false),
                    track = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    new_characters_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_story_chapters", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "streaks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    current_streak = table.Column<int>(type: "integer", nullable: false),
                    longest_streak = table.Column<int>(type: "integer", nullable: false),
                    last_study_date_local = table.Column<DateOnly>(type: "date", nullable: true),
                    freeze_tokens = table.Column<int>(type: "integer", nullable: false),
                    last_freeze_granted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_streaks", x => x.id);
                    table.CheckConstraint("ck_streaks_freeze_tokens", "freeze_tokens BETWEEN 0 AND 2");
                });

            migrationBuilder.CreateTable(
                name: "system_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    value_json = table.Column<string>(type: "jsonb", nullable: false),
                    is_secret = table.Column<bool>(type: "boolean", nullable: false),
                    description_vi = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_system_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    display_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    email_verified = table.Column<bool>(type: "boolean", nullable: false),
                    email_verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    locked_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failed_login_count = table.Column<int>(type: "integer", nullable: false),
                    last_login_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    security_stamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "writing_sets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    title_vi = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    context_vi = table.Column<string>(type: "text", nullable: false),
                    track = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    level = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    source_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_writing_sets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "lesson_activities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lesson_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    skill = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    order_index = table.Column<int>(type: "integer", nullable: false),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    pass_score = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lesson_activities", x => x.id);
                    table.CheckConstraint("ck_lesson_activities_pass_score", "pass_score BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "fk_lesson_activities_lessons_lesson_id",
                        column: x => x.lesson_id,
                        principalTable: "lessons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lesson_prerequisites",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lesson_id = table.Column<Guid>(type: "uuid", nullable: false),
                    required_lesson_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    min_mastery = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lesson_prerequisites", x => x.id);
                    table.CheckConstraint("ck_lesson_prerequisites_min_mastery", "min_mastery BETWEEN 0 AND 100");
                    table.CheckConstraint("ck_lesson_prerequisites_no_self", "lesson_id <> required_lesson_id");
                    table.ForeignKey(
                        name: "fk_lesson_prerequisites_lessons_lesson_id",
                        column: x => x.lesson_id,
                        principalTable: "lessons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_lesson_prerequisites_lessons_required_lesson_id",
                        column: x => x.required_lesson_id,
                        principalTable: "lessons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "placement_form_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    form_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    order_index = table.Column<int>(type: "integer", nullable: false),
                    kind = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    skill = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    prompt_json = table.Column<string>(type: "jsonb", nullable: false),
                    answer_json = table.Column<string>(type: "jsonb", nullable: false),
                    weight = table.Column<double>(type: "double precision", nullable: false),
                    difficulty = table.Column<int>(type: "integer", nullable: false),
                    slow_answer_seconds = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_placement_form_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_placement_form_items_placement_forms_form_id",
                        column: x => x.form_id,
                        principalTable: "placement_forms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "roleplay_attempts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scenario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    outcome = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    path_json = table.Column<string>(type: "jsonb", nullable: false),
                    hints_used = table.Column<int>(type: "integer", nullable: false),
                    score = table.Column<double>(type: "double precision", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_roleplay_attempts", x => x.id);
                    table.ForeignKey(
                        name: "fk_roleplay_attempts_roleplay_scenarios_scenario_id",
                        column: x => x.scenario_id,
                        principalTable: "roleplay_scenarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "roleplay_nodes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    scenario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    partner_line_en = table.Column<string>(type: "text", nullable: false),
                    partner_line_vi = table.Column<string>(type: "text", nullable: false),
                    audio_asset_id = table.Column<Guid>(type: "uuid", nullable: true),
                    choices_json = table.Column<string>(type: "jsonb", nullable: false),
                    is_terminal = table.Column<bool>(type: "boolean", nullable: false),
                    summary_vi = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_roleplay_nodes", x => x.id);
                    table.ForeignKey(
                        name: "fk_roleplay_nodes_roleplay_scenarios_scenario_id",
                        column: x => x.scenario_id,
                        principalTable: "roleplay_scenarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "story_progresses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chapter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unlocked = table.Column<bool>(type: "boolean", nullable: false),
                    unlocked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_story_progresses", x => x.id);
                    table.ForeignKey(
                        name: "fk_story_progresses_story_chapters_chapter_id",
                        column: x => x.chapter_id,
                        principalTable: "story_chapters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "email_verification_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_email_verification_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_email_verification_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "enrollments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    entry_level = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    entry_lesson_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_enrollments", x => x.id);
                    table.ForeignKey(
                        name: "fk_enrollments_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lesson_masteries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lesson_id = table.Column<Guid>(type: "uuid", nullable: false),
                    state = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    mastery_raw = table.Column<double>(type: "double precision", nullable: false),
                    mastery_effective = table.Column<double>(type: "double precision", nullable: false),
                    skill_scores = table.Column<string>(type: "jsonb", nullable: false),
                    attempts_count = table.Column<int>(type: "integer", nullable: false),
                    first_started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_activity_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    mastered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    time_spent_seconds = table.Column<int>(type: "integer", nullable: false),
                    unlocked_by_challenge = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lesson_masteries", x => x.id);
                    table.CheckConstraint("ck_lesson_mastery_effective_range", "mastery_effective BETWEEN 0 AND 100");
                    table.CheckConstraint("ck_lesson_mastery_raw_range", "mastery_raw BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "fk_lesson_masteries_lessons_lesson_id",
                        column: x => x.lesson_id,
                        principalTable: "lessons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_lesson_masteries_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    title_vi = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    body_vi = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    action_url = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    dedupe_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications", x => x.id);
                    table.ForeignKey(
                        name: "fk_notifications_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "onboarding_answers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    answer_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_onboarding_answers", x => x.id);
                    table.ForeignKey(
                        name: "fk_onboarding_answers_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "password_reset_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_password_reset_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_password_reset_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "placement_attempts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    form_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deadline_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    result_level = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    skill_scores = table.Column<string>(type: "jsonb", nullable: false),
                    vocab_grammar_score = table.Column<double>(type: "double precision", nullable: false),
                    fast_answer_ratio = table.Column<double>(type: "double precision", nullable: false),
                    self_rated_score = table.Column<double>(type: "double precision", nullable: false),
                    explanation_json = table.Column<string>(type: "jsonb", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_placement_attempts", x => x.id);
                    table.ForeignKey(
                        name: "fk_placement_attempts_placement_forms_form_id",
                        column: x => x.form_id,
                        principalTable: "placement_forms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_placement_attempts_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    csrf_secret = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    rotated_to_session_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_agent = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sessions", x => x.id);
                    table.ForeignKey(
                        name: "fk_sessions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    goals = table.Column<string>(type: "jsonb", nullable: false),
                    primary_track = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    study_mode = table.Column<int>(type: "integer", nullable: false),
                    current_layer = table.Column<int>(type: "integer", nullable: false),
                    daily_minutes_target = table.Column<int>(type: "integer", nullable: false),
                    microphone_checked = table.Column<bool>(type: "boolean", nullable: false),
                    onboarding_completed = table.Column<bool>(type: "boolean", nullable: false),
                    onboarding_completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    current_level = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    time_zone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    reminder_hour_local = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_profiles", x => x.id);
                    table.CheckConstraint("ck_user_profiles_daily_minutes", "daily_minutes_target BETWEEN 5 AND 240");
                    table.CheckConstraint("ck_user_profiles_reminder_hour", "reminder_hour_local BETWEEN 0 AND 23");
                    table.ForeignKey(
                        name: "fk_user_profiles_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    granted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_roles", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_roles_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "writing_tasks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    order_index = table.Column<int>(type: "integer", nullable: false),
                    kind = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    prompt_json = table.Column<string>(type: "jsonb", nullable: false),
                    rubric_json = table.Column<string>(type: "jsonb", nullable: false),
                    pass_score = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_writing_tasks", x => x.id);
                    table.ForeignKey(
                        name: "fk_writing_tasks_writing_sets_set_id",
                        column: x => x.set_id,
                        principalTable: "writing_sets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lesson_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    activity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    order_index = table.Column<int>(type: "integer", nullable: false),
                    prompt_json = table.Column<string>(type: "jsonb", nullable: false),
                    answer_json = table.Column<string>(type: "jsonb", nullable: false),
                    difficulty = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lesson_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_lesson_items_lesson_activities_activity_id",
                        column: x => x.activity_id,
                        principalTable: "lesson_activities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "placement_answers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    attempt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    response_json = table.Column<string>(type: "jsonb", nullable: false),
                    score = table.Column<double>(type: "double precision", nullable: false),
                    is_correct = table.Column<bool>(type: "boolean", nullable: false),
                    response_seconds = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_placement_answers", x => x.id);
                    table.ForeignKey(
                        name: "fk_placement_answers_placement_attempts_attempt_id",
                        column: x => x.attempt_id,
                        principalTable: "placement_attempts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_placement_answers_placement_form_items_item_id",
                        column: x => x.item_id,
                        principalTable: "placement_form_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "review_queue",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    interval_days = table.Column<int>(type: "integer", nullable: false),
                    ease = table.Column<double>(type: "double precision", nullable: false),
                    repetition_count = table.Column<int>(type: "integer", nullable: false),
                    lapse_count = table.Column<int>(type: "integer", nullable: false),
                    last_reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_review_queue", x => x.id);
                    table.CheckConstraint("ck_review_queue_ease", "ease BETWEEN 1.3 AND 3.0");
                    table.CheckConstraint("ck_review_queue_interval", "interval_days BETWEEN 1 AND 60");
                    table.ForeignKey(
                        name: "fk_review_queue_lesson_items_item_id",
                        column: x => x.item_id,
                        principalTable: "lesson_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_activity_attempts_lesson_attempt_id",
                table: "activity_attempts",
                column: "lesson_attempt_id");

            migrationBuilder.CreateIndex(
                name: "ix_activity_attempts_user_id_skill_created_at",
                table: "activity_attempts",
                columns: new[] { "user_id", "skill", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_ai_cache_entries_cache_key",
                table: "ai_cache_entries",
                column: "cache_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ai_cache_entries_expires_at",
                table: "ai_cache_entries",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_ai_provider_settings_provider",
                table: "ai_provider_settings",
                column: "provider",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ai_usages_created_at",
                table: "ai_usages",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_ai_usages_user_id_created_at",
                table: "ai_usages",
                columns: new[] { "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_action_created_at",
                table: "audit_logs",
                columns: new[] { "action", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_actor_user_id_created_at",
                table: "audit_logs",
                columns: new[] { "actor_user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_challenge_passes_user_id_lesson_id",
                table: "challenge_passes",
                columns: new[] { "user_id", "lesson_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_content_versions_entity_type_entity_code_version_number",
                table: "content_versions",
                columns: new[] { "entity_type", "entity_code", "version_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_email_verification_tokens_expires_at",
                table: "email_verification_tokens",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_email_verification_tokens_token_hash",
                table: "email_verification_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_email_verification_tokens_user_id",
                table: "email_verification_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_enrollments_user_id",
                table: "enrollments",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lesson_activities_lesson_id_order_index",
                table: "lesson_activities",
                columns: new[] { "lesson_id", "order_index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lesson_attempts_user_id_lesson_id_started_at",
                table: "lesson_attempts",
                columns: new[] { "user_id", "lesson_id", "started_at" });

            migrationBuilder.CreateIndex(
                name: "ix_lesson_attempts_user_id_submitted_at",
                table: "lesson_attempts",
                columns: new[] { "user_id", "submitted_at" },
                filter: "submitted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_lesson_items_activity_id_order_index",
                table: "lesson_items",
                columns: new[] { "activity_id", "order_index" });

            migrationBuilder.CreateIndex(
                name: "ix_lesson_items_code",
                table: "lesson_items",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lesson_masteries_lesson_id",
                table: "lesson_masteries",
                column: "lesson_id");

            migrationBuilder.CreateIndex(
                name: "ix_lesson_masteries_user_id_lesson_id",
                table: "lesson_masteries",
                columns: new[] { "user_id", "lesson_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lesson_masteries_user_id_state",
                table: "lesson_masteries",
                columns: new[] { "user_id", "state" });

            migrationBuilder.CreateIndex(
                name: "ix_lesson_prerequisites_lesson_id_required_lesson_id",
                table: "lesson_prerequisites",
                columns: new[] { "lesson_id", "required_lesson_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lesson_prerequisites_required_lesson_id",
                table: "lesson_prerequisites",
                column: "required_lesson_id");

            migrationBuilder.CreateIndex(
                name: "ix_lesson_state_events_user_id_lesson_id_created_at",
                table: "lesson_state_events",
                columns: new[] { "user_id", "lesson_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_lessons_code",
                table: "lessons",
                column: "code",
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_lessons_layer_level_track_order_index",
                table: "lessons",
                columns: new[] { "layer", "level", "track", "order_index" });

            migrationBuilder.CreateIndex(
                name: "ix_lessons_slug",
                table: "lessons",
                column: "slug",
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_media_assets_content_hash",
                table: "media_assets",
                column: "content_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notification_preferences_user_id",
                table: "notification_preferences",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notifications_dedupe_key",
                table: "notifications",
                column: "dedupe_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notifications_user_id_read_at",
                table: "notifications",
                columns: new[] { "user_id", "read_at" },
                filter: "read_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_onboarding_answers_user_id_question_key",
                table: "onboarding_answers",
                columns: new[] { "user_id", "question_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_emails_idempotency_key",
                table: "outbox_emails",
                column: "idempotency_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_emails_status_next_attempt_at",
                table: "outbox_emails",
                columns: new[] { "status", "next_attempt_at" },
                filter: "status = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "ix_password_reset_tokens_expires_at",
                table: "password_reset_tokens",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_password_reset_tokens_token_hash",
                table: "password_reset_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_password_reset_tokens_user_id",
                table: "password_reset_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_placement_answers_attempt_id_item_id",
                table: "placement_answers",
                columns: new[] { "attempt_id", "item_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_placement_answers_item_id",
                table: "placement_answers",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "ix_placement_attempts_form_id",
                table: "placement_attempts",
                column: "form_id");

            migrationBuilder.CreateIndex(
                name: "ix_placement_attempts_user_id",
                table: "placement_attempts",
                column: "user_id",
                unique: true,
                filter: "status = 'InProgress'");

            migrationBuilder.CreateIndex(
                name: "ix_placement_attempts_user_id_form_id_submitted_at",
                table: "placement_attempts",
                columns: new[] { "user_id", "form_id", "submitted_at" });

            migrationBuilder.CreateIndex(
                name: "ix_placement_form_items_form_id_code",
                table: "placement_form_items",
                columns: new[] { "form_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_placement_form_items_form_id_order_index",
                table: "placement_form_items",
                columns: new[] { "form_id", "order_index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_placement_forms_code",
                table: "placement_forms",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_placement_speaking_scores_attempt_id_item_id",
                table: "placement_speaking_scores",
                columns: new[] { "attempt_id", "item_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_placement_speaking_scores_scored",
                table: "placement_speaking_scores",
                column: "scored",
                filter: "scored = false");

            migrationBuilder.CreateIndex(
                name: "ix_review_queue_item_id",
                table: "review_queue",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "ix_review_queue_user_id_due_at",
                table: "review_queue",
                columns: new[] { "user_id", "due_at" });

            migrationBuilder.CreateIndex(
                name: "ix_review_queue_user_id_item_id",
                table: "review_queue",
                columns: new[] { "user_id", "item_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_roleplay_attempts_scenario_id",
                table: "roleplay_attempts",
                column: "scenario_id");

            migrationBuilder.CreateIndex(
                name: "ix_roleplay_attempts_user_id_scenario_id_started_at",
                table: "roleplay_attempts",
                columns: new[] { "user_id", "scenario_id", "started_at" });

            migrationBuilder.CreateIndex(
                name: "ix_roleplay_nodes_scenario_id_code",
                table: "roleplay_nodes",
                columns: new[] { "scenario_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_roleplay_scenarios_code",
                table: "roleplay_scenarios",
                column: "code",
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_sessions_expires_at",
                table: "sessions",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_sessions_token_hash",
                table: "sessions",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sessions_user_id_expires_at",
                table: "sessions",
                columns: new[] { "user_id", "expires_at" },
                filter: "revoked_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_speech_attempts_created_at",
                table: "speech_attempts",
                column: "created_at",
                filter: "audio_relative_path IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_speech_attempts_user_id_created_at",
                table: "speech_attempts",
                columns: new[] { "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_story_chapters_code",
                table: "story_chapters",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_story_chapters_number",
                table: "story_chapters",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_story_progresses_chapter_id",
                table: "story_progresses",
                column: "chapter_id");

            migrationBuilder.CreateIndex(
                name: "ix_story_progresses_user_id_chapter_id",
                table: "story_progresses",
                columns: new[] { "user_id", "chapter_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_streaks_user_id",
                table: "streaks",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_system_settings_key",
                table: "system_settings",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_profiles_user_id",
                table: "user_profiles",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_roles_user_id_role",
                table: "user_roles",
                columns: new[] { "user_id", "role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                table: "users",
                column: "email",
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_writing_sets_code",
                table: "writing_sets",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_writing_tasks_set_id_code",
                table: "writing_tasks",
                columns: new[] { "set_id", "code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "activity_attempts");

            migrationBuilder.DropTable(
                name: "ai_cache_entries");

            migrationBuilder.DropTable(
                name: "ai_provider_settings");

            migrationBuilder.DropTable(
                name: "ai_usages");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "challenge_passes");

            migrationBuilder.DropTable(
                name: "content_versions");

            migrationBuilder.DropTable(
                name: "email_verification_tokens");

            migrationBuilder.DropTable(
                name: "enrollments");

            migrationBuilder.DropTable(
                name: "lesson_attempts");

            migrationBuilder.DropTable(
                name: "lesson_masteries");

            migrationBuilder.DropTable(
                name: "lesson_prerequisites");

            migrationBuilder.DropTable(
                name: "lesson_state_events");

            migrationBuilder.DropTable(
                name: "mail_settings");

            migrationBuilder.DropTable(
                name: "media_assets");

            migrationBuilder.DropTable(
                name: "notification_preferences");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "onboarding_answers");

            migrationBuilder.DropTable(
                name: "outbox_emails");

            migrationBuilder.DropTable(
                name: "password_reset_tokens");

            migrationBuilder.DropTable(
                name: "placement_answers");

            migrationBuilder.DropTable(
                name: "placement_speaking_scores");

            migrationBuilder.DropTable(
                name: "review_queue");

            migrationBuilder.DropTable(
                name: "roleplay_attempts");

            migrationBuilder.DropTable(
                name: "roleplay_nodes");

            migrationBuilder.DropTable(
                name: "sessions");

            migrationBuilder.DropTable(
                name: "speech_attempts");

            migrationBuilder.DropTable(
                name: "story_progresses");

            migrationBuilder.DropTable(
                name: "streaks");

            migrationBuilder.DropTable(
                name: "system_settings");

            migrationBuilder.DropTable(
                name: "user_profiles");

            migrationBuilder.DropTable(
                name: "user_roles");

            migrationBuilder.DropTable(
                name: "writing_tasks");

            migrationBuilder.DropTable(
                name: "placement_attempts");

            migrationBuilder.DropTable(
                name: "placement_form_items");

            migrationBuilder.DropTable(
                name: "lesson_items");

            migrationBuilder.DropTable(
                name: "roleplay_scenarios");

            migrationBuilder.DropTable(
                name: "story_chapters");

            migrationBuilder.DropTable(
                name: "writing_sets");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "placement_forms");

            migrationBuilder.DropTable(
                name: "lesson_activities");

            migrationBuilder.DropTable(
                name: "lessons");
        }
    }
}
