using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DampCode_API.Data.Migrations
{
    /// <inheritdoc />
    public partial class Evaluation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_hackathons_winners_count",
                table: "hackathons");

            migrationBuilder.AddColumn<DateTime>(
                name: "evaluated_at",
                table: "submissions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "evaluated_by_account_id",
                table: "submissions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "feedback",
                table: "submissions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "final_position",
                table: "submissions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "prize_position",
                table: "submissions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "score",
                table: "submissions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "results_published_at",
                table: "hackathons",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "hackathon_prizes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    hackathon_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    reward_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    description = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_hackathon_prizes", x => x.id);
                    table.ForeignKey(
                        name: "fk_hackathon_prizes_hackathons",
                        column: x => x.hackathon_id,
                        principalTable: "hackathons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Cada Hackathon existente com prêmio definido ganha o 1º lugar.
            migrationBuilder.Sql(@"
                INSERT INTO hackathon_prizes (id, hackathon_id, position, reward_type, amount, description, created_at)
                SELECT gen_random_uuid(), id, 1, reward_type, CASE WHEN reward_type = 'Money' THEN prize_amount END, NULL, now()
                FROM hackathons
                WHERE reward_type = 'Interview' OR (reward_type = 'Money' AND prize_amount > 0);");

            migrationBuilder.CreateIndex(
                name: "ix_submissions_evaluated_by_account_id",
                table: "submissions",
                column: "evaluated_by_account_id");

            migrationBuilder.CreateIndex(
                name: "ux_submissions_prize_position",
                table: "submissions",
                columns: new[] { "hackathon_id", "prize_position" },
                unique: true,
                filter: "prize_position IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_hackathons_winners_count",
                table: "hackathons",
                sql: "winners_count IS NULL OR winners_count BETWEEN 1 AND 10 OR winners_count IN (15, 20, 50, 100)");

            migrationBuilder.CreateIndex(
                name: "ux_hackathon_prizes_position",
                table: "hackathon_prizes",
                columns: new[] { "hackathon_id", "position" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_submissions_evaluated_by_accounts",
                table: "submissions",
                column: "evaluated_by_account_id",
                principalTable: "accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_submissions_evaluated_by_accounts",
                table: "submissions");

            migrationBuilder.DropTable(
                name: "hackathon_prizes");

            migrationBuilder.DropIndex(
                name: "ix_submissions_evaluated_by_account_id",
                table: "submissions");

            migrationBuilder.DropIndex(
                name: "ux_submissions_prize_position",
                table: "submissions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_hackathons_winners_count",
                table: "hackathons");

            migrationBuilder.DropColumn(
                name: "evaluated_at",
                table: "submissions");

            migrationBuilder.DropColumn(
                name: "evaluated_by_account_id",
                table: "submissions");

            migrationBuilder.DropColumn(
                name: "feedback",
                table: "submissions");

            migrationBuilder.DropColumn(
                name: "final_position",
                table: "submissions");

            migrationBuilder.DropColumn(
                name: "prize_position",
                table: "submissions");

            migrationBuilder.DropColumn(
                name: "score",
                table: "submissions");

            migrationBuilder.DropColumn(
                name: "results_published_at",
                table: "hackathons");

            migrationBuilder.AddCheckConstraint(
                name: "ck_hackathons_winners_count",
                table: "hackathons",
                sql: "winners_count IS NULL OR winners_count IN (1, 10, 15, 20, 50, 100)");
        }
    }
}
