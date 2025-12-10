using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HFiles_Backend.Application.DTOs.Clinics.Branch
{
    public class DeletedBranchDto
    {
        public int Id { get; set; }
        public string LabName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string HFID { get; set; } = string.Empty;
        public string? ProfilePhoto { get; set; }
        public int DeletedBy { get; set; }
        public string? DeletedByUser { get; set; }
        public string? DeletedByUserRole { get; set; }
    }
}
