using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HFilesBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDurationAndSessionsToClinicTreatment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Duration",
                table: "ClinicTreatments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Sessions",
                table: "ClinicTreatments",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Duration",
                table: "ClinicTreatments");

            migrationBuilder.DropColumn(
                name: "Sessions",
                table: "ClinicTreatments");
        }
    }
}
