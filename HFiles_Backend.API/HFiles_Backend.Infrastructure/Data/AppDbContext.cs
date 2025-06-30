using HFiles_Backend.Domain.Entities.Labs;
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
                .HasIndex(u => u.HFID)
                .IsUnique();

            modelBuilder.Entity<UserDetails>().HasNoKey();
        }

        public DbSet<UserReports> UserReports { get; set; }
        public DbSet<UserDetails> UserDetails { get; set; }
        public DbSet<LabUserReports> LabUserReports { get; set; }
        public DbSet<LabSuperAdmin> LabSuperAdmins { get; set; }
        public DbSet<LabMember> LabMembers { get; set; }
        public DbSet<LabResendReports> LabResendReports { get; set; }
        public DbSet<LabAuditLog> LabAuditLogs { get; set; }
        public DbSet<LabErrorLog> LabErrorLogs { get; set; }
    }
}
