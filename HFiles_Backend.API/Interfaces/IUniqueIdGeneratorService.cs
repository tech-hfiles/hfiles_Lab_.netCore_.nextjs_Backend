using HFiles_Backend.Domain.Enums;

namespace HFiles_Backend.API.Interfaces
{
    public interface IUniqueIdGeneratorService
    {
        Task<string> GenerateUniqueIdAsync(int clinicId, RecordType recordType);
        Task<string> GetNextAvailableIdAsync(int clinicId, RecordType recordType); 
    }
}
