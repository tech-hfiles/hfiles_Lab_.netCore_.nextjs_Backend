using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HFilesBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCoachNavigationToHigh5Appointment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_High5Appointments_CoachId",
                table: "High5Appointments",
                column: "CoachId");

            migrationBuilder.AddForeignKey(
                name: "FK_High5Appointments_clinicmembers_CoachId",
                table: "High5Appointments",
                column: "CoachId",
                principalTable: "clinicmembers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_High5Appointments_clinicmembers_CoachId",
                table: "High5Appointments");

            migrationBuilder.DropIndex(
                name: "IX_High5Appointments_CoachId",
                table: "High5Appointments");
        }
    }
}
