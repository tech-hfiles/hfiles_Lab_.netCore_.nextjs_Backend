using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HFilesBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ClinicEnquiryCoachTableCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClinicEnquiryCoach",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    EnquiryId = table.Column<int>(type: "int", nullable: false),
                    CoachId = table.Column<int>(type: "int", nullable: false),
                    EpochTime = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicEnquiryCoach", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClinicEnquiryCoach_clinicEnquiry_EnquiryId",
                        column: x => x.EnquiryId,
                        principalTable: "clinicEnquiry",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClinicEnquiryCoach_clinicMemberRecords_CoachId",
                        column: x => x.CoachId,
                        principalTable: "clinicMemberRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicEnquiryCoach_CoachId",
                table: "ClinicEnquiryCoach",
                column: "CoachId");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicEnquiryCoach_EnquiryId",
                table: "ClinicEnquiryCoach",
                column: "EnquiryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClinicEnquiryCoach");
        }
    }
}
