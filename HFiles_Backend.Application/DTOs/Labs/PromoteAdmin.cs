using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HFiles_Backend.Application.DTOs.Labs
{
    public class PromoteAdmin
    {
        [Required(ErrorMessage = "MemberId is Required.")]
        [Range(1, int.MaxValue, ErrorMessage = "MemberId must be greater than zero.")]
        public int MemberId { get; set; }
    }
}
