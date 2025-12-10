using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HFilesBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueIndexToHFID : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HFID",
                table: "LabSignupUsers",
                type: "varchar(255)",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_LabSignupUsers_HFID",
                table: "LabSignupUsers",
                column: "HFID",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LabSignupUsers_HFID",
                table: "LabSignupUsers");

            migrationBuilder.DropColumn(
                name: "HFID",
                table: "LabSignupUsers");
        }
    }
}
