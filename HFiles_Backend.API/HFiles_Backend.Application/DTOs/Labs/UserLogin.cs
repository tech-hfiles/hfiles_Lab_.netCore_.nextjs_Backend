using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HFiles_Backend.Application.DTOs.Labs
{
    public class UserLogin
    {
        [Required(ErrorMessage = "HFID is required.")]
        [MaxLength(20, ErrorMessage = "HFID must not exceed 20 characters.")]
        public string HFID { get; set; } = null!;

        [Required(ErrorMessage = "Role is required.")]
        [MaxLength(20, ErrorMessage = "Role must not exceed 20 characters.")]
        public string Role { get; set; } = null!;

        [Required(ErrorMessage = "Password is required.")]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
        [MaxLength(100, ErrorMessage = "Password must not exceed 100 characters.")]
        public string Password { get; set; } = null!;

        [Required(ErrorMessage = "LabId is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "LabId must be greater than zero.")]
        public int UserId { get; set; }

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        [MaxLength(100, ErrorMessage = "Email must not exceed 100 characters.")]
        public string Email { get; set; } = null!;
    }

}