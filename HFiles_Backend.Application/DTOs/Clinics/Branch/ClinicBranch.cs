using System.ComponentModel.DataAnnotations;

namespace HFiles_Backend.Application.DTOs.Clinics.Branch
{
    public class ClinicBranch
    {
        [Required(ErrorMessage = "Clinic name is required.")]
        public string ClinicName { get; set; } = null!;

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "Phone number is required.")]
        [Phone(ErrorMessage = "Invalid phone number.")]
        public string PhoneNumber { get; set; } = null!;

        [Required(ErrorMessage = "Pincode is required.")]
        [RegularExpression(@"^\d{5,6}$", ErrorMessage = "Invalid pincode format.")]
        public string Pincode { get; set; } = null!;
    }
}
