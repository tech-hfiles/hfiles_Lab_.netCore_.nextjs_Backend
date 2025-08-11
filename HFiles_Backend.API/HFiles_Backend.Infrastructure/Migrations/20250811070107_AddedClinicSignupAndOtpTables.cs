using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HFilesBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddedClinicSignupAndOtpTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClinicOtpEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Email = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OtpCode = table.Column<string>(type: "varchar(6)", maxLength: 6, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ExpiryTime = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicOtpEntries", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ClinicSignups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ClinicName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Email = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    HFID = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PhoneNumber = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Address = table.Column<string>(type: "varchar(250)", maxLength: 250, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Pincode = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProfilePhoto = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PasswordHash = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsSuperAdmin = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ClinicReference = table.Column<int>(type: "int", nullable: false),
                    DeletedBy = table.Column<int>(type: "int", nullable: false),
                    CreatedAtEpoch = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicSignups", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicOtpEntry_Email",
                table: "ClinicOtpEntries",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicOtpEntry_Email_CreatedAt",
                table: "ClinicOtpEntries",
                columns: new[] { "Email", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicOtpEntry_Email_CreatedAt_ExpiryTime",
                table: "ClinicOtpEntries",
                columns: new[] { "Email", "CreatedAt", "ExpiryTime" });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicSignup_ClinicReference_DeletedBy",
                table: "ClinicSignups",
                columns: new[] { "ClinicReference", "DeletedBy" });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicSignup_Email",
                table: "ClinicSignups",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClinicSignup_HFID",
                table: "ClinicSignups",
                column: "HFID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClinicSignup_Id",
                table: "ClinicSignups",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicSignup_Id_Email_DeletedBy",
                table: "ClinicSignups",
                columns: new[] { "Id", "Email", "DeletedBy" });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicSignup_Id_Reference",
                table: "ClinicSignups",
                columns: new[] { "Id", "ClinicReference" });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicSignup_PhoneNumber",
                table: "ClinicSignups",
                column: "PhoneNumber");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicSignup_Pincode",
                table: "ClinicSignups",
                column: "Pincode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClinicOtpEntries");

            migrationBuilder.DropTable(
                name: "ClinicSignups");
        }
    }
}
