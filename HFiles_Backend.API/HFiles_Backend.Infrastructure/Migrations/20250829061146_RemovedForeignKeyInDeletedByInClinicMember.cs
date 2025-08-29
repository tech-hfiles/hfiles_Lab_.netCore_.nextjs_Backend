using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HFilesBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemovedForeignKeyInDeletedByInClinicMember : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_clinicmembers_users_DeletedBy",
                table: "clinicmembers");

            migrationBuilder.DropIndex(
                name: "IX_clinicmembers_DeletedBy",
                table: "clinicmembers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_clinicmembers_DeletedBy",
                table: "clinicmembers",
                column: "DeletedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_clinicmembers_users_DeletedBy",
                table: "clinicmembers",
                column: "DeletedBy",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
