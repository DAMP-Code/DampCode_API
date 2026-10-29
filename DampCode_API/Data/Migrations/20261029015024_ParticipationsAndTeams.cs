using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DampCode_API.Data.Migrations
{
    /// <inheritdoc />
    public partial class ParticipationsAndTeams : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "hackathon_teams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    hackathon_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_open = table.Column<bool>(type: "boolean", nullable: false),
                    member_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_hackathon_teams", x => x.id);
                    table.CheckConstraint("ck_hackathon_teams_member_count", "member_count BETWEEN 0 AND 5");
                    table.ForeignKey(
                        name: "fk_hackathon_teams_accounts",
                        column: x => x.created_by_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_hackathon_teams_hackathons",
                        column: x => x.hackathon_id,
                        principalTable: "hackathons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "participations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    hackathon_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: true),
                    joined_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    left_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_participations", x => x.id);
                    table.ForeignKey(
                        name: "fk_participations_accounts",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_participations_hackathon_teams",
                        column: x => x.team_id,
                        principalTable: "hackathon_teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_participations_hackathons",
                        column: x => x.hackathon_id,
                        principalTable: "hackathons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "team_invitations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invited_by_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    normalized_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    responded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_team_invitations", x => x.id);
                    table.CheckConstraint("ck_team_invitations_status", "status IN ('Pending', 'Accepted', 'Declined', 'Revoked', 'Cancelled')");
                    table.ForeignKey(
                        name: "fk_team_invitations_accounts",
                        column: x => x.invited_by_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_team_invitations_hackathon_teams",
                        column: x => x.team_id,
                        principalTable: "hackathon_teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "team_join_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    responded_by_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    responded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_team_join_requests", x => x.id);
                    table.CheckConstraint("ck_team_join_requests_status", "status IN ('Pending', 'Accepted', 'Declined', 'Cancelled')");
                    table.ForeignKey(
                        name: "fk_team_join_requests_accounts",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_team_join_requests_hackathon_teams",
                        column: x => x.team_id,
                        principalTable: "hackathon_teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_hackathon_teams_created_by_account_id",
                table: "hackathon_teams",
                column: "created_by_account_id");

            migrationBuilder.CreateIndex(
                name: "ux_hackathon_teams_hackathon_name",
                table: "hackathon_teams",
                columns: new[] { "hackathon_id", "normalized_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_participations_account_id",
                table: "participations",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_participations_team_id",
                table: "participations",
                column: "team_id");

            migrationBuilder.CreateIndex(
                name: "ux_participations_active_account",
                table: "participations",
                columns: new[] { "hackathon_id", "account_id" },
                unique: true,
                filter: "left_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_team_invitations_invited_by_account_id",
                table: "team_invitations",
                column: "invited_by_account_id");

            migrationBuilder.CreateIndex(
                name: "ix_team_invitations_normalized_email",
                table: "team_invitations",
                column: "normalized_email");

            migrationBuilder.CreateIndex(
                name: "ix_team_invitations_team_id",
                table: "team_invitations",
                column: "team_id");

            migrationBuilder.CreateIndex(
                name: "ix_team_join_requests_account_id",
                table: "team_join_requests",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_team_join_requests_team_id",
                table: "team_join_requests",
                column: "team_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "participations");

            migrationBuilder.DropTable(
                name: "team_invitations");

            migrationBuilder.DropTable(
                name: "team_join_requests");

            migrationBuilder.DropTable(
                name: "hackathon_teams");
        }
    }
}
