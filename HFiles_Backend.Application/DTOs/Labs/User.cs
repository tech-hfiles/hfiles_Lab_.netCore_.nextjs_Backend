using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HFiles_Backend.Application.DTOs.Labs
{
    public class User
    {
        public int MemberId { get; set; }
        public string HFID { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string PromotedByName { get; set; } = "Not Promoted Yet";
        public string CreatedByName { get; set; } = "Unknown";
        public string ProfilePhoto { get; set; } = "No image preview available";
    }
}
