using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HFilesBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddedClinicIdInBlacklistToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClinicId",
                table: "blacklisted_tokens",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_blacklisted_tokens_ClinicId",
                table: "blacklisted_tokens",
                column: "ClinicId");

            migrationBuilder.AddForeignKey(
                name: "FK_blacklisted_tokens_clinicsignups_ClinicId",
                table: "blacklisted_tokens",
                column: "ClinicId",
                principalTable: "clinicsignups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_blacklisted_tokens_clinicsignups_ClinicId",
                table: "blacklisted_tokens");

            migrationBuilder.DropIndex(
                name: "IX_blacklisted_tokens_ClinicId",
                table: "blacklisted_tokens");

            migrationBuilder.DropColumn(
                name: "ClinicId",
                table: "blacklisted_tokens");
        }
    }
}
