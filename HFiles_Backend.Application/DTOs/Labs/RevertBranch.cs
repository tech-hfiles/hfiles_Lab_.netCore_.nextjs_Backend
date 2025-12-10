using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HFiles_Backend.Application.DTOs.Labs
{
    public class RevertBranch
    {
        [Required(ErrorMessage = "Branch ID is required.")]
        public int Id { get; set; }
    }
}
