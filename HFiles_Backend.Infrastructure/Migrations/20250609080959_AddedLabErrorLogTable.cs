using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HFilesBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddedLabErrorLogTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActionType",
                table: "LabAuditLogs");

            migrationBuilder.RenameColumn(
                name: "TableName",
                table: "LabAuditLogs",
                newName: "UserRole");

            migrationBuilder.RenameColumn(
                name: "Role",
                table: "LabAuditLogs",
                newName: "Url");

            migrationBuilder.RenameColumn(
                name: "RequestUrl",
                table: "LabAuditLogs",
                newName: "HttpMethod");

            migrationBuilder.RenameColumn(
                name: "RequestMethod",
                table: "LabAuditLogs",
                newName: "EntityName");

            migrationBuilder.RenameColumn(
                name: "RecordId",
                table: "LabAuditLogs",
                newName: "EntityId");

            migrationBuilder.RenameColumn(
                name: "LogCategory",
                table: "LabAuditLogs",
                newName: "Details");

            migrationBuilder.RenameColumn(
                name: "ExceptionDetails",
                table: "LabAuditLogs",
                newName: "Category");

            migrationBuilder.RenameColumn(
                name: "EpochTime",
                table: "LabAuditLogs",
                newName: "Timestamp");

            migrationBuilder.RenameColumn(
                name: "ChangedData",
                table: "LabAuditLogs",
                newName: "Action");

            migrationBuilder.CreateTable(
                name: "LabErrorLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    LabId = table.Column<int>(type: "int", nullable: true),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    UserRole = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EntityName = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    Action = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ErrorMessage = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StackTrace = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Timestamp = table.Column<long>(type: "bigint", nullable: true),
                    IpAddress = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SessionId = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Url = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    HttpMethod = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Category = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabErrorLogs", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LabErrorLogs");

            migrationBuilder.RenameColumn(
                name: "UserRole",
                table: "LabAuditLogs",
                newName: "TableName");

            migrationBuilder.RenameColumn(
                name: "Url",
                table: "LabAuditLogs",
                newName: "Role");

            migrationBuilder.RenameColumn(
                name: "Timestamp",
                table: "LabAuditLogs",
                newName: "EpochTime");

            migrationBuilder.RenameColumn(
                name: "HttpMethod",
                table: "LabAuditLogs",
                newName: "RequestUrl");

            migrationBuilder.RenameColumn(
                name: "EntityName",
                table: "LabAuditLogs",
                newName: "RequestMethod");

            migrationBuilder.RenameColumn(
                name: "EntityId",
                table: "LabAuditLogs",
                newName: "RecordId");

            migrationBuilder.RenameColumn(
                name: "Details",
                table: "LabAuditLogs",
                newName: "LogCategory");

            migrationBuilder.RenameColumn(
                name: "Category",
                table: "LabAuditLogs",
                newName: "ExceptionDetails");

            migrationBuilder.RenameColumn(
                name: "Action",
                table: "LabAuditLogs",
                newName: "ChangedData");

            migrationBuilder.AddColumn<string>(
                name: "ActionType",
                table: "LabAuditLogs",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }
    }
}
