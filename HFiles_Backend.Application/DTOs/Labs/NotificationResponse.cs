using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HFiles_Backend.Application.DTOs.Labs
{
    public class NotificationResponse
    {
        public bool Success { get; set; }
        public string? Status { get; set; }
        public string? Reason { get; set; }
        public int LabUserReportId { get; set; }
        public int? BranchLabId { get; set; }
        public string? ResendReportName { get; set; }
        public string? ResendReportType { get; set; }
        public int? NewResendCount { get; set; }
        public string? NotificationMessage { get; set; }
    }
}
