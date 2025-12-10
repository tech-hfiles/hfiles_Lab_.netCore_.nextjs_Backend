using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HFilesBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClinicTreatmentTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClinicTreatments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ClinicId = table.Column<int>(type: "int", nullable: false),
                    TreatmentName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    QuantityPerDay = table.Column<int>(type: "int", nullable: false),
                    Cost = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    Total = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicTreatments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClinicTreatments_clinicsignups_ClinicId",
                        column: x => x.ClinicId,
                        principalTable: "clinicsignups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicTreatments_ClinicId",
                table: "ClinicTreatments",
                column: "ClinicId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClinicTreatments");
        }
    }
}
