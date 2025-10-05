using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HFilesBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SentToUserId",
                table: "labauditlogs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SentToUserNotifications",
                table: "labauditlogs",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SentToUserId",
                table: "labauditlogs");

            migrationBuilder.DropColumn(
                name: "SentToUserNotifications",
                table: "labauditlogs");
        }
    }
}
