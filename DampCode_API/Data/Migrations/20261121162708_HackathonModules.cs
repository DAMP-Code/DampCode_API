using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DampCode_API.Data.Migrations
{
    /// <inheritdoc />
    public partial class HackathonModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "hackathon_modules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    hackathon_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_hackathon_modules", x => x.id);
                    table.ForeignKey(
                        name: "fk_hackathon_modules_hackathons",
                        column: x => x.hackathon_id,
                        principalTable: "hackathons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "hackathon_module_contents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    module_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    content_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    body = table.Column<string>(type: "text", nullable: true),
                    url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_hackathon_module_contents", x => x.id);
                    table.CheckConstraint("ck_hackathon_module_contents_content_type", "content_type IN ('Text', 'Link', 'Video')");
                    table.ForeignKey(
                        name: "fk_hackathon_module_contents_hackathon_modules",
                        column: x => x.module_id,
                        principalTable: "hackathon_modules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_hackathon_module_contents_position",
                table: "hackathon_module_contents",
                columns: new[] { "module_id", "position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_hackathon_modules_position",
                table: "hackathon_modules",
                columns: new[] { "hackathon_id", "position" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "hackathon_module_contents");

            migrationBuilder.DropTable(
                name: "hackathon_modules");
        }
    }
}
