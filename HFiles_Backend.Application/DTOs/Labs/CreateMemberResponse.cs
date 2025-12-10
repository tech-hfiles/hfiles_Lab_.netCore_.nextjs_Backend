namespace HFiles_Backend.Application.DTOs.Labs
{
    public class CreateMemberResponse
    {
        public int UserId { get; set; }
        public string Name { get; set; } = null!;
        public string Email { get; set; } = null!;
        public int LabId { get; set; }
        public string LabName { get; set; } = null!;
        public string CreatedBy { get; set; } = null!;
        public string Role { get; set; } = null!;
        public long EpochTime { get; set; }
        public int BranchLabId { get; set; }
        public string NotificationMessage { get; set; } = null!;

        public NotificationContext? NotificationContext { get; set; }
    }

    public class NotificationContext
    {
        public string MemberName { get; set; } = null!;
        public string CreatedByName { get; set; } = null!;
    }

}
