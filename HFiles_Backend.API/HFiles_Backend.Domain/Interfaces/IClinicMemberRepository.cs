using HFiles_Backend.Domain.Entities.Clinics;

namespace HFiles_Backend.Domain.Interfaces
{
    public interface IClinicMemberRepository
    {
        Task<ClinicMember?> GetMemberAsync(int userId, int clinicId, string role);
        Task<bool> MemberExistsAsync(int userId, int clinicId, string role);
        Task AddAsync(ClinicMember member);
    }
}
