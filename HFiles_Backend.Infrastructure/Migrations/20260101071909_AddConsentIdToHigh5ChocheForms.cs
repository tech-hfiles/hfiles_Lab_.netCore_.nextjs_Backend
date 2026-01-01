using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HFilesBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddConsentIdToHigh5ChocheForms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ConsentId",
                table: "high5ChocheForms",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ReportType",
                table: "clinicMemberRecords",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConsentId",
                table: "high5ChocheForms");

            migrationBuilder.UpdateData(
                table: "clinicMemberRecords",
                keyColumn: "ReportType",
                keyValue: null,
                column: "ReportType",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "ReportType",
                table: "clinicMemberRecords",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
