using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Entities.Labs;
using HFiles_Backend.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;

namespace HFiles_Backend.Infrastructure.Data
{
    public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        public DbSet<LabSignup> LabSignups { get; set; }
        public DbSet<LabOtpEntry> LabOtpEntries { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<LabSignup>()
              .HasIndex(l => l.Id)
              .HasDatabaseName("IX_LabSignup_Id");

            modelBuilder.Entity<LabSignup>()
                .HasIndex(l => l.HFID)
                .IsUnique();

            modelBuilder.Entity<LabSignup>()
                .HasIndex(l => l.Email)
                .IsUnique()
                .HasDatabaseName("IX_LabSignup_Email");

            modelBuilder.Entity<LabSignup>()
                .HasIndex(l => l.PhoneNumber)
                .HasDatabaseName("IX_LabSignups_PhoneNumber");

            modelBuilder.Entity<LabSignup>()
                .HasIndex(l => new { l.Id, l.Email, l.DeletedBy })
                .HasDatabaseName("IX_LabSignups_Id_Email_DeletedBy");

            modelBuilder.Entity<LabSignup>()
                .HasIndex(l => new { l.Id, l.LabReference })
                .HasDatabaseName("IX_LabSignups_Id_Reference");

            modelBuilder.Entity<LabSignup>()
                .HasIndex(l => l.Pincode)
                .HasDatabaseName("IX_LabSignups_Pincode");

            modelBuilder.Entity<LabSignup>()
                .HasIndex(l => new { l.LabReference, l.DeletedBy })
                .HasDatabaseName("IX_LabSignups_LabReference_DeletedBy");



            modelBuilder.Entity<LabOtpEntry>()
                .HasIndex(otp => otp.Email)
                .HasDatabaseName("IX_LabOtpEntry_Email");

            modelBuilder.Entity<LabOtpEntry>()
                .HasIndex(x => new { x.Email, x.CreatedAt })
                .HasDatabaseName("IX_LabOtpEntries_Email_CreatedAt");

            modelBuilder.Entity<LabOtpEntry>()
                .HasIndex(x => new { x.Email, x.CreatedAt, x.ExpiryTime })
                .HasDatabaseName("IX_LabOtpEntries_Email_CreatedAt_ExpiryTime");





            modelBuilder.Entity<LabSuperAdmin>()
                .ToTable("LabSuperAdmins")
                .HasIndex(a => new { a.UserId, a.LabId, a.IsMain })
                .HasDatabaseName("IX_LabSuperAdmins_UserId_LabId_IsMain");

            modelBuilder.Entity<LabSuperAdmin>()
                .HasIndex(a => new { a.LabId, a.IsMain })
                .HasDatabaseName("IX_LabSuperAdmins_LabId_IsMain");

            modelBuilder.Entity<LabSuperAdmin>()
                .HasIndex(a => new { a.Id, a.UserId })
                .HasDatabaseName("IX_LabSuperAdmins_Id_UserId");





            modelBuilder.Entity<LabMember>()
                .ToTable("LabMembers")
                .HasIndex(m => new { m.UserId, m.LabId, m.DeletedBy })
                .HasDatabaseName("IX_LabMembers_UserId_LabId_DeletedBy");

            modelBuilder.Entity<LabMember>()
                .HasIndex(m => new { m.UserId, m.LabId, m.DeletedBy, m.Role })
                .HasDatabaseName("IX_LabMembers_UserId_LabId_DeletedBy_Role");

            modelBuilder.Entity<LabMember>()
                .HasIndex(m => new { m.LabId, m.DeletedBy })
                .HasDatabaseName("IX_LabMembers_LabId_DeletedBy");

            modelBuilder.Entity<LabMember>()
                .HasIndex(m => new { m.Id, m.LabId, m.DeletedBy })
                .HasDatabaseName("IX_LabMembers_Id_LabId_DeletedBy");




            // Labs
            modelBuilder.Entity<LabSignup>().ToTable("labsignups", t => t.ExcludeFromMigrations());
            modelBuilder.Entity<LabOtpEntry>().ToTable("labotpentries", t => t.ExcludeFromMigrations());
            modelBuilder.Entity<LabUserReports>().ToTable("labuserreports", t => t.ExcludeFromMigrations());
            modelBuilder.Entity<LabSuperAdmin>().ToTable("labsuperadmins", t => t.ExcludeFromMigrations());
            modelBuilder.Entity<LabMember>().ToTable("labmembers", t => t.ExcludeFromMigrations());
            modelBuilder.Entity<LabResendReports>().ToTable("labresendreports", t => t.ExcludeFromMigrations());
            //modelBuilder.Entity<LabAuditLog>().ToTable("labauditlogs", t => t.ExcludeFromMigrations());
            modelBuilder.Entity<LabErrorLog>().ToTable("laberrorlogs", t => t.ExcludeFromMigrations());




            // Users
            modelBuilder.Entity<User>().ToTable("users", t => t.ExcludeFromMigrations());
            modelBuilder.Entity<UserReport>().ToTable("userreports", t => t.ExcludeFromMigrations());
            modelBuilder.Entity<UserSurgeryDetails>().ToTable("user_surgery_details", t => t.ExcludeFromMigrations());
            modelBuilder.Entity<UserStaticAllergy>().ToTable("userstaticallergies", t => t.ExcludeFromMigrations());
            modelBuilder.Entity<UserMedicationAllergy>().ToTable("usermedicationallergies", t => t.ExcludeFromMigrations());
            modelBuilder.Entity<UserDynamicAllergy>().ToTable("userdynamicallergies", t => t.ExcludeFromMigrations());
            modelBuilder.Entity<UserStaticDisease>().ToTable("userstaticdiseases", t => t.ExcludeFromMigrations());
            modelBuilder.Entity<UserDynamicDiseaseType>().ToTable("userdynamicdiseasetypes", t => t.ExcludeFromMigrations());
            modelBuilder.Entity<UserDynamicDiseaseRecord>().ToTable("userdynamicdiseaserecords", t => t.ExcludeFromMigrations());
            modelBuilder.Entity<UserSocialHistory>().ToTable("usersocialhistories", t => t.ExcludeFromMigrations());
            modelBuilder.Entity<UserNotification>().ToTable("usernotifications", t => t.ExcludeFromMigrations());
            modelBuilder.Entity<UserSubscription>().ToTable("usersubscriptions", t => t.ExcludeFromMigrations());
            modelBuilder.Entity<CountryLists2>().ToTable("countrylist2", t => t.ExcludeFromMigrations());




            // Clinics
            modelBuilder.Entity<ClinicSignup>().ToTable("clinicsignups");   
            modelBuilder.Entity<ClinicOtpEntry>().ToTable("clinicotpentries");
            modelBuilder.Entity<ClinicSuperAdmin>().ToTable("clinicsuperadmins");
            modelBuilder.Entity<ClinicMember>().ToTable("clinicmembers");
            modelBuilder.Entity<ClinicAppointment>().ToTable("clinicappointments");
            modelBuilder.Entity<ClinicConsentForm>().ToTable("clinicconsentforms");
            modelBuilder.Entity<ClinicPatient>().ToTable("clinicpatients");
            modelBuilder.Entity<ClinicVisit>().ToTable("clinicvisits");
            modelBuilder.Entity<ClinicVisitConsentForm>().ToTable("clinicvisitconsentforms");
            modelBuilder.Entity<ClinicPrescription>().ToTable("clinicprescriptions");
            modelBuilder.Entity<ClinicTreatment>().ToTable("clinictreatments");
            modelBuilder.Entity<ClinicPatientRecord>().ToTable("clinicpatientrecords");



            // Clinics
            modelBuilder.Entity<ClinicSignup>()
                .HasIndex(c => c.Id)
                .HasDatabaseName("IX_ClinicSignup_Id");

            modelBuilder.Entity<ClinicSignup>()
                .HasIndex(c => c.HFID)
                .IsUnique()
                .HasDatabaseName("IX_ClinicSignup_HFID");

            modelBuilder.Entity<ClinicSignup>()
                .HasIndex(c => c.Email)
                .IsUnique()
                .HasDatabaseName("IX_ClinicSignup_Email");

            modelBuilder.Entity<ClinicSignup>()
                .HasIndex(c => c.PhoneNumber)
                .HasDatabaseName("IX_ClinicSignup_PhoneNumber");

            modelBuilder.Entity<ClinicSignup>()
                .HasIndex(c => new { c.Id, c.Email, c.DeletedBy })
                .HasDatabaseName("IX_ClinicSignup_Id_Email_DeletedBy");

            modelBuilder.Entity<ClinicSignup>()
                .HasIndex(c => new { c.Id, c.ClinicReference })
                .HasDatabaseName("IX_ClinicSignup_Id_Reference");

            modelBuilder.Entity<ClinicSignup>()
                .HasIndex(c => c.Pincode)
                .HasDatabaseName("IX_ClinicSignup_Pincode");

            modelBuilder.Entity<ClinicSignup>()
                .HasIndex(c => new { c.ClinicReference, c.DeletedBy })
                .HasDatabaseName("IX_ClinicSignup_ClinicReference_DeletedBy");





            modelBuilder.Entity<ClinicOtpEntry>()
                .HasIndex(o => o.Email)
                .HasDatabaseName("IX_ClinicOtpEntry_Email");

            modelBuilder.Entity<ClinicOtpEntry>()
                .HasIndex(o => new { o.Email, o.CreatedAt })
                .HasDatabaseName("IX_ClinicOtpEntry_Email_CreatedAt");

            modelBuilder.Entity<ClinicOtpEntry>()
                .HasIndex(o => new { o.Email, o.CreatedAt, o.ExpiryTime })
                .HasDatabaseName("IX_ClinicOtpEntry_Email_CreatedAt_ExpiryTime");





            modelBuilder.Entity<ClinicSuperAdmin>()
                .ToTable("clinicsuperadmins")
                .HasIndex(a => new { a.UserId, a.ClinicId, a.IsMain })
                .HasDatabaseName("IX_ClinicSuperAdmins_UserId_ClinicId_IsMain");

            modelBuilder.Entity<ClinicSuperAdmin>()
                .HasIndex(a => new { a.ClinicId, a.IsMain })
                .HasDatabaseName("IX_ClinicSuperAdmins_ClinicId_IsMain");

            modelBuilder.Entity<ClinicSuperAdmin>()
                .HasIndex(a => new { a.Id, a.UserId })
                .HasDatabaseName("IX_ClinicSuperAdmins_Id_UserId");

            modelBuilder.Entity<ClinicSuperAdmin>()
                .ToTable("clinicsuperadmins")
                .HasOne(csa => csa.User)
                .WithMany()
                .HasForeignKey(csa => csa.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ClinicSuperAdmin>()
                .HasOne(csa => csa.Clinic)
                .WithMany()
                .HasForeignKey(csa => csa.ClinicId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ClinicSuperAdmin>()
                .ToTable("clinicsuperadmins")
                .HasOne(csa => csa.Clinic)
                .WithMany()
                .HasForeignKey(csa => csa.ClinicId)
                .OnDelete(DeleteBehavior.Restrict);





            modelBuilder.Entity<ClinicMember>()
              .ToTable("clinicmembers")
              .HasIndex(m => new { m.UserId, m.ClinicId, m.DeletedBy })
              .HasDatabaseName("IX_ClinicMembers_UserId_ClinicId_DeletedBy");

            modelBuilder.Entity<ClinicMember>()
                .HasIndex(m => new { m.UserId, m.ClinicId, m.DeletedBy, m.Role })
                .HasDatabaseName("IX_ClinicMembers_UserId_ClinicId_DeletedBy_Role");

            modelBuilder.Entity<ClinicMember>()
                .HasIndex(m => new { m.ClinicId, m.DeletedBy })
                .HasDatabaseName("IX_ClinicMembers_ClinicId_DeletedBy");

            modelBuilder.Entity<ClinicMember>()
                .HasIndex(m => new { m.Id, m.ClinicId, m.DeletedBy })
                .HasDatabaseName("IX_ClinicMembers_Id_ClinicId_DeletedBy");

            modelBuilder.Entity<ClinicMember>()
                .HasOne(m => m.User)
                .WithMany()
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ClinicMember>()
                .HasOne(m => m.Clinic)
                .WithMany()
                .HasForeignKey(m => m.ClinicId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ClinicMember>()
                .HasOne(m => m.CreatedByUser)
                .WithMany()
                .HasForeignKey(m => m.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ClinicMember>()
                .HasOne(m => m.PromotedByUser)
                .WithMany()
                .HasForeignKey(m => m.PromotedBy)
                .OnDelete(DeleteBehavior.Restrict);





            // ClinicPatients: Index on HFID
            modelBuilder.Entity<ClinicPatient>()
                .HasIndex(p => p.HFID)
                .HasDatabaseName("IX_ClinicPatients_HFID");






            // ClinicVisits: Index on ClinicPatientId
            modelBuilder.Entity<ClinicVisit>()
                .HasIndex(v => v.ClinicPatientId)
                .HasDatabaseName("IX_ClinicVisits_ClinicPatientId");

            // ClinicVisits: Index on AppointmentDate
            modelBuilder.Entity<ClinicVisit>()
                .HasIndex(v => v.AppointmentDate)
                .HasDatabaseName("IX_ClinicVisits_AppointmentDate");

            modelBuilder.Entity<ClinicVisit>()
              .HasIndex(v => new { v.AppointmentDate, v.ClinicPatientId })
              .HasDatabaseName("IX_ClinicVisits_Date_Patient");






            // ClinicVisitConsentForm: Index on ClinicVisitId
            modelBuilder.Entity<ClinicVisitConsentForm>()
                .HasIndex(c => c.ClinicVisitId)
                .HasDatabaseName("IX_ClinicVisitConsentForm_ClinicVisitId");

            // ClinicVisitConsentForm: Index on ConsentFormId
            modelBuilder.Entity<ClinicVisitConsentForm>()
                .HasIndex(c => c.ConsentFormId)
                .HasDatabaseName("IX_ClinicVisitConsentForm_ConsentFormId");





            modelBuilder.Entity<ClinicAppointment>()
            .Property(a => a.Treatment)
            .HasMaxLength(1000)
            .IsRequired(false);

            // Index: Status + AppointmentDate
            modelBuilder.Entity<ClinicAppointment>()
                .HasIndex(a => new { a.Status, a.AppointmentDate })
                .HasDatabaseName("idx_clinicappointments_status_date");

            // Index: AppointmentDate + AppointmentTime
            modelBuilder.Entity<ClinicAppointment>()
                .HasIndex(a => new { a.AppointmentDate, a.AppointmentTime })
                .HasDatabaseName("idx_clinicappointments_date_time");





            // Configure ClinicGoogleToken
            modelBuilder.Entity<ClinicGoogleToken>(entity =>
            {
                entity.ToTable("clinic_google_tokens");

                entity.HasKey(e => e.Id);

                entity.Property(e => e.CalendarId)
                    .HasMaxLength(255)
                    .HasDefaultValue("primary");

                entity.Property(e => e.AccessToken)
                    .IsRequired();

                entity.Property(e => e.RefreshToken)
                    .IsRequired();

                entity.Property(e => e.TokenExpiry)
                    .IsRequired();

                entity.Property(e => e.Scope)
                    .HasMaxLength(500)
                    .HasDefaultValue("https://www.googleapis.com/auth/calendar");

                entity.Property(e => e.TokenType)
                    .HasMaxLength(50)
                    .HasDefaultValue("Bearer");

                entity.Property(e => e.IsActive)
                    .HasDefaultValue(true);

                entity.Property(e => e.CreatedAt)
                    .HasDefaultValueSql("GETUTCDATE()");

                entity.Property(e => e.UpdatedAt)
                    .HasDefaultValueSql("GETUTCDATE()");

                // Foreign key relationship
                entity.HasOne(e => e.Clinic)
                    .WithMany()
                    .HasForeignKey(e => e.ClinicId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Indexes
                entity.HasIndex(e => e.ClinicId);
                entity.HasIndex(e => new { e.ClinicId, e.IsActive });
                entity.HasIndex(e => new { e.IsActive, e.TokenExpiry });
            });
        }

        //public DbSet<UserReports> UserReports { get; set; }
        //public DbSet<UserDetails> UserDetails { get; set; }
        public DbSet<LabUserReports> LabUserReports { get; set; }
        public DbSet<LabSuperAdmin> LabSuperAdmins { get; set; }
        public DbSet<LabMember> LabMembers { get; set; }
        public DbSet<LabResendReports> LabResendReports { get; set; }
        public DbSet<LabAuditLog> LabAuditLogs { get; set; }
        public DbSet<LabErrorLog> LabErrorLogs { get; set; }


        // Users
        public DbSet<User> Users { get; set; }
        public DbSet <UserReport> UserReports { get; set; }
        public DbSet<UserSurgeryDetails> UserSurgeryDetails { get; set; }
        public DbSet<UserStaticAllergy> UserStaticAllergies { get; set; }
        public DbSet<UserMedicationAllergy> UserMedicationAllergies { get; set; }
        public DbSet<UserDynamicAllergy> UserDynamicAllergies { get; set; }
        public DbSet<UserStaticDisease> UserStaticDiseases { get; set; }
        public DbSet<UserDynamicDiseaseRecord> UserDynamicDiseaseRecords { get; set; }
        public DbSet<UserDynamicDiseaseType> UserDynamicDiseaseTypes { get; set; }
        public DbSet<UserSocialHistory> UserSocialHistories { get; set; }
        public DbSet<UserNotification> UserNotifications { get; set; }
        public DbSet<UserSubscription> UserSubscriptions { get; set; }
        public DbSet<CountryLists2> countrylist2 { get; set; }



        // Clinics
        public DbSet<ClinicSignup> ClinicSignups { get; set; }
        public DbSet<ClinicOtpEntry> ClinicOtpEntries { get; set; }
        public DbSet<ClinicSuperAdmin> ClinicSuperAdmins { get; set; }
        public DbSet<ClinicMember> ClinicMembers { get; set; }
        public DbSet<ClinicAppointment> ClinicAppointments { get; set; }
        public DbSet<ClinicConsentForm> ClinicConsentForms { get; set; }
        public DbSet <ClinicPatient> ClinicPatients { get; set; }
        public DbSet <ClinicVisit> ClinicVisits { get; set; }
        public DbSet <ClinicVisitConsentForm> ClinicVisitConsentForms { get; set; }
        public DbSet <ClinicPrescription> ClinicPrescriptions { get; set; }
        public DbSet<ClinicTreatment> ClinicTreatments { get; set; }
        public DbSet<ClinicPatientRecord> ClinicPatientRecords { get; set; }
        public DbSet<BlacklistedToken> BlacklistedTokens { get; set; }
        public DbSet<ClinicPatientMedicalHistory> ClinicPatientMedicalHistories { get; set; }
        public DbSet<ClinicRecordCounter> ClinicRecordCounters { get; set; }
        //public DbSet<NotificationAuditLog> NotificationAuditLogs { get; set; }
        public DbSet<ClinicGoogleToken> ClinicGoogleTokens { get; set; }

        public DbSet<ClinicPrescriptionNotes> clinicPrescriptionNotes { get; set; }
        public DbSet<Hifi5PricingPackage> hifi5PricingPackages { get; set; }
        public DbSet<ClinicMemberReport> clinicMemberReports { get; set; }

        public DbSet<ClinicMemberRecord> clinicMemberRecords { get; set; }
    }
}
