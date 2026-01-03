using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HFilesBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class alterupdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClinicEnquiryCoaches_clinicmembers_CoachMemberId",
                table: "ClinicEnquiryCoaches");

            migrationBuilder.DropIndex(
                name: "IX_ClinicEnquiryCoaches_CoachMemberId",
                table: "ClinicEnquiryCoaches");

            migrationBuilder.DropColumn(
                name: "CoachMemberId",
                table: "ClinicEnquiryCoaches");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicEnquiryCoaches_CoachId",
                table: "ClinicEnquiryCoaches",
                column: "CoachId");

            migrationBuilder.AddForeignKey(
                name: "FK_ClinicEnquiryCoaches_clinicmembers_CoachId",
                table: "ClinicEnquiryCoaches",
                column: "CoachId",
                principalTable: "clinicmembers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClinicEnquiryCoaches_clinicmembers_CoachId",
                table: "ClinicEnquiryCoaches");

            migrationBuilder.DropIndex(
                name: "IX_ClinicEnquiryCoaches_CoachId",
                table: "ClinicEnquiryCoaches");

            migrationBuilder.AddColumn<int>(
                name: "CoachMemberId",
                table: "ClinicEnquiryCoaches",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ClinicEnquiryCoaches_CoachMemberId",
                table: "ClinicEnquiryCoaches",
                column: "CoachMemberId");

            migrationBuilder.AddForeignKey(
                name: "FK_ClinicEnquiryCoaches_clinicmembers_CoachMemberId",
                table: "ClinicEnquiryCoaches",
                column: "CoachMemberId",
                principalTable: "clinicmembers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
