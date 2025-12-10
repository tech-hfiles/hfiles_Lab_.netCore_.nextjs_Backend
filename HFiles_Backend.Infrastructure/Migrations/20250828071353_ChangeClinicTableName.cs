using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HFilesBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChangeClinicTableName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClinicMembers_clinicsignups_ClinicId",
                table: "ClinicMembers");

            migrationBuilder.DropForeignKey(
                name: "FK_ClinicMembers_users_CreatedBy",
                table: "ClinicMembers");

            migrationBuilder.DropForeignKey(
                name: "FK_ClinicMembers_users_DeletedBy",
                table: "ClinicMembers");

            migrationBuilder.DropForeignKey(
                name: "FK_ClinicMembers_users_PromotedBy",
                table: "ClinicMembers");

            migrationBuilder.DropForeignKey(
                name: "FK_ClinicMembers_users_UserId",
                table: "ClinicMembers");

            migrationBuilder.DropForeignKey(
                name: "FK_ClinicSuperAdmins_clinicsignups_ClinicId",
                table: "ClinicSuperAdmins");

            migrationBuilder.DropForeignKey(
                name: "FK_ClinicSuperAdmins_users_UserId",
                table: "ClinicSuperAdmins");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ClinicSuperAdmins",
                table: "ClinicSuperAdmins");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ClinicMembers",
                table: "ClinicMembers");

            migrationBuilder.RenameTable(
                name: "ClinicSuperAdmins",
                newName: "clinicsuperadmins");

            migrationBuilder.RenameTable(
                name: "ClinicMembers",
                newName: "clinicmembers");

            migrationBuilder.RenameIndex(
                name: "IX_ClinicMembers_PromotedBy",
                table: "clinicmembers",
                newName: "IX_clinicmembers_PromotedBy");

            migrationBuilder.RenameIndex(
                name: "IX_ClinicMembers_DeletedBy",
                table: "clinicmembers",
                newName: "IX_clinicmembers_DeletedBy");

            migrationBuilder.RenameIndex(
                name: "IX_ClinicMembers_CreatedBy",
                table: "clinicmembers",
                newName: "IX_clinicmembers_CreatedBy");

            migrationBuilder.AddPrimaryKey(
                name: "PK_clinicsuperadmins",
                table: "clinicsuperadmins",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_clinicmembers",
                table: "clinicmembers",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_clinicmembers_clinicsignups_ClinicId",
                table: "clinicmembers",
                column: "ClinicId",
                principalTable: "clinicsignups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_clinicmembers_users_CreatedBy",
                table: "clinicmembers",
                column: "CreatedBy",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_clinicmembers_users_DeletedBy",
                table: "clinicmembers",
                column: "DeletedBy",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_clinicmembers_users_PromotedBy",
                table: "clinicmembers",
                column: "PromotedBy",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_clinicmembers_users_UserId",
                table: "clinicmembers",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_clinicsuperadmins_clinicsignups_ClinicId",
                table: "clinicsuperadmins",
                column: "ClinicId",
                principalTable: "clinicsignups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_clinicsuperadmins_users_UserId",
                table: "clinicsuperadmins",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_clinicmembers_clinicsignups_ClinicId",
                table: "clinicmembers");

            migrationBuilder.DropForeignKey(
                name: "FK_clinicmembers_users_CreatedBy",
                table: "clinicmembers");

            migrationBuilder.DropForeignKey(
                name: "FK_clinicmembers_users_DeletedBy",
                table: "clinicmembers");

            migrationBuilder.DropForeignKey(
                name: "FK_clinicmembers_users_PromotedBy",
                table: "clinicmembers");

            migrationBuilder.DropForeignKey(
                name: "FK_clinicmembers_users_UserId",
                table: "clinicmembers");

            migrationBuilder.DropForeignKey(
                name: "FK_clinicsuperadmins_clinicsignups_ClinicId",
                table: "clinicsuperadmins");

            migrationBuilder.DropForeignKey(
                name: "FK_clinicsuperadmins_users_UserId",
                table: "clinicsuperadmins");

            migrationBuilder.DropPrimaryKey(
                name: "PK_clinicsuperadmins",
                table: "clinicsuperadmins");

            migrationBuilder.DropPrimaryKey(
                name: "PK_clinicmembers",
                table: "clinicmembers");

            migrationBuilder.RenameTable(
                name: "clinicsuperadmins",
                newName: "ClinicSuperAdmins");

            migrationBuilder.RenameTable(
                name: "clinicmembers",
                newName: "ClinicMembers");

            migrationBuilder.RenameIndex(
                name: "IX_clinicmembers_PromotedBy",
                table: "ClinicMembers",
                newName: "IX_ClinicMembers_PromotedBy");

            migrationBuilder.RenameIndex(
                name: "IX_clinicmembers_DeletedBy",
                table: "ClinicMembers",
                newName: "IX_ClinicMembers_DeletedBy");

            migrationBuilder.RenameIndex(
                name: "IX_clinicmembers_CreatedBy",
                table: "ClinicMembers",
                newName: "IX_ClinicMembers_CreatedBy");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ClinicSuperAdmins",
                table: "ClinicSuperAdmins",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ClinicMembers",
                table: "ClinicMembers",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ClinicMembers_clinicsignups_ClinicId",
                table: "ClinicMembers",
                column: "ClinicId",
                principalTable: "clinicsignups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ClinicMembers_users_CreatedBy",
                table: "ClinicMembers",
                column: "CreatedBy",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ClinicMembers_users_DeletedBy",
                table: "ClinicMembers",
                column: "DeletedBy",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ClinicMembers_users_PromotedBy",
                table: "ClinicMembers",
                column: "PromotedBy",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ClinicMembers_users_UserId",
                table: "ClinicMembers",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ClinicSuperAdmins_clinicsignups_ClinicId",
                table: "ClinicSuperAdmins",
                column: "ClinicId",
                principalTable: "clinicsignups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ClinicSuperAdmins_users_UserId",
                table: "ClinicSuperAdmins",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
