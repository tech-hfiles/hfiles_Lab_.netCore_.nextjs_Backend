using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HFilesBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClinicTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            //migrationBuilder.DropPrimaryKey(
            //    name: "PK_LabSuperAdmins",
            //    table: "LabSuperAdmins");

            //migrationBuilder.DropPrimaryKey(
            //    name: "PK_LabMembers",
            //    table: "LabMembers");

            //migrationBuilder.RenameTable(
            //    name: "LabSuperAdmins",
            //    newName: "labsuperadmins");

            //migrationBuilder.RenameTable(
            //    name: "LabMembers",
            //    newName: "labmembers");

            //migrationBuilder.AddPrimaryKey(
            //    name: "PK_labsuperadmins",
            //    table: "labsuperadmins",
            //    column: "Id");

            //migrationBuilder.AddPrimaryKey(
            //    name: "PK_labmembers",
            //    table: "labmembers",
            //    column: "Id");

            migrationBuilder.CreateTable(
                name: "clinicconsentforms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Title = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clinicconsentforms", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "clinicotpentries",
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
                    table.PrimaryKey("PK_clinicotpentries", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "clinicpatients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    HFID = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PatientName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clinicpatients", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "clinicsignups",
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
                    table.PrimaryKey("PK_clinicsignups", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "clinicappointments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    VisitorUsername = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    VisitorPhoneNumber = table.Column<string>(type: "varchar(15)", maxLength: 15, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AppointmentDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AppointmentTime = table.Column<TimeSpan>(type: "time(6)", nullable: false),
                    Treatment = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ClinicId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clinicappointments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_clinicappointments_clinicsignups_ClinicId",
                        column: x => x.ClinicId,
                        principalTable: "clinicsignups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

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
                        name: "FK_ClinicMembers_clinicsignups_ClinicId",
                        column: x => x.ClinicId,
                        principalTable: "clinicsignups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClinicMembers_users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClinicMembers_users_DeletedBy",
                        column: x => x.DeletedBy,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClinicMembers_users_PromotedBy",
                        column: x => x.PromotedBy,
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

            migrationBuilder.CreateTable(
                name: "clinicprescriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ClinicId = table.Column<int>(type: "int", nullable: false),
                    MedicationName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MedicationDosage = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Frequency = table.Column<int>(type: "int", nullable: false),
                    Timing = table.Column<int>(type: "int", nullable: false),
                    Instructions = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clinicprescriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_clinicprescriptions_clinicsignups_ClinicId",
                        column: x => x.ClinicId,
                        principalTable: "clinicsignups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

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
                        name: "FK_ClinicSuperAdmins_clinicsignups_ClinicId",
                        column: x => x.ClinicId,
                        principalTable: "clinicsignups",
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

            migrationBuilder.CreateTable(
                name: "clinictreatments",
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
                    Status = table.Column<int>(type: "int", nullable: false),
                    Duration = table.Column<int>(type: "int", nullable: true),
                    Sessions = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clinictreatments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_clinictreatments_clinicsignups_ClinicId",
                        column: x => x.ClinicId,
                        principalTable: "clinicsignups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "clinicvisits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ClinicPatientId = table.Column<int>(type: "int", nullable: false),
                    ClinicId = table.Column<int>(type: "int", nullable: false),
                    AppointmentDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AppointmentTime = table.Column<TimeSpan>(type: "time(6)", nullable: false),
                    PaymentMethod = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clinicvisits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_clinicvisits_clinicpatients_ClinicPatientId",
                        column: x => x.ClinicPatientId,
                        principalTable: "clinicpatients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_clinicvisits_clinicsignups_ClinicId",
                        column: x => x.ClinicId,
                        principalTable: "clinicsignups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "clinicpatientrecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ClinicId = table.Column<int>(type: "int", nullable: false),
                    PatientId = table.Column<int>(type: "int", nullable: false),
                    ClinicVisitId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    JsonData = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SendToPatient = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clinicpatientrecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_clinicpatientrecords_clinicpatients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "clinicpatients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_clinicpatientrecords_clinicsignups_ClinicId",
                        column: x => x.ClinicId,
                        principalTable: "clinicsignups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_clinicpatientrecords_clinicvisits_ClinicVisitId",
                        column: x => x.ClinicVisitId,
                        principalTable: "clinicvisits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "clinicvisitconsentforms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ClinicVisitId = table.Column<int>(type: "int", nullable: false),
                    ConsentFormId = table.Column<int>(type: "int", nullable: false),
                    ConsentFormUrl = table.Column<string>(type: "varchar(2048)", maxLength: 2048, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsVerified = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clinicvisitconsentforms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_clinicvisitconsentforms_clinicconsentforms_ConsentFormId",
                        column: x => x.ConsentFormId,
                        principalTable: "clinicconsentforms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_clinicvisitconsentforms_clinicvisits_ClinicVisitId",
                        column: x => x.ClinicVisitId,
                        principalTable: "clinicvisits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_clinicappointments_ClinicId",
                table: "clinicappointments",
                column: "ClinicId");

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

            migrationBuilder.CreateIndex(
                name: "IX_ClinicOtpEntry_Email",
                table: "clinicotpentries",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicOtpEntry_Email_CreatedAt",
                table: "clinicotpentries",
                columns: new[] { "Email", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicOtpEntry_Email_CreatedAt_ExpiryTime",
                table: "clinicotpentries",
                columns: new[] { "Email", "CreatedAt", "ExpiryTime" });

            migrationBuilder.CreateIndex(
                name: "IX_clinicpatientrecords_ClinicId",
                table: "clinicpatientrecords",
                column: "ClinicId");

            migrationBuilder.CreateIndex(
                name: "IX_clinicpatientrecords_ClinicVisitId",
                table: "clinicpatientrecords",
                column: "ClinicVisitId");

            migrationBuilder.CreateIndex(
                name: "IX_clinicpatientrecords_PatientId",
                table: "clinicpatientrecords",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicPatients_HFID",
                table: "clinicpatients",
                column: "HFID");

            migrationBuilder.CreateIndex(
                name: "IX_clinicprescriptions_ClinicId",
                table: "clinicprescriptions",
                column: "ClinicId");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicSignup_ClinicReference_DeletedBy",
                table: "clinicsignups",
                columns: new[] { "ClinicReference", "DeletedBy" });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicSignup_Email",
                table: "clinicsignups",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClinicSignup_HFID",
                table: "clinicsignups",
                column: "HFID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClinicSignup_Id",
                table: "clinicsignups",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicSignup_Id_Email_DeletedBy",
                table: "clinicsignups",
                columns: new[] { "Id", "Email", "DeletedBy" });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicSignup_Id_Reference",
                table: "clinicsignups",
                columns: new[] { "Id", "ClinicReference" });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicSignup_PhoneNumber",
                table: "clinicsignups",
                column: "PhoneNumber");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicSignup_Pincode",
                table: "clinicsignups",
                column: "Pincode");

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

            migrationBuilder.CreateIndex(
                name: "IX_clinictreatments_ClinicId",
                table: "clinictreatments",
                column: "ClinicId");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicVisitConsentForm_ClinicVisitId",
                table: "clinicvisitconsentforms",
                column: "ClinicVisitId");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicVisitConsentForm_ConsentFormId",
                table: "clinicvisitconsentforms",
                column: "ConsentFormId");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicVisits_AppointmentDate",
                table: "clinicvisits",
                column: "AppointmentDate");

            migrationBuilder.CreateIndex(
                name: "IX_clinicvisits_ClinicId",
                table: "clinicvisits",
                column: "ClinicId");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicVisits_ClinicPatientId",
                table: "clinicvisits",
                column: "ClinicPatientId");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicVisits_Date_Patient",
                table: "clinicvisits",
                columns: new[] { "AppointmentDate", "ClinicPatientId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "clinicappointments");

            migrationBuilder.DropTable(
                name: "ClinicMembers");

            migrationBuilder.DropTable(
                name: "clinicotpentries");

            migrationBuilder.DropTable(
                name: "clinicpatientrecords");

            migrationBuilder.DropTable(
                name: "clinicprescriptions");

            migrationBuilder.DropTable(
                name: "ClinicSuperAdmins");

            migrationBuilder.DropTable(
                name: "clinictreatments");

            migrationBuilder.DropTable(
                name: "clinicvisitconsentforms");

            migrationBuilder.DropTable(
                name: "clinicconsentforms");

            migrationBuilder.DropTable(
                name: "clinicvisits");

            migrationBuilder.DropTable(
                name: "clinicpatients");

            migrationBuilder.DropTable(
                name: "clinicsignups");

            //migrationBuilder.DropPrimaryKey(
            //    name: "PK_labsuperadmins",
            //    table: "labsuperadmins");

            //migrationBuilder.DropPrimaryKey(
            //    name: "PK_labmembers",
            //    table: "labmembers");

            //migrationBuilder.RenameTable(
            //    name: "labsuperadmins",
            //    newName: "LabSuperAdmins");

            //migrationBuilder.RenameTable(
            //    name: "labmembers",
            //    newName: "LabMembers");

            //migrationBuilder.AddPrimaryKey(
            //    name: "PK_LabSuperAdmins",
            //    table: "LabSuperAdmins",
            //    column: "Id");

            //migrationBuilder.AddPrimaryKey(
            //    name: "PK_LabMembers",
            //    table: "LabMembers",
            //    column: "Id");
        }
    }
}
