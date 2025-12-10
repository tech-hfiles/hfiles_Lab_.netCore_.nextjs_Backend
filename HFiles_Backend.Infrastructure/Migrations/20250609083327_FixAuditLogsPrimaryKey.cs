using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HFilesBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixAuditLogsPrimaryKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
      name: "Id",
      table: "labauditlogs",
      nullable: false)
  .Annotation("SqlServer:Identity", "1, 1"); 
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
       name: "Id",
       table: "labauditlogs",
       nullable: false,
       oldClrType: typeof(int))
   .OldAnnotation("SqlServer:Identity", "1, 1"); 
        }
    }
}
