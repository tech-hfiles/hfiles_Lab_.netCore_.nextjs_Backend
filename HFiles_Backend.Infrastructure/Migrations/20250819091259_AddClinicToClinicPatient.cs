using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HFilesBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClinicToClinicPatient : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClinicId",
                table: "ClinicVisits",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_ClinicVisits_ClinicId",
                table: "ClinicVisits",
                column: "ClinicId");

            migrationBuilder.AddForeignKey(
                name: "FK_ClinicVisits_clinicsignups_ClinicId",
                table: "ClinicVisits",
                column: "ClinicId",
                principalTable: "clinicsignups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClinicVisits_clinicsignups_ClinicId",
                table: "ClinicVisits");

            migrationBuilder.DropIndex(
                name: "IX_ClinicVisits_ClinicId",
                table: "ClinicVisits");

            migrationBuilder.DropColumn(
                name: "ClinicId",
                table: "ClinicVisits");
        }
    }
}
