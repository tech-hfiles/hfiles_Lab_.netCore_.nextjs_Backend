using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HFilesBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateClinicPatientInPatientRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClinicPatientRecords_users_PatientId",
                table: "ClinicPatientRecords");

            migrationBuilder.AddForeignKey(
                name: "FK_ClinicPatientRecords_ClinicPatients_PatientId",
                table: "ClinicPatientRecords",
                column: "PatientId",
                principalTable: "ClinicPatients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClinicPatientRecords_ClinicPatients_PatientId",
                table: "ClinicPatientRecords");

            migrationBuilder.AddForeignKey(
                name: "FK_ClinicPatientRecords_users_PatientId",
                table: "ClinicPatientRecords",
                column: "PatientId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
