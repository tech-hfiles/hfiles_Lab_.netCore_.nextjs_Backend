using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HFilesBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddedClinicMemberTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClinicMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ClinicId = table.Column<int>(type: "int", nullable: false),
                    Role = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PasswordHash = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    PromotedBy = table.Column<int>(type: "int", nullable: false),
                    DeletedBy = table.Column<int>(type: "int", nullable: false),
                    EpochTime = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClinicMembers_ClinicSignups_ClinicId",
                        column: x => x.ClinicId,
                        principalTable: "ClinicSignups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClinicMembers_users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClinicMembers_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicMembers_ClinicId_DeletedBy",
                table: "ClinicMembers",
                columns: new[] { "ClinicId", "DeletedBy" });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicMembers_CreatedBy",
                table: "ClinicMembers",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicMembers_DeletedBy",
                table: "ClinicMembers",
                column: "DeletedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicMembers_Id_ClinicId_DeletedBy",
                table: "ClinicMembers",
                columns: new[] { "Id", "ClinicId", "DeletedBy" });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicMembers_PromotedBy",
                table: "ClinicMembers",
                column: "PromotedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicMembers_UserId_ClinicId_DeletedBy",
                table: "ClinicMembers",
                columns: new[] { "UserId", "ClinicId", "DeletedBy" });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicMembers_UserId_ClinicId_DeletedBy_Role",
                table: "ClinicMembers",
                columns: new[] { "UserId", "ClinicId", "DeletedBy", "Role" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClinicMembers");
        }
    }
}
