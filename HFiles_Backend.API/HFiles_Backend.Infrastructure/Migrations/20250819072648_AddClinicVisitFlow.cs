using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HFilesBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClinicVisitFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClinicConsentForm",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Title = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicConsentForm", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ClinicPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    HFID = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PatientName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicPatient", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ClinicVisit",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ClinicPatientId = table.Column<int>(type: "int", nullable: false),
                    AppointmentDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AppointmentTime = table.Column<TimeSpan>(type: "time(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicVisit", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClinicVisit_ClinicPatient_ClinicPatientId",
                        column: x => x.ClinicPatientId,
                        principalTable: "ClinicPatient",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ClinicVisitConsentForm",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ClinicVisitId = table.Column<int>(type: "int", nullable: false),
                    ConsentFormId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicVisitConsentForm", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClinicVisitConsentForm_ClinicConsentForm_ConsentFormId",
                        column: x => x.ConsentFormId,
                        principalTable: "ClinicConsentForm",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClinicVisitConsentForm_ClinicVisit_ClinicVisitId",
                        column: x => x.ClinicVisitId,
                        principalTable: "ClinicVisit",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicPatients_HFID",
                table: "ClinicPatient",
                column: "HFID");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicVisits_AppointmentDate",
                table: "ClinicVisit",
                column: "AppointmentDate");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicVisits_ClinicPatientId",
                table: "ClinicVisit",
                column: "ClinicPatientId");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicVisits_Date_Patient",
                table: "ClinicVisit",
                columns: new[] { "AppointmentDate", "ClinicPatientId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicVisitConsentForm_ClinicVisitId",
                table: "ClinicVisitConsentForm",
                column: "ClinicVisitId");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicVisitConsentForm_ConsentFormId",
                table: "ClinicVisitConsentForm",
                column: "ConsentFormId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClinicVisitConsentForm");

            migrationBuilder.DropTable(
                name: "ClinicConsentForm");

            migrationBuilder.DropTable(
                name: "ClinicVisit");

            migrationBuilder.DropTable(
                name: "ClinicPatient");
        }
    }
}
