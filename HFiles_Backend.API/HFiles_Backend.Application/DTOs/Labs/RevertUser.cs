using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HFiles_Backend.Application.DTOs.Labs
{
    public class RevertUser
    {
        [Required(ErrorMessage = "User ID is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "User ID must be greater than zero.")]
        public int Id { get; set; }

        [Required(ErrorMessage = "Lab ID is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Lab ID must be greater than zero.")]
        public int LabId { get; set; }

        [Required(ErrorMessage = "Role is required.")]
        [MaxLength(20, ErrorMessage = "Role must not exceed 20 characters.")]
        public string? Role { get; set; }
    }

}
