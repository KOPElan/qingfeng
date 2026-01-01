using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QingFeng.Migrations
{
    /// <inheritdoc />
    public partial class RemoveFileIndexEntriesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FileIndexEntries");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FileIndexEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Extension = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    IndexedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsDirectory = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastModified = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Path = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    RootPath = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Size = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileIndexEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FileIndexEntries_Name",
                table: "FileIndexEntries",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_FileIndexEntries_Path",
                table: "FileIndexEntries",
                column: "Path");

            migrationBuilder.CreateIndex(
                name: "IX_FileIndexEntries_RootPath",
                table: "FileIndexEntries",
                column: "RootPath");
        }
    }
}
