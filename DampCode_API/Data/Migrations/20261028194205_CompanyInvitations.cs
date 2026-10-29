using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DampCode_API.Data.Migrations
{
    /// <inheritdoc />
    public partial class CompanyInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "company_invitations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invited_by_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    normalized_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    job_title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    responded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_company_invitations", x => x.id);
                    table.CheckConstraint("ck_company_invitations_status", "status IN ('Pending', 'Accepted', 'Declined', 'Revoked')");
                    table.ForeignKey(
                        name: "fk_company_invitations_accounts",
                        column: x => x.invited_by_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_company_invitations_companies",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_company_invitations_company_id",
                table: "company_invitations",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_company_invitations_invited_by_account_id",
                table: "company_invitations",
                column: "invited_by_account_id");

            migrationBuilder.CreateIndex(
                name: "ix_company_invitations_normalized_email",
                table: "company_invitations",
                column: "normalized_email");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "company_invitations");
        }
    }
}
