using HFiles_Backend.Domain.Entities.Clinics;
using static System.Net.Mime.MediaTypeNames;

namespace HFiles_Backend.Domain.Interfaces
{
    public interface IClinicMemberRepository
    {
        Task<ClinicMember?> GetMemberAsync(int userId, int clinicId, string role);
        Task<bool> MemberExistsAsync(int userId, int clinicId, string role);
        Task AddAsync(ClinicMember member);
        //Task<List<Application.DTOs.Labs.User>> GetMemberDtosByClinicIdAsync(int clinicId);
        Task<Dictionary<int, ClinicMember>> GetAllMembersAsync();
        Task<ClinicMember?> GetByIdInBranchesAsync(int memberId, List<int> branchIds);
        Task UpdateAsync(ClinicMember member);
    }
}
