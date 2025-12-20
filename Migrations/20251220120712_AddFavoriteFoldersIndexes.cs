using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QingFeng.Migrations
{
    /// <inheritdoc />
    public partial class AddFavoriteFoldersIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_FavoriteFolders_Order",
                table: "FavoriteFolders",
                column: "Order");

            migrationBuilder.CreateIndex(
                name: "IX_FavoriteFolders_Path",
                table: "FavoriteFolders",
                column: "Path",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FavoriteFolders_Order",
                table: "FavoriteFolders");

            migrationBuilder.DropIndex(
                name: "IX_FavoriteFolders_Path",
                table: "FavoriteFolders");
        }
    }
}
