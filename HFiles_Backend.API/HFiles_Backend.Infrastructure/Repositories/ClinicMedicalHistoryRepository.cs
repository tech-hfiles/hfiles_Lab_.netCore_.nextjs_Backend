using HFiles_Backend.Application.DTOs.Clinics.MedicalHistory;
using HFiles_Backend.Domain.Enums;
using HFiles_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HFiles_Backend.Infrastructure.Repositories
{
    public class ClinicMedicalHistoryRepository(AppDbContext context)
    {
        private readonly AppDbContext _context = context;





        public async Task<List<UserSurgeryDetailsDto>> GetSurgerySummaryAsync(List<int> userIds)
        {
            return await _context.UserSurgeryDetails
                .Where(x => userIds.Contains(x.user_id))
                .Select(x => new UserSurgeryDetailsDto
                {
                    SurgeryId = x.user_surgery_id,
                    Details = x.user_surgery_details,
                    Year = x.user_surgery_year,
                    Hospital = x.hostname,
                    DoctorName = x.drname
                })
               .ToListAsync();
        }




        public async Task<List<UserStaticAllergySummaryDto>> GetStaticAllergySummaryAsync(int userId)
        {
            // Step 1: Load existing allergy entries for this user from DB
            var existing = await _context.UserStaticAllergies
                .Where(x => x.UserId == userId)
                .ToListAsync();

            // Step 2: Load all predefined enum values of StaticAllergyType
            var allTypes = Enum.GetValues(typeof(StaticAllergyType))
                .Cast<StaticAllergyType>()
                .ToList();

            // Step 3: Prepare final response list
            var result = new List<UserStaticAllergySummaryDto>();

            foreach (var type in allTypes)
            {
                // Try to find matching entry in DB for this allergy type
                var match = existing.FirstOrDefault(x => x.AllergyType == type);
                if (match != null)
                {
                    // User has this allergy explicitly recorded
                    result.Add(new UserStaticAllergySummaryDto
                    {
                        Id = match.Id,
                        AllergyType = match.AllergyType,
                        IsAllergic = match.IsAllergic
                    });
                }
                else
                {
                    // Allergy type missing — default response with IsAllergic = false
                    result.Add(new UserStaticAllergySummaryDto
                    {
                        Id = 0, // Indicates no DB record
                        AllergyType = type,
                        IsAllergic = false
                    });
                }
            }

            // Step 4: Return final list including both stored and defaulted entries
            return result;
        }




        public async Task<List<DynamicAllergySummaryDto>> GetDynamicAllergySummariesAsync(List<int> userIds)
        {
            return await _context.UserDynamicAllergies
                .Where(x => userIds.Contains(x.UserId))
                .Select(x => new DynamicAllergySummaryDto
                {
                    Id = x.Id,
                    AllergyName = x.AllergyName,
                    IsAllergic = x.IsAllergic
                })
                .ToListAsync();
        }




        public async Task<List<UserMedicationAllergyDto>> GetMedicationAllergySummaryAsync(List<int> userIds)
        {
            return await _context.UserMedicationAllergies
                .Where(x => userIds.Contains(x.UserId))
                .Select(x => new UserMedicationAllergyDto
                {
                    Id = x.Id,
                    StaticAllergyId = x.StaticAllergyId,
                    MedicationName = x.MedicationName
                })
                .ToListAsync();
        }




        public async Task<List<UserStaticDiseaseDto>> GetStaticDiseaseWithDefaultsAsync(int userId)
        {
            // Step 1: Get existing entries for the user
            var existing = await _context.UserStaticDiseases
                .Where(x => x.UserId == userId)
                .ToListAsync();

            // Step 2: Get all static disease types from enum
            var allTypes = Enum.GetValues(typeof(StaticDiseaseType))
                .Cast<StaticDiseaseType>()
                .ToList();

            var results = new List<UserStaticDiseaseDto>();

            foreach (var type in allTypes)
            {
                var match = existing.FirstOrDefault(x => x.DiseaseType == type);
                if (match != null)
                {
                    results.Add(new UserStaticDiseaseDto
                    {
                        Id = match.Id,
                        DiseaseType = match.DiseaseType,
                        Myself = match.Myself,
                        MotherSide = match.MotherSide,
                        FatherSide = match.FatherSide
                    });
                }
                else
                {
                    // Return default entry when disease is missing
                    results.Add(new UserStaticDiseaseDto
                    {
                        Id = 0,
                        DiseaseType = type,
                        Myself = false,
                        MotherSide = false,
                        FatherSide = false
                    });
                }
            }

            return results;
        }




        public async Task<List<DynamicDiseaseRecordDto>> GetDynamicDiseaseRecordSummaryAsync(List<int> userIds)
        {
            return await _context.UserDynamicDiseaseRecords
                .Where(x => userIds.Contains(x.UserId))
                .Include(x => x.DiseaseType)
                .Select(x => new DynamicDiseaseRecordDto
                {
                    Id = x.Id,
                    DiseaseTypeId = x.DiseaseTypeId,
                    DiseaseName = x.DiseaseType!.DiseaseName,
                    Myself = x.Myself,
                    MotherSide = x.MotherSide,
                    FatherSide = x.FatherSide
                })
                .ToListAsync();
        }




        public async Task<UserSocialHistoryDto?> GetUserSocialHistoryAsync(int userId)
        {
            return await _context.UserSocialHistories
                .Where(x => x.UserId == userId)
                .Select(x => new UserSocialHistoryDto
                {
                    Id = x.Id,
                    SmokingFrequency = x.SmokingFrequency,
                    AlcoholFrequency = x.AlcoholFrequency,
                    CaffeineFrequency = x.CaffeineFrequency,
                    ExerciseFrequency = x.ExerciseFrequency
                })
                .FirstOrDefaultAsync();
        }




        public async Task<UserProfileSummaryDto?> GetUserProfileSummaryAsync(int userId)
        {
            return await _context.Users
                .Where(x => x.Id == userId)
                .Select(x => new UserProfileSummaryDto
                {
                    ProfilePhoto = x.ProfilePhoto,
                    FullName = $"{x.FirstName} {x.LastName}",
                    Gender = x.Gender ?? "Gender not updated",
                    HeightFeet = x.HeightFeet,
                    HeightInches = x.HeightInches,
                    WeightKg = x.WeightKg,
                    BloodGroup = x.BloodGroup,
                    HfId = x.HfId
                })
                .FirstOrDefaultAsync();
        }
    }
}
