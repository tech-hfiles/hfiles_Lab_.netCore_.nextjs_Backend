using HFiles_Backend.Application.DTOs.Labs;
using Newtonsoft.Json;

namespace HFiles_Backend.API.Services
{
    public class LocationService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<LocationService> _logger;

        public LocationService(HttpClient httpClient, ILogger<LocationService> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _logger.LogInformation("LocationService initialized successfully.");
        }

        public async Task<string> GetLocationDetails(string? pincode)
        {
            if (string.IsNullOrWhiteSpace(pincode))
            {
                _logger.LogWarning("Location request failed: Pincode is missing or invalid.");
                return "Invalid pincode";
            }

            _logger.LogInformation("Fetching location details for Pincode: {Pincode}.", pincode);

            try
            {
                var response = await _httpClient.GetAsync($"https://api.postalpincode.in/pincode/{pincode}");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to fetch location details for Pincode: {Pincode}. Status Code: {StatusCode}",
                        pincode, response.StatusCode);
                    return $"Failed to fetch location details (Status Code: {response.StatusCode})";
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                var postalData = JsonConvert.DeserializeObject<List<LocationDetailsResponse>>(jsonResponse);

                if (postalData == null || postalData.Count == 0 || postalData[0].Status != "Success")
                {
                    _logger.LogWarning("Location not found for Pincode: {Pincode}.", pincode);
                    return $"Location not found for pincode {pincode}";
                }

                var locationDetails = postalData[0].PostOffice?.FirstOrDefault();

                string location = locationDetails != null
                    ? $"{locationDetails.Name}, {locationDetails.District}, {locationDetails.State}"
                    : "Location not found";

                _logger.LogInformation("Location details retrieved successfully for Pincode: {Pincode}.", pincode);
                return location;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred while fetching location details for Pincode: {Pincode}.", pincode);
                return "An unexpected error occurred while fetching location details.";
            }
        }
    }
}
