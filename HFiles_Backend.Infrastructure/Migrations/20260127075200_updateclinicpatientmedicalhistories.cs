using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HFilesBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class updateclinicpatientmedicalhistories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmergencyNumber",
                table: "clinicpatientmedicalhistories",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmergencyNumber",
                table: "clinicpatientmedicalhistories");
        }
    }
}
