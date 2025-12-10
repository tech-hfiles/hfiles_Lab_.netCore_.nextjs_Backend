using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HFiles_Backend.Application.DTOs.Labs
{
    public class Branch
    {
        [Required(ErrorMessage = "Lab name is required.")]
        public string LabName { get; set; } = null!;

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
