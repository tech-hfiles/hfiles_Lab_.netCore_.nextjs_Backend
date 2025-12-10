using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HFilesBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameToLabAuditLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_AuditLogs",
                table: "AuditLogs");

            migrationBuilder.RenameTable(
                name: "AuditLogs",
                newName: "LabAuditLogs");

            migrationBuilder.AddPrimaryKey(
                name: "PK_LabAuditLogs",
                table: "LabAuditLogs",
                column: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_LabAuditLogs",
                table: "LabAuditLogs");

            migrationBuilder.RenameTable(
                name: "LabAuditLogs",
                newName: "AuditLogs");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AuditLogs",
                table: "AuditLogs",
                column: "Id");
        }
    }
}
