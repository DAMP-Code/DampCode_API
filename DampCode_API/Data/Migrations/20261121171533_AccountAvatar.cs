using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DampCode_API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AccountAvatar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "avatar_color",
                table: "accounts",
                type: "character varying(7)",
                maxLength: 7,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "avatar_image",
                table: "accounts",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "avatar_color",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "avatar_image",
                table: "accounts");
        }
    }
}
