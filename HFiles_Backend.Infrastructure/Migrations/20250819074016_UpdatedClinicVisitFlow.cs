using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HFilesBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdatedClinicVisitFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClinicVisit_ClinicPatient_ClinicPatientId",
                table: "ClinicVisit");

            migrationBuilder.DropForeignKey(
                name: "FK_ClinicVisitConsentForm_ClinicConsentForm_ConsentFormId",
                table: "ClinicVisitConsentForm");

            migrationBuilder.DropForeignKey(
                name: "FK_ClinicVisitConsentForm_ClinicVisit_ClinicVisitId",
                table: "ClinicVisitConsentForm");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ClinicVisitConsentForm",
                table: "ClinicVisitConsentForm");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ClinicVisit",
                table: "ClinicVisit");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ClinicPatient",
                table: "ClinicPatient");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ClinicConsentForm",
                table: "ClinicConsentForm");

            migrationBuilder.RenameTable(
                name: "ClinicVisitConsentForm",
                newName: "ClinicVisitConsentForms");

            migrationBuilder.RenameTable(
                name: "ClinicVisit",
                newName: "ClinicVisits");

            migrationBuilder.RenameTable(
                name: "ClinicPatient",
                newName: "ClinicPatients");

            migrationBuilder.RenameTable(
                name: "ClinicConsentForm",
                newName: "ClinicConsentForms");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ClinicVisitConsentForms",
                table: "ClinicVisitConsentForms",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ClinicVisits",
                table: "ClinicVisits",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ClinicPatients",
                table: "ClinicPatients",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ClinicConsentForms",
                table: "ClinicConsentForms",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ClinicVisitConsentForms_ClinicConsentForms_ConsentFormId",
                table: "ClinicVisitConsentForms",
                column: "ConsentFormId",
                principalTable: "ClinicConsentForms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ClinicVisitConsentForms_ClinicVisits_ClinicVisitId",
                table: "ClinicVisitConsentForms",
                column: "ClinicVisitId",
                principalTable: "ClinicVisits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ClinicVisits_ClinicPatients_ClinicPatientId",
                table: "ClinicVisits",
                column: "ClinicPatientId",
                principalTable: "ClinicPatients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClinicVisitConsentForms_ClinicConsentForms_ConsentFormId",
                table: "ClinicVisitConsentForms");

            migrationBuilder.DropForeignKey(
                name: "FK_ClinicVisitConsentForms_ClinicVisits_ClinicVisitId",
                table: "ClinicVisitConsentForms");

            migrationBuilder.DropForeignKey(
                name: "FK_ClinicVisits_ClinicPatients_ClinicPatientId",
                table: "ClinicVisits");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ClinicVisits",
                table: "ClinicVisits");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ClinicVisitConsentForms",
                table: "ClinicVisitConsentForms");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ClinicPatients",
                table: "ClinicPatients");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ClinicConsentForms",
                table: "ClinicConsentForms");

            migrationBuilder.RenameTable(
                name: "ClinicVisits",
                newName: "ClinicVisit");

            migrationBuilder.RenameTable(
                name: "ClinicVisitConsentForms",
                newName: "ClinicVisitConsentForm");

            migrationBuilder.RenameTable(
                name: "ClinicPatients",
                newName: "ClinicPatient");

            migrationBuilder.RenameTable(
                name: "ClinicConsentForms",
                newName: "ClinicConsentForm");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ClinicVisit",
                table: "ClinicVisit",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ClinicVisitConsentForm",
                table: "ClinicVisitConsentForm",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ClinicPatient",
                table: "ClinicPatient",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ClinicConsentForm",
                table: "ClinicConsentForm",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ClinicVisit_ClinicPatient_ClinicPatientId",
                table: "ClinicVisit",
                column: "ClinicPatientId",
                principalTable: "ClinicPatient",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ClinicVisitConsentForm_ClinicConsentForm_ConsentFormId",
                table: "ClinicVisitConsentForm",
                column: "ConsentFormId",
                principalTable: "ClinicConsentForm",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ClinicVisitConsentForm_ClinicVisit_ClinicVisitId",
                table: "ClinicVisitConsentForm",
                column: "ClinicVisitId",
                principalTable: "ClinicVisit",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
