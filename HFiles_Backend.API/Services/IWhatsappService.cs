namespace HFiles_Backend.API.Services
{
    public interface IWhatsappService
    {
        Task SendOtpAsync(string otp, string phoneNumber);
    }

}
