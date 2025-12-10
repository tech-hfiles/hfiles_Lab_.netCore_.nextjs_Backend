using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HFilesBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddedClinicVisitIdInPatientRecordsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClinicVisitId",
                table: "ClinicPatientRecords",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_ClinicPatientRecords_ClinicVisitId",
                table: "ClinicPatientRecords",
                column: "ClinicVisitId");

            migrationBuilder.AddForeignKey(
                name: "FK_ClinicPatientRecords_ClinicVisits_ClinicVisitId",
                table: "ClinicPatientRecords",
                column: "ClinicVisitId",
                principalTable: "ClinicVisits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClinicPatientRecords_ClinicVisits_ClinicVisitId",
                table: "ClinicPatientRecords");

            migrationBuilder.DropIndex(
                name: "IX_ClinicPatientRecords_ClinicVisitId",
                table: "ClinicPatientRecords");

            migrationBuilder.DropColumn(
                name: "ClinicVisitId",
                table: "ClinicPatientRecords");
        }
    }
}
