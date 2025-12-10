using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HFilesBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameLabTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(name: "LabSignupUsers", newName: "LabSignups");
            migrationBuilder.RenameTable(name: "LabAdmins", newName: "LabSuperAdmins");
        }


        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(name: "LabSignups", newName: "LabSignupUsers");
            migrationBuilder.RenameTable(name: "LabSuperAdmins", newName: "LabAdmins");
        }
    }
}
