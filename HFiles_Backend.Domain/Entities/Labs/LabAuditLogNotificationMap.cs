using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HFiles_Backend.Domain.Entities.Labs
{
    public class LabAuditLogNotificationMap
    {
        [Key]
        public int Id { get; set; }

        public int LabNotificationId { get; set; }
        public int LabAuditLogId { get; set; }

        [ForeignKey("LabNotificationId")]
        public LabNotification? Notification { get; set; }

        [ForeignKey("LabAuditLogId")]
        public LabAuditLog? AuditLog { get; set; }
    }
}
