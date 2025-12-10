namespace HFiles_Backend.API.Settings
{
    namespace HFiles_Backend.Application.Settings
    {
        public class GoogleCalendarSettings
        {
            public string ApplicationName { get; set; } = null!;
            public string CredentialsPath { get; set; } = null!;
            public string CalendarId { get; set; } = "primary";
        }
    }
}
