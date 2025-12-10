using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HFilesBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddedGoogleCalendarInClinicSignups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GoogleCalendarId",
                table: "clinicsignups",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "GoogleCredentialsPath",
                table: "clinicsignups",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "GoogleServiceAccountEmail",
                table: "clinicsignups",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GoogleCalendarId",
                table: "clinicsignups");

            migrationBuilder.DropColumn(
                name: "GoogleCredentialsPath",
                table: "clinicsignups");

            migrationBuilder.DropColumn(
                name: "GoogleServiceAccountEmail",
                table: "clinicsignups");
        }
    }
}
