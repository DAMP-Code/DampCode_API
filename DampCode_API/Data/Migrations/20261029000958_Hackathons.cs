using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DampCode_API.Data.Migrations
{
    /// <inheritdoc />
    public partial class Hackathons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "hackathons",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    slug = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    area = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    participation_mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    evaluation_method = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    prize_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    reward_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    winners_count = table.Column<int>(type: "integer", nullable: true),
                    starts_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ends_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    review_ends_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    scheduled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    closed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancelled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    primary_color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    secondary_color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    background_color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    logo = table.Column<string>(type: "text", nullable: true),
                    logo_storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_hackathons", x => x.id);
                    table.CheckConstraint("ck_hackathons_dates", "starts_at IS NULL OR ends_at IS NULL OR review_ends_at IS NULL OR (starts_at < ends_at AND ends_at < review_ends_at)");
                    table.CheckConstraint("ck_hackathons_participation_mode", "participation_mode IN ('Individual', 'Team')");
                    table.CheckConstraint("ck_hackathons_reward_type", "reward_type IS NULL OR reward_type IN ('Money', 'Interview')");
                    table.CheckConstraint("ck_hackathons_status", "status IN ('Draft', 'Scheduled', 'Active', 'InReview', 'Closed', 'Cancelled')");
                    table.CheckConstraint("ck_hackathons_winners_count", "winners_count IS NULL OR winners_count IN (1, 10, 15, 20, 50, 100)");
                    table.ForeignKey(
                        name: "fk_hackathons_accounts",
                        column: x => x.created_by_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_hackathons_companies",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hackathon_technologies",
                columns: table => new
                {
                    hackathon_id = table.Column<Guid>(type: "uuid", nullable: false),
                    technology_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_hackathon_technologies", x => new { x.hackathon_id, x.technology_id });
                    table.ForeignKey(
                        name: "fk_hackathon_technologies_hackathons",
                        column: x => x.hackathon_id,
                        principalTable: "hackathons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_hackathon_technologies_technologies",
                        column: x => x.technology_id,
                        principalTable: "technologies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_hackathon_technologies_technology_id",
                table: "hackathon_technologies",
                column: "technology_id");

            migrationBuilder.CreateIndex(
                name: "ix_hackathons_company_id",
                table: "hackathons",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_hackathons_created_by_account_id",
                table: "hackathons",
                column: "created_by_account_id");

            migrationBuilder.CreateIndex(
                name: "ix_hackathons_status",
                table: "hackathons",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ux_hackathons_slug",
                table: "hackathons",
                column: "slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "hackathon_technologies");

            migrationBuilder.DropTable(
                name: "hackathons");
        }
    }
}
