using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HFilesBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddpackageEnquiryhigh5 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PricingPackageId",
                table: "clinicEnquiry",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_clinicEnquiry_PricingPackageId",
                table: "clinicEnquiry",
                column: "PricingPackageId");

            migrationBuilder.AddForeignKey(
                name: "FK_clinicEnquiry_hifi5PricingPackages_PricingPackageId",
                table: "clinicEnquiry",
                column: "PricingPackageId",
                principalTable: "hifi5PricingPackages",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_clinicEnquiry_hifi5PricingPackages_PricingPackageId",
                table: "clinicEnquiry");

            migrationBuilder.DropIndex(
                name: "IX_clinicEnquiry_PricingPackageId",
                table: "clinicEnquiry");

            migrationBuilder.DropColumn(
                name: "PricingPackageId",
                table: "clinicEnquiry");
        }
    }
}
