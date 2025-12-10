using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HFilesBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddedIndexingInAppointmentsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "idx_clinicappointments_date_time",
                table: "clinicappointments",
                columns: new[] { "AppointmentDate", "AppointmentTime" });

            migrationBuilder.CreateIndex(
                name: "idx_clinicappointments_status_date",
                table: "clinicappointments",
                columns: new[] { "Status", "AppointmentDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_clinicappointments_date_time",
                table: "clinicappointments");

            migrationBuilder.DropIndex(
                name: "idx_clinicappointments_status_date",
                table: "clinicappointments");
        }
    }
}
