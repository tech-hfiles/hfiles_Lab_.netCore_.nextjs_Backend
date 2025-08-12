namespace HFiles_Backend.Application.DTOs.Clinics.Member
{
    public class DeletedClinicMemberDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = "Email Not Found";
        public string HFID { get; set; } = "HFID Not Found";
        public string ProfilePhoto { get; set; } = "No image preview available";
        public int ClinicId { get; set; }
        public string Role { get; set; } = "Role Not Found";
        public string DeletedByUser { get; set; } = "Name Not Found";
        public string DeletedByUserRole { get; set; } = "Role Not Found";
    }

}
