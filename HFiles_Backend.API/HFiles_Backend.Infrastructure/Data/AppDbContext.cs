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





            modelBuilder.Entity<User>().ToTable("users", t => t.ExcludeFromMigrations());


        }

        public DbSet<UserReports> UserReports { get; set; }
        public DbSet<UserDetails> UserDetails { get; set; }
        public DbSet<LabUserReports> LabUserReports { get; set; }
        public DbSet<LabSuperAdmin> LabSuperAdmins { get; set; }
        public DbSet<LabMember> LabMembers { get; set; }
        public DbSet<LabResendReports> LabResendReports { get; set; }
        public DbSet<LabAuditLog> LabAuditLogs { get; set; }
        public DbSet<LabErrorLog> LabErrorLogs { get; set; }
        public DbSet<User> Users { get; set; }
    }
}
