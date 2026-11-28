using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace degra_parser_api.Migrations
{
    /// <inheritdoc />
    public partial class addurl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContentHash",
                table: "schedules",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SourceUrl",
                table: "schedules",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContentHash",
                table: "schedules");

            migrationBuilder.DropColumn(
                name: "SourceUrl",
                table: "schedules");
        }
    }
}
