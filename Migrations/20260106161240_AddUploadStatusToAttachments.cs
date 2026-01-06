using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QingFeng.Migrations
{
    /// <inheritdoc />
    public partial class AddUploadStatusToAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UploadErrorMessage",
                table: "AnydropAttachments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UploadStatus",
                table: "AnydropAttachments",
                type: "TEXT",
                nullable: false,
                defaultValue: "Completed");
                
            // Update existing records to have "Completed" status since they were already uploaded
            migrationBuilder.Sql(
                "UPDATE AnydropAttachments SET UploadStatus = 'Completed' WHERE UploadStatus IS NULL OR UploadStatus = '';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UploadErrorMessage",
                table: "AnydropAttachments");

            migrationBuilder.DropColumn(
                name: "UploadStatus",
                table: "AnydropAttachments");
        }
    }
}
