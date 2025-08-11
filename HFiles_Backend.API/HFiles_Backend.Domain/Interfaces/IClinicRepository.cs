using HFiles_Backend.Domain.Entities.Clinics;
using Microsoft.EntityFrameworkCore.Storage;

namespace HFiles_Backend.Domain.Interfaces
{
    public interface IClinicRepository
    {
        Task<IDbContextTransaction> BeginTransactionAsync();
        Task<bool> EmailExistsAsync(string email);
        Task<ClinicSignup?> GetByEmailAsync(string email);
        Task<ClinicSignup?> GetByPhoneAsync(string phoneNumber);
        Task<ClinicOtpEntry?> GetLatestOtpAsync(string emailOrPhone);
        Task AddSignupAsync(ClinicSignup signup);
        Task RemoveOtpAsync(ClinicOtpEntry otpEntry);
        Task AddOtpAsync(ClinicOtpEntry entry);
        Task SaveChangesAsync();
    }
}
