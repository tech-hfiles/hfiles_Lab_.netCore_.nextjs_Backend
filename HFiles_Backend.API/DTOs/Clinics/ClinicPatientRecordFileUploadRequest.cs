using HFiles_Backend.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace HFiles_Backend.API.DTOs.Clinics
{
    public class ClinicPatientRecordFileUploadRequest
    {
        [Required]
        public int ClinicId { get; set; }

        [Required]
        public int PatientId { get; set; }

        [Required]
        public int ClinicVisitId { get; set; }

        [Required]
        public RecordType Type { get; set; }

        [Required(ErrorMessage = "Report file is required.")]
        [DataType(DataType.Upload)]
        [MaxFileSize(50 * 1024 * 1024, ErrorMessage = "File size cannot exceed 50 MB.")]
        public IFormFileCollection Files { get; set; } = null!;
    }

    public class MaxFileSizeAttribute(int maxFileSize) : ValidationAttribute
    {
        private readonly int _maxFileSize = maxFileSize;

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            var file = value as IFormFile;
            if (file != null && file.Length > _maxFileSize)
            {
                return new ValidationResult($"Maximum allowed file size is {_maxFileSize / (1024 * 1024)} MB.");
            }

            return ValidationResult.Success;
        }
    }
}
