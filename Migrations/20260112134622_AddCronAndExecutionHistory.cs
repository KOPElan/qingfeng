using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QingFeng.Migrations
{
    /// <inheritdoc />
    public partial class AddCronAndExecutionHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CronExpression",
                table: "ScheduledTasks",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOneTime",
                table: "ScheduledTasks",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ScheduledTaskExecutionHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ScheduledTaskId = table.Column<int>(type: "INTEGER", nullable: false),
                    StartTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Result = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    DurationMs = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledTaskExecutionHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduledTaskExecutionHistories_ScheduledTasks_ScheduledTaskId",
                        column: x => x.ScheduledTaskId,
                        principalTable: "ScheduledTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledTaskExecutionHistories_ScheduledTaskId",
                table: "ScheduledTaskExecutionHistories",
                column: "ScheduledTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledTaskExecutionHistories_StartTime",
                table: "ScheduledTaskExecutionHistories",
                column: "StartTime");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScheduledTaskExecutionHistories");

            migrationBuilder.DropColumn(
                name: "CronExpression",
                table: "ScheduledTasks");

            migrationBuilder.DropColumn(
                name: "IsOneTime",
                table: "ScheduledTasks");
        }
    }
}
