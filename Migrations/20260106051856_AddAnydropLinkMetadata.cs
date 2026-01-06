using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QingFeng.Migrations
{
    /// <inheritdoc />
    public partial class AddAnydropLinkMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LinkDescription",
                table: "AnydropMessages",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LinkTitle",
                table: "AnydropMessages",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LinkUrl",
                table: "AnydropMessages",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LinkDescription",
                table: "AnydropMessages");

            migrationBuilder.DropColumn(
                name: "LinkTitle",
                table: "AnydropMessages");

            migrationBuilder.DropColumn(
                name: "LinkUrl",
                table: "AnydropMessages");
        }
    }
}
