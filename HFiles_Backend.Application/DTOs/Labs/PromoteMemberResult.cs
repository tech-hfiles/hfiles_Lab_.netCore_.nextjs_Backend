using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HFiles_Backend.Application.DTOs.Labs
{
    public class PromoteMemberResult
    {
        public int Id { get; set; }
        public string Status { get; set; } = "Success";
        public string? Reason { get; set; }
        public string? NewRole { get; set; }
        public int? PromotedBy { get; set; }
        public string? PromotedByRole { get; set; }
    }
}
