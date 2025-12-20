using HFiles_Backend.API.DTOs.Clinics;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HFiles_Backend.API.Interfaces
{
    public interface IClinicMemberRecordService
    {
        Task<UploadClinicMemberRecordResponseDto> UploadAsync(int clinicId, int userId, string reportName,string reportType, IFormFile file);
        Task<List<ClinicMemberRecordDto>> GetAsync(int clinicId, int userId);
    }
}
