using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HFiles_Backend.Application.DTOs.Labs
{
    public class LabInfo
    {
        public int LabId { get; set; }
        public string? LabName { get; set; }
        public string? HFID { get; set; }
        public string? Email { get; set; } 
        public string? PhoneNumber { get; set; }
        public string? Pincode { get; set; }
        public string? Location { get; set; }
        public string? Address { get; set; }
        public string? ProfilePhoto { get; set; }
        public string LabType { get; set; } = "branch";
    }
}
