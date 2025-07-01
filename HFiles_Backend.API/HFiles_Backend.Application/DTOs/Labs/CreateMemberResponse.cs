namespace HFiles_Backend.Application.DTOs.Labs
{
    public class CreateMemberResponse
    {
        public int UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int LabId { get; set; }
        public string LabName { get; set; } = string.Empty;
        public int CreatedBy { get; set; }
        public string Role { get; set; } = string.Empty;
        public long EpochTime { get; set; }
        public int BranchLabId { get; set; }
    }
}
