using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HFilesBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenamedPrescriptionNotesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_clinicPrescriptionNotes_clinicsignups_ClinicId",
                table: "clinicPrescriptionNotes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_clinicPrescriptionNotes",
                table: "clinicPrescriptionNotes");

            migrationBuilder.RenameTable(
                name: "clinicPrescriptionNotes",
                newName: "clinicprescriptionnotes");

            migrationBuilder.RenameIndex(
                name: "IX_clinicPrescriptionNotes_ClinicId",
                table: "clinicprescriptionnotes",
                newName: "IX_clinicprescriptionnotes_ClinicId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_clinicprescriptionnotes",
                table: "clinicprescriptionnotes",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_clinicprescriptionnotes_clinicsignups_ClinicId",
                table: "clinicprescriptionnotes",
                column: "ClinicId",
                principalTable: "clinicsignups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_clinicprescriptionnotes_clinicsignups_ClinicId",
                table: "clinicprescriptionnotes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_clinicprescriptionnotes",
                table: "clinicprescriptionnotes");

            migrationBuilder.RenameTable(
                name: "clinicprescriptionnotes",
                newName: "clinicPrescriptionNotes");

            migrationBuilder.RenameIndex(
                name: "IX_clinicprescriptionnotes_ClinicId",
                table: "clinicPrescriptionNotes",
                newName: "IX_clinicPrescriptionNotes_ClinicId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_clinicPrescriptionNotes",
                table: "clinicPrescriptionNotes",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_clinicPrescriptionNotes_clinicsignups_ClinicId",
                table: "clinicPrescriptionNotes",
                column: "ClinicId",
                principalTable: "clinicsignups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
