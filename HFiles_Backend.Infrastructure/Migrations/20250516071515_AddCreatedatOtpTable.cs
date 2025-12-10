using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HFilesBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatedatOtpTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_OtpEntries",
                table: "OtpEntries");

            migrationBuilder.RenameTable(
                name: "OtpEntries",
                newName: "LabOtpEntries");

            migrationBuilder.AddPrimaryKey(
                name: "PK_LabOtpEntries",
                table: "LabOtpEntries",
                column: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_LabOtpEntries",
                table: "LabOtpEntries");

            migrationBuilder.RenameTable(
                name: "LabOtpEntries",
                newName: "OtpEntries");

            migrationBuilder.AddPrimaryKey(
                name: "PK_OtpEntries",
                table: "OtpEntries",
                column: "Id");
        }
    }
}
