using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HFilesBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Hifi5PricingPackage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClinicId",
                table: "hifi5PricingPackages",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_hifi5PricingPackages_ClinicId",
                table: "hifi5PricingPackages",
                column: "ClinicId");

            migrationBuilder.AddForeignKey(
                name: "FK_hifi5PricingPackages_clinicsignups_ClinicId",
                table: "hifi5PricingPackages",
                column: "ClinicId",
                principalTable: "clinicsignups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_hifi5PricingPackages_clinicsignups_ClinicId",
                table: "hifi5PricingPackages");

            migrationBuilder.DropIndex(
                name: "IX_hifi5PricingPackages_ClinicId",
                table: "hifi5PricingPackages");

            migrationBuilder.DropColumn(
                name: "ClinicId",
                table: "hifi5PricingPackages");
        }
    }
}
