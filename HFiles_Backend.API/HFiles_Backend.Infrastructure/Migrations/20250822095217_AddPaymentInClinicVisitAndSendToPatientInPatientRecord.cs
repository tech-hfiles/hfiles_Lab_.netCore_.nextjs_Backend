using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HFilesBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentInClinicVisitAndSendToPatientInPatientRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PaymentMethod",
                table: "ClinicVisits",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SendToPatient",
                table: "ClinicPatientRecords",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "ClinicVisits");

            migrationBuilder.DropColumn(
                name: "SendToPatient",
                table: "ClinicPatientRecords");
        }
    }
}
