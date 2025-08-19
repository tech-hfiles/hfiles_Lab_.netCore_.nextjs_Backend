using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HFilesBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddedClinicIdAndStatusInAppointmentTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
           
            migrationBuilder.AddColumn<int>(
                name: "ClinicId",
                table: "ClinicAppointments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "ClinicAppointments",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicAppointments_ClinicId",
                table: "ClinicAppointments",
                column: "ClinicId");

            migrationBuilder.AddForeignKey(
                name: "FK_ClinicAppointments_clinicsignups_ClinicId",
                table: "ClinicAppointments",
                column: "ClinicId",
                principalTable: "clinicsignups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClinicAppointments_clinicsignups_ClinicId",
                table: "ClinicAppointments");

            migrationBuilder.DropIndex(
                name: "IX_ClinicAppointments_ClinicId",
                table: "ClinicAppointments");

            migrationBuilder.DropColumn(
                name: "ClinicId",
                table: "ClinicAppointments");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "ClinicAppointments");
        }
    }
}
