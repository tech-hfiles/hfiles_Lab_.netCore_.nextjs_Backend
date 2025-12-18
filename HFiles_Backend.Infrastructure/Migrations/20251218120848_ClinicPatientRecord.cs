using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HFilesBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ClinicPatientRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "EpochTime",
                table: "clinicpatientrecords",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<bool>(
                name: "Is_Cansel",
                table: "clinicpatientrecords",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Is_editable",
                table: "clinicpatientrecords",
                type: "tinyint(1)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Reference_Id",
                table: "clinicpatientrecords",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "payment_verify",
                table: "clinicpatientrecords",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EpochTime",
                table: "clinicpatientrecords");

            migrationBuilder.DropColumn(
                name: "Is_Cansel",
                table: "clinicpatientrecords");

            migrationBuilder.DropColumn(
                name: "Is_editable",
                table: "clinicpatientrecords");

            migrationBuilder.DropColumn(
                name: "Reference_Id",
                table: "clinicpatientrecords");

            migrationBuilder.DropColumn(
                name: "payment_verify",
                table: "clinicpatientrecords");
        }
    }
}
