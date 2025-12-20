using HFiles_Backend.API.DTOs.Clinics;
using HFiles_Backend.API.Interfaces;
using HFiles_Backend.Domain.Entities.Clinics;
using HFiles_Backend.Domain.Interfaces;
using Microsoft.AspNetCore.Http;

namespace HFiles_Backend.API.Services
{
    public class ClinicMemberRecordService : IClinicMemberRecordService
    {
        private readonly IClinicMemberRecordRepository _repository;
        private readonly S3StorageService _s3Service;

        public ClinicMemberRecordService(
            IClinicMemberRecordRepository repository,
            S3StorageService s3Service)
        {
            _repository = repository;
            _s3Service = s3Service;
        }

        public async Task<UploadClinicMemberRecordResponseDto> UploadAsync(
     int clinicId,
     int userId,
     string reportName,
     string reportType,
     IFormFile file)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("File required");

            if (file.Length > 50 * 1024 * 1024)
                throw new ArgumentException("Max file size is 50MB");

            var tempPath = Path.GetTempFileName();
            await using (var stream = new FileStream(tempPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var extension = Path.GetExtension(file.FileName);
            var key = $"clinic/{clinicId}/members/{userId}/{Guid.NewGuid()}{extension}";

            var url = await _s3Service.UploadFileToS3(tempPath, key);

            var record = new ClinicMemberRecord
            {
                ClinicId = clinicId,
                UserId = userId,
                ReportName = reportName,
                ReportType = reportType,
                ReportUrl = url!,
                FileSize = file.Length,
                DeletedBy = 0,
                EpochTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            await _repository.AddAsync(record);

            return new UploadClinicMemberRecordResponseDto
            {
                ReportName = record.ReportName,
                ReportUrl = record.ReportUrl,
                ReportType = record.ReportType,
                FileSize = record.FileSize
            };
        }


        public async Task<List<ClinicMemberRecordDto>> GetAsync(int clinicId, int userId)
        {

            var records = await _repository.GetByClinicAndUserAsync(clinicId, userId);

            return records.Select(r => new ClinicMemberRecordDto
            {
                Id = r.Id,
                ClinicId = r.ClinicId,
                UserId = r.UserId,
                ReportName = r.ReportName,
                ReportUrl = r.ReportUrl,
                ReportType = r.ReportType,
                FileSize = r.FileSize,
                EpochTime = r.EpochTime
            }).ToList();
        }
    }
}
