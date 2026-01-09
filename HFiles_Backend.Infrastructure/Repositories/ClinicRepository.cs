using HFiles_Backend.Application.DTOs.Clinics.HFID;
using HFiles_Backend.Application.DTOs.Clinics.Profile;
using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Entities.Users;
using HFiles_Backend.Domain.Interfaces;
using HFiles_Backend.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Security.Claims;


namespace HFiles_Backend.Infrastructure.Repositories
{
    public class ClinicRepository(AppDbContext context) : IClinicRepository
    {
        private readonly AppDbContext _context = context;

        public async Task<IDbContextTransaction> BeginTransactionAsync()
        {
            return await _context.Database.BeginTransactionAsync();
        }
        public async Task<bool> ExistsAsync(int clinicId)
        {
            return await _context.ClinicSignups
                .AsNoTracking()
                .AnyAsync(c => c.Id == clinicId && c.DeletedBy == 0);
        }

        public async Task<bool> EmailExistsAsync(string email)
        {
            return await _context.ClinicSignups
                .AsNoTracking()
                .AnyAsync(c => c.Email == email);
        }

        public async Task<ClinicSignup?> GetByEmailAsync(string email)
        {
            return await _context.ClinicSignups
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Email == email);
        }

        public async Task<ClinicSignup?> GetByPhoneAsync(string phoneNumber)
        {
            return await _context.ClinicSignups
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.PhoneNumber == phoneNumber);
        }

        public async Task<ClinicOtpEntry?> GetLatestOtpAsync(string emailOrPhone)
        {
            return await _context.ClinicOtpEntries
                .Where(o => o.Email == emailOrPhone)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<ClinicSignup?> GetByIdAndEmailAsync(int id, string email)
        {
            return await _context.ClinicSignups
                .Where(c => c.Id == id && c.Email == email)
                .FirstOrDefaultAsync();
        }

        public async Task<ClinicHFIDDto?> GetHFIDByEmailAsync(string email)
        {
            return await _context.ClinicSignups
                .Where(u => u.Email == email)
                .Select(u => new ClinicHFIDDto
                {
                    Email = u.Email,
                    ClinicName = u.ClinicName,
                    HFID = u.HFID,
                    ProfilePhoto = u.ProfilePhoto
                })
                .FirstOrDefaultAsync();
        }
        public async Task<ClinicSignup?> GetByIdAsync(int id)
        {
            return await _context.ClinicSignups
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<List<ClinicOtpEntry>> GetExpiredOtpsAsync(string identifier, DateTime now)
        {
            return await _context.ClinicOtpEntries
                .Where(x => x.Email == identifier && x.ExpiryTime < now)
                .ToListAsync();
        }

        public async Task RemoveExpiredOtpsAsync(string email, DateTime currentTime)
        {
            var expiredOtps = await _context.ClinicOtpEntries
                .Where(o => o.Email == email && o.ExpiryTime < currentTime)
                .ToListAsync();

            _context.ClinicOtpEntries.RemoveRange(expiredOtps);
        }

        public async Task RemoveOtpsAsync(IEnumerable<ClinicOtpEntry> otpEntries)
        {
            _context.ClinicOtpEntries.RemoveRange(otpEntries);
            await _context.SaveChangesAsync();
        }

        public async Task AddSignupAsync(ClinicSignup signup)
        {
            await _context.ClinicSignups.AddAsync(signup);
        }

        public async Task RemoveOtpAsync(ClinicOtpEntry otpEntry)
        {
            _context.ClinicOtpEntries.Remove(otpEntry);
            await _context.SaveChangesAsync();
        }

        public async Task AddOtpAsync(ClinicOtpEntry entry)
        {
            await _context.ClinicOtpEntries.AddAsync(entry);
        }

        public async Task UpdateAsync(ClinicSignup clinic)
        {
            var existing = await _context.ClinicSignups.FindAsync(clinic.Id);
            if (existing == null) return;

            _context.Entry(existing).CurrentValues.SetValues(clinic);
            await _context.SaveChangesAsync();
        }
        public async Task<List<int>> GetBranchIdsAsync(int mainClinicId)
        {
            return await _context.ClinicSignups
                .Where(c => c.ClinicReference == mainClinicId)
                .Select(c => c.Id)
                .ToListAsync();
        }
        public void Update(ClinicSignup clinic)
        {
            _context.ClinicSignups.Update(clinic);
        }


        public async Task<bool> IsClinicAuthorizedAsync(int clinicId, ClaimsPrincipal user)
        {
            var userIdClaim = user.FindFirst("UserId")?.Value;
            if (!int.TryParse(userIdClaim, out int userId)) return false;

            var clinic = await _context.ClinicSignups.FirstOrDefaultAsync(c => c.Id == clinicId);
            if (clinic == null) return false;

            int mainClinicId = clinic.ClinicReference == 0 ? clinicId : clinic.ClinicReference;

            var authorizedClinicIds = await _context.ClinicSignups
                .Where(c => c.ClinicReference == mainClinicId || c.Id == mainClinicId)
                .Select(c => c.Id)
                .ToListAsync();

            return authorizedClinicIds.Contains(clinicId);
        }

        public async Task<ClinicSignup?> GetClinicByIdAsync(int clinicId)
        {
            return await _context.ClinicSignups.FirstOrDefaultAsync(c => c.Id == clinicId);
        }

        public async Task<List<int>> GetBranchClinicIdsAsync(int mainClinicId)
        {
            return await _context.ClinicSignups
                .Where(c => c.ClinicReference == mainClinicId)
                .Select(c => c.Id)
                .ToListAsync();
        }

        public async Task<ClinicMember?> GetDeletedMemberAsync(int userId, int clinicId)
        {
            return await _context.ClinicMembers
                .FirstOrDefaultAsync(m => m.Id == userId && m.ClinicId == clinicId && m.DeletedBy != 0);
        }

        public async Task<ClinicSuperAdmin?> GetSuperAdminByIdAsync(int adminId)
        {
            return await _context.ClinicSuperAdmins.FirstOrDefaultAsync(a => a.Id == adminId);
        }

        public async Task<User?> GetUserByIdAsync(int userId)
        {
            return await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        }

        public async Task<string?> ResolveUsernameFromClaimsAsync(HttpContext context)
        {
            var userIdClaim = context.User.FindFirst("UserId")?.Value;
            if (!int.TryParse(userIdClaim, out int userId)) return null;

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return null;

            return $"{user.FirstName} {user.LastName}".Trim();
        }

        public void UpdateClinicMember(ClinicMember member)
        {
            _context.ClinicMembers.Update(member);
        }

        public async Task<ClinicSignup?> GetClinicByEmailAsync(string email)
        {
            return await _context.ClinicSignups.FirstOrDefaultAsync(c => c.Email == email);
        }

        public async Task<ClinicSignup?> GetMainClinicAsync(int clinicId)
        {
            var clinic = await _context.ClinicSignups.FirstOrDefaultAsync(c => c.Id == clinicId);
            if (clinic == null) return null;

            return clinic.ClinicReference == 0
                ? clinic
                : await _context.ClinicSignups.FirstOrDefaultAsync(c => c.Id == clinic.ClinicReference);
        }

        public void AddOtpEntry(ClinicOtpEntry entry)
        {
            _context.ClinicOtpEntries.Add(entry);
        }

        public async Task<ClinicOtpEntry?> GetLatestOtpEntryAsync(string email)
        {
            return await _context.ClinicOtpEntries
                .Where(o => o.Email == email)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public void RemoveOtpEntries(IEnumerable<ClinicOtpEntry> entries)
        {
            _context.ClinicOtpEntries.RemoveRange(entries);
        }

        public void RemoveOtpEntry(ClinicOtpEntry entry)
        {
            _context.ClinicOtpEntries.Remove(entry);
        }

        public void UpdateClinic(ClinicSignup clinic)
        {
            _context.ClinicSignups.Update(clinic);
        }
        public async Task<User?> GetVerifiedUserByEmailAsync(string email)
        {
            return await _context.Users.FirstOrDefaultAsync(u => u.Email == email && u.IsEmailVerified && u.UserReference == 0);
        }

        public async Task<ClinicSuperAdmin?> GetSuperAdminAsync(int userId, int clinicId)
        {
            return await _context.ClinicSuperAdmins
                .FirstOrDefaultAsync(sa => sa.UserId == userId && sa.ClinicId == clinicId && sa.IsMain == 1);
        }

        public async Task<ClinicMember?> GetClinicMemberAsync(int userId, int clinicId)
        {
            return await _context.ClinicMembers
                .FirstOrDefaultAsync(m => m.UserId == userId && m.ClinicId == clinicId && m.DeletedBy == 0);
        }

        public void UpdateSuperAdmin(ClinicSuperAdmin admin)
        {
            _context.ClinicSuperAdmins.Update(admin);
        }

        public async Task<List<ClinicPatient>> GetClinicPatientsWithVisitsAsync(int clinicId)
        {
            return await _context.ClinicPatients
                .Where(p => p.Visits.Any(v => v.ClinicId == clinicId))
                .Include(p => p.Visits.Where(v => v.ClinicId == clinicId))
                    .ThenInclude(v => v.ConsentFormsSent)
                        .ThenInclude(cf => cf.ConsentForm)
                .ToListAsync();
        }

        public async Task<ClinicPatient?> GetPatientByHFIDAsync(string hfid)
        => await _context.ClinicPatients.FirstOrDefaultAsync(p => p.HFID == hfid);

        public async Task<ClinicVisit?> GetVisitAsync(int clinicId, int patientId, DateTime visitDate)
            => await _context.ClinicVisits
                .Include(v => v.ConsentFormsSent)
                .ThenInclude(cf => cf.ConsentForm)
                .FirstOrDefaultAsync(v =>
                    v.ClinicId == clinicId &&
                    v.ClinicPatientId == patientId &&
                    v.AppointmentDate.Date == visitDate.Date);

        public async Task<List<ClinicVisitConsentForm>> GetConsentFormsForVisitAsync(int visitId)
            => await _context.ClinicVisitConsentForms
                .Include(cf => cf.ConsentForm)
                .Where(cf => cf.ClinicVisitId == visitId)
                .ToListAsync();

        public async Task<List<ClinicNotificationDto>> GetClinicNotificationsAsync(
        int clinicId, long epochStart, long epochEnd)
        {
            long currentEpoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            return await _context.LabAuditLogs
                .AsNoTracking()
                .Where(n => n.LabId == clinicId &&
                            n.Timestamp >= epochStart &&
                            n.Timestamp <= epochEnd &&
                            n.Notifications != "No notification message found.")
                .OrderByDescending(n => n.Timestamp)
                .Select(n => new ClinicNotificationDto
                {
                    Id = n.Id,
                    ClinicId = n.LabId,
                    UserRole = n.UserRole,
                    EntityName = n.EntityName,
                    Category = n.Category,
                    Timestamp = n.Timestamp,
                    Notifications = n.Notifications,
                    ElapsedMinutes = (currentEpoch - n.Timestamp) / 60
                })
                .ToListAsync();
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<ClinicConsentForm>> GetConsentFormsByClinicIdAsync(int clinicId)
        {
            return await _context.ClinicConsentForms
                .Where(f => f.ClinicId == clinicId)
                .ToListAsync();
        }

        public async Task<ClinicConsentForm?> GetConsentFormByIdAsync(int consentFormId)
        {
            return await _context.ClinicConsentForms
                .FirstOrDefaultAsync(f => f.Id == consentFormId);
        }

        public async Task UpdateConsentFormAsync(ClinicConsentForm consentForm)
        {
            _context.ClinicConsentForms.Update(consentForm);
            await _context.SaveChangesAsync();
        }

        public async Task<High5FormImages> SaveHigh5FormImageAsync(High5FormImages formImage)
        {
            await _context.High5FormImages.AddAsync(formImage);
            await _context.SaveChangesAsync();
            return formImage;
        }

        public async Task<List<High5FormImages>> GetHigh5FormImagesByVisitIdAsync(int visitId)
        {
            return await _context.High5FormImages
                .Include(x => x.ConsentForm)
                .Include(x => x.Patient)
                .Include(x => x.Visit)
                .Where(x => x.ClinicVisitId == visitId)
                .OrderByDescending(x => x.EpochTime)
                .ToListAsync();
        }

        public async Task<High5FormImages?> GetHigh5FormImageByIdAsync(int id)
        {
            return await _context.High5FormImages
                .Include(x => x.ConsentForm)
                .Include(x => x.Patient)
                .Include(x => x.Visit)
                .FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task<ClinicPatient?> GetPatientByIdAsync(int patientId)
        {
            return await _context.ClinicPatients
                .FirstOrDefaultAsync(p => p.Id == patientId);
        }

        public async Task DeleteHigh5FormImageAsync(int imageId)
        {
            var formImage = await _context.High5FormImages.FindAsync(imageId);
            if (formImage != null)
            {
                _context.High5FormImages.Remove(formImage);
                await _context.SaveChangesAsync();
            }
        }



    }
}
