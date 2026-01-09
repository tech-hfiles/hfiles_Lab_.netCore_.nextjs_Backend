using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HFilesBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class High5FormImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "High5FormImages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ClinicId = table.Column<int>(type: "int", nullable: false),
                    PatientId = table.Column<int>(type: "int", nullable: false),
                    ClinicVisitId = table.Column<int>(type: "int", nullable: false),
                    FileUrl = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ConsentFormId = table.Column<int>(type: "int", nullable: true),
                    ConsentFormTitle = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EpochTime = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_High5FormImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_High5FormImages_clinicconsentforms_ConsentFormId",
                        column: x => x.ConsentFormId,
                        principalTable: "clinicconsentforms",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_High5FormImages_clinicpatients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "clinicpatients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_High5FormImages_clinicsignups_ClinicId",
                        column: x => x.ClinicId,
                        principalTable: "clinicsignups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_High5FormImages_clinicvisits_ClinicVisitId",
                        column: x => x.ClinicVisitId,
                        principalTable: "clinicvisits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_High5FormImages_ClinicId",
                table: "High5FormImages",
                column: "ClinicId");

            migrationBuilder.CreateIndex(
                name: "IX_High5FormImages_ClinicVisitId",
                table: "High5FormImages",
                column: "ClinicVisitId");

            migrationBuilder.CreateIndex(
                name: "IX_High5FormImages_ConsentFormId",
                table: "High5FormImages",
                column: "ConsentFormId");

            migrationBuilder.CreateIndex(
                name: "IX_High5FormImages_PatientId",
                table: "High5FormImages",
                column: "PatientId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "High5FormImages");
        }
    }
}
