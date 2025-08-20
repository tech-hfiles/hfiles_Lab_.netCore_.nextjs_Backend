using HFiles_Backend.Domain.Entities.Clinics;

namespace HFiles_Backend.Domain.Interfaces
{
    public interface IClinicMemberRepository
    {
        Task<ClinicMember?> GetMemberAsync(int userId, int clinicId, string role);
        Task<bool> MemberExistsAsync(int userId, int clinicId, string role);
        Task AddAsync(ClinicMember member);
        Task<Dictionary<int, ClinicMember>> GetAllMembersAsync();
        Task<ClinicMember?> GetByIdInBranchesAsync(int memberId, List<int> branchIds);
        Task UpdateAsync(ClinicMember member);
        void Update(ClinicMember member);
        Task<ClinicMember?> GetDeletedMemberByIdAsync(int memberId, int clinicId);
        void Remove(ClinicMember member);
        Task<ClinicMember?> GetEligibleMemberForPromotionAsync(int memberId, List<int> branchIds);
        Task<ClinicMember?> GetDeletedMemberByUserIdAsync(int userId, List<int> branchIds);
        Task<ClinicMember?> GetActiveMemberAsync(int userId, int clinicId);
        void Add(ClinicMember member);
    }
}
