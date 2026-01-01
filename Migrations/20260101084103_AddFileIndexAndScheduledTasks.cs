using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QingFeng.Migrations
{
    /// <inheritdoc />
    public partial class AddFileIndexAndScheduledTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FileIndexEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Path = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    IsDirectory = table.Column<bool>(type: "INTEGER", nullable: false),
                    Size = table.Column<long>(type: "INTEGER", nullable: false),
                    LastModified = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Extension = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    IndexedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RootPath = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileIndexEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScheduledTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    TaskType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Configuration = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    IntervalMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    LastRunTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    NextRunTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    LastError = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledTasks", x => x.Id);
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

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledTasks_NextRunTime",
                table: "ScheduledTasks",
                column: "NextRunTime");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledTasks_TaskType",
                table: "ScheduledTasks",
                column: "TaskType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FileIndexEntries");

            migrationBuilder.DropTable(
                name: "ScheduledTasks");
        }
    }
}
