using HFiles_Backend.Domain.Entities.Clinics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HFiles_Backend.Domain.Interfaces
{
    public interface IClinicMemberRecordRepository
    {
        Task<ClinicMemberRecord?> GetByIdAsync(int id);
        Task<ClinicMemberRecord> UpdateAsync(ClinicMemberRecord record);
        Task AddAsync(ClinicMemberRecord record);
        Task<List<ClinicMemberRecord>> GetByClinicAndUserAsync(int clinicId, int userId);
        Task<ClinicMember?> GetClinicMemberByIdAsync(int id);
        Task<List<ClinicMemberRecord>> GetRecordsByClinicMemberAsync(int clinicMemberId);
    }
}
