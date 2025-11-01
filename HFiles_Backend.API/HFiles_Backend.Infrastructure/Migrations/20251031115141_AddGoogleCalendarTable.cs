using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HFilesBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGoogleCalendarTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "clinic_google_tokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ClinicId = table.Column<int>(type: "int", nullable: false),
                    CalendarId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false, defaultValue: "primary")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AccessToken = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RefreshToken = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TokenExpiry = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Scope = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false, defaultValue: "https://www.googleapis.com/auth/calendar")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TokenType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, defaultValue: "Bearer")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    LastRefreshedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)")


                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clinic_google_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_clinic_google_tokens_clinicsignups_ClinicId",
                        column: x => x.ClinicId,
                        principalTable: "clinicsignups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_clinic_google_tokens_ClinicId",
                table: "clinic_google_tokens",
                column: "ClinicId");

            migrationBuilder.CreateIndex(
                name: "IX_clinic_google_tokens_ClinicId_IsActive",
                table: "clinic_google_tokens",
                columns: new[] { "ClinicId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_clinic_google_tokens_IsActive_TokenExpiry",
                table: "clinic_google_tokens",
                columns: new[] { "IsActive", "TokenExpiry" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "clinic_google_tokens");
        }
    }
}
