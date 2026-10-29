using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DampCode_API.Data.Migrations
{
    /// <inheritdoc />
    public partial class Submissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "reference_code",
                table: "hackathons",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reference_language",
                table: "hackathons",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "submissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    hackathon_id = table.Column<Guid>(type: "uuid", nullable: false),
                    participation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    team_id = table.Column<Guid>(type: "uuid", nullable: true),
                    stage_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    current_version = table.Column<int>(type: "integer", nullable: false),
                    current_mongo_version_id = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    first_submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    finalized_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_submissions", x => x.id);
                    table.CheckConstraint("ck_submissions_owner", "(participation_id IS NULL) <> (team_id IS NULL)");
                    table.ForeignKey(
                        name: "fk_submissions_hackathon_teams",
                        column: x => x.team_id,
                        principalTable: "hackathon_teams",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_submissions_hackathons",
                        column: x => x.hackathon_id,
                        principalTable: "hackathons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_submissions_participations",
                        column: x => x.participation_id,
                        principalTable: "participations",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "submission_versions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    language = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    mongo_document_id = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    submitted_by_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_submission_versions", x => x.id);
                    table.ForeignKey(
                        name: "fk_submission_versions_accounts",
                        column: x => x.submitted_by_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_submission_versions_submissions",
                        column: x => x.submission_id,
                        principalTable: "submissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_submission_versions_submitted_by_account_id",
                table: "submission_versions",
                column: "submitted_by_account_id");

            migrationBuilder.CreateIndex(
                name: "ux_submission_versions_version",
                table: "submission_versions",
                columns: new[] { "submission_id", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_submissions_hackathon_id",
                table: "submissions",
                column: "hackathon_id");

            migrationBuilder.CreateIndex(
                name: "ux_submissions_participation",
                table: "submissions",
                column: "participation_id",
                unique: true,
                filter: "participation_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_submissions_team",
                table: "submissions",
                column: "team_id",
                unique: true,
                filter: "team_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "submission_versions");

            migrationBuilder.DropTable(
                name: "submissions");

            migrationBuilder.DropColumn(
                name: "reference_code",
                table: "hackathons");

            migrationBuilder.DropColumn(
                name: "reference_language",
                table: "hackathons");
        }
    }
}
