using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HFilesBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIndexing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Pincode",
                table: "LabSignups",
                type: "varchar(255)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: "LabSignups",
                type: "varchar(255)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "LabSignups",
                type: "varchar(255)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "LabOtpEntries",
                type: "varchar(255)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "LabMembers",
                type: "varchar(255)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<long>(
                name: "Timestamp",
                table: "LabAuditLogs",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabSuperAdmins_Id_UserId",
                table: "LabSuperAdmins",
                columns: new[] { "Id", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_LabSuperAdmins_LabId_IsMain",
                table: "LabSuperAdmins",
                columns: new[] { "LabId", "IsMain" });

            migrationBuilder.CreateIndex(
                name: "IX_LabSuperAdmins_UserId_LabId_IsMain",
                table: "LabSuperAdmins",
                columns: new[] { "UserId", "LabId", "IsMain" });

            migrationBuilder.CreateIndex(
                name: "IX_LabSignup_Email",
                table: "LabSignups",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabSignup_Id",
                table: "LabSignups",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_LabSignups_Id_Email_DeletedBy",
                table: "LabSignups",
                columns: new[] { "Id", "Email", "DeletedBy" });

            migrationBuilder.CreateIndex(
                name: "IX_LabSignups_Id_Reference",
                table: "LabSignups",
                columns: new[] { "Id", "LabReference" });

            migrationBuilder.CreateIndex(
                name: "IX_LabSignups_LabReference_DeletedBy",
                table: "LabSignups",
                columns: new[] { "LabReference", "DeletedBy" });

            migrationBuilder.CreateIndex(
                name: "IX_LabSignups_PhoneNumber",
                table: "LabSignups",
                column: "PhoneNumber");

            migrationBuilder.CreateIndex(
                name: "IX_LabSignups_Pincode",
                table: "LabSignups",
                column: "Pincode");

            migrationBuilder.CreateIndex(
                name: "IX_LabOtpEntries_Email_CreatedAt",
                table: "LabOtpEntries",
                columns: new[] { "Email", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LabOtpEntries_Email_CreatedAt_ExpiryTime",
                table: "LabOtpEntries",
                columns: new[] { "Email", "CreatedAt", "ExpiryTime" });

            migrationBuilder.CreateIndex(
                name: "IX_LabOtpEntry_Email",
                table: "LabOtpEntries",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_LabMembers_Id_LabId_DeletedBy",
                table: "LabMembers",
                columns: new[] { "Id", "LabId", "DeletedBy" });

            migrationBuilder.CreateIndex(
                name: "IX_LabMembers_LabId_DeletedBy",
                table: "LabMembers",
                columns: new[] { "LabId", "DeletedBy" });

            migrationBuilder.CreateIndex(
                name: "IX_LabMembers_UserId_LabId_DeletedBy",
                table: "LabMembers",
                columns: new[] { "UserId", "LabId", "DeletedBy" });

            migrationBuilder.CreateIndex(
                name: "IX_LabMembers_UserId_LabId_DeletedBy_Role",
                table: "LabMembers",
                columns: new[] { "UserId", "LabId", "DeletedBy", "Role" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LabSuperAdmins_Id_UserId",
                table: "LabSuperAdmins");

            migrationBuilder.DropIndex(
                name: "IX_LabSuperAdmins_LabId_IsMain",
                table: "LabSuperAdmins");

            migrationBuilder.DropIndex(
                name: "IX_LabSuperAdmins_UserId_LabId_IsMain",
                table: "LabSuperAdmins");

            migrationBuilder.DropIndex(
                name: "IX_LabSignup_Email",
                table: "LabSignups");

            migrationBuilder.DropIndex(
                name: "IX_LabSignup_Id",
                table: "LabSignups");

            migrationBuilder.DropIndex(
                name: "IX_LabSignups_Id_Email_DeletedBy",
                table: "LabSignups");

            migrationBuilder.DropIndex(
                name: "IX_LabSignups_Id_Reference",
                table: "LabSignups");

            migrationBuilder.DropIndex(
                name: "IX_LabSignups_LabReference_DeletedBy",
                table: "LabSignups");

            migrationBuilder.DropIndex(
                name: "IX_LabSignups_PhoneNumber",
                table: "LabSignups");

            migrationBuilder.DropIndex(
                name: "IX_LabSignups_Pincode",
                table: "LabSignups");

            migrationBuilder.DropIndex(
                name: "IX_LabOtpEntries_Email_CreatedAt",
                table: "LabOtpEntries");

            migrationBuilder.DropIndex(
                name: "IX_LabOtpEntries_Email_CreatedAt_ExpiryTime",
                table: "LabOtpEntries");

            migrationBuilder.DropIndex(
                name: "IX_LabOtpEntry_Email",
                table: "LabOtpEntries");

            migrationBuilder.DropIndex(
                name: "IX_LabMembers_Id_LabId_DeletedBy",
                table: "LabMembers");

            migrationBuilder.DropIndex(
                name: "IX_LabMembers_LabId_DeletedBy",
                table: "LabMembers");

            migrationBuilder.DropIndex(
                name: "IX_LabMembers_UserId_LabId_DeletedBy",
                table: "LabMembers");

            migrationBuilder.DropIndex(
                name: "IX_LabMembers_UserId_LabId_DeletedBy_Role",
                table: "LabMembers");

            migrationBuilder.AlterColumn<string>(
                name: "Pincode",
                table: "LabSignups",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: "LabSignups",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "LabSignups",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "LabOtpEntries",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(255)",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "LabMembers",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<long>(
                name: "Timestamp",
                table: "LabAuditLogs",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

          
        }
    }
}
