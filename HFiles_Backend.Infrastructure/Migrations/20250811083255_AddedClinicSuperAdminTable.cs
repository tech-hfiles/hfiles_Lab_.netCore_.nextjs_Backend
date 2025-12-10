using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HFilesBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddedClinicSuperAdminTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClinicSuperAdmins",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ClinicId = table.Column<int>(type: "int", nullable: false),
                    PasswordHash = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EpochTime = table.Column<long>(type: "bigint", nullable: false),
                    IsMain = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicSuperAdmins", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClinicSuperAdmins_ClinicSignups_ClinicId",
                        column: x => x.ClinicId,
                        principalTable: "ClinicSignups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClinicSuperAdmins_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicSuperAdmins_ClinicId_IsMain",
                table: "ClinicSuperAdmins",
                columns: new[] { "ClinicId", "IsMain" });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicSuperAdmins_Id_UserId",
                table: "ClinicSuperAdmins",
                columns: new[] { "Id", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicSuperAdmins_UserId_ClinicId_IsMain",
                table: "ClinicSuperAdmins",
                columns: new[] { "UserId", "ClinicId", "IsMain" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClinicSuperAdmins");
        }
    }
}
