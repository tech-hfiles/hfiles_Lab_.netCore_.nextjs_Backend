using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HFilesBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ClinicPatientMedicalHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AggravatingFactors",
                table: "clinicpatientmedicalhistories",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Duration",
                table: "clinicpatientmedicalhistories",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Frequency",
                table: "clinicpatientmedicalhistories",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Intensity",
                table: "clinicpatientmedicalhistories",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "NatureofPain",
                table: "clinicpatientmedicalhistories",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "RelievingFacors",
                table: "clinicpatientmedicalhistories",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AggravatingFactors",
                table: "clinicpatientmedicalhistories");

            migrationBuilder.DropColumn(
                name: "Duration",
                table: "clinicpatientmedicalhistories");

            migrationBuilder.DropColumn(
                name: "Frequency",
                table: "clinicpatientmedicalhistories");

            migrationBuilder.DropColumn(
                name: "Intensity",
                table: "clinicpatientmedicalhistories");

            migrationBuilder.DropColumn(
                name: "NatureofPain",
                table: "clinicpatientmedicalhistories");

            migrationBuilder.DropColumn(
                name: "RelievingFacors",
                table: "clinicpatientmedicalhistories");
        }
    }
}
