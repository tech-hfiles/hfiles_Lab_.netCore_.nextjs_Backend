using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HFilesBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ClinicMemberRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
    "DROP TABLE IF EXISTS `clinic_member_reports`;"
);


            migrationBuilder.AddColumn<int>(
                name: "ClinicMemberId",
                table: "clinicMemberRecords",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_clinicMemberRecords_ClinicMemberId",
                table: "clinicMemberRecords",
                column: "ClinicMemberId");

            migrationBuilder.AddForeignKey(
                name: "FK_clinicMemberRecords_clinicmembers_ClinicMemberId",
                table: "clinicMemberRecords",
                column: "ClinicMemberId",
                principalTable: "clinicmembers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_clinicMemberRecords_clinicmembers_ClinicMemberId",
                table: "clinicMemberRecords");

            migrationBuilder.DropIndex(
                name: "IX_clinicMemberRecords_ClinicMemberId",
                table: "clinicMemberRecords");

            migrationBuilder.DropColumn(
                name: "ClinicMemberId",
                table: "clinicMemberRecords");

            migrationBuilder.CreateTable(
                name: "clinic_member_reports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ClinicId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    DeletedBy = table.Column<int>(type: "int", nullable: false),
                    EpochTime = table.Column<long>(type: "bigint", nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    ReportName = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReportType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReportUrl = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clinic_member_reports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_clinic_member_reports_clinicsignups_ClinicId",
                        column: x => x.ClinicId,
                        principalTable: "clinicsignups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_clinic_member_reports_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_clinic_member_reports_ClinicId",
                table: "clinic_member_reports",
                column: "ClinicId");

            migrationBuilder.CreateIndex(
                name: "IX_clinic_member_reports_UserId",
                table: "clinic_member_reports",
                column: "UserId");
        }
    }
}
