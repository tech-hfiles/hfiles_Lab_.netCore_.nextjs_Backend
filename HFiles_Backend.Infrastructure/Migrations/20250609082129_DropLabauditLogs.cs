using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HFilesBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropLabauditLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "labauditlogs");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "labauditlogs",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    LabId = table.Column<int>(nullable: true),
                    UserId = table.Column<int>(nullable: true),
                    UserRole = table.Column<string>(nullable: true),
                    EntityName = table.Column<string>(nullable: true),
                    EntityId = table.Column<int>(nullable: true),
                    Action = table.Column<string>(nullable: true),
                    Details = table.Column<string>(nullable: true),
                    Timestamp = table.Column<long>(nullable: true),
                    IpAddress = table.Column<string>(nullable: true),
                    SessionId = table.Column<string>(nullable: true),
                    Url = table.Column<string>(nullable: true),
                    HttpMethod = table.Column<string>(nullable: true),
                    Category = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_labauditlogs", x => x.Id);
                }
            );
        }

    }
}
