using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HFilesBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserReportsandAddLabUserReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LabUserReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EpochTime = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabUserReports", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

          

            migrationBuilder.AddColumn<string>(
            name: "UploadedBy",
            table: "user_reports",
            type: "longtext",
            nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LabUserReports");

            migrationBuilder.DropTable(
                name: "user_details");

            migrationBuilder.DropColumn(
                name: "UploadedBy",
                table: "user_reports");

        }
    }
}
