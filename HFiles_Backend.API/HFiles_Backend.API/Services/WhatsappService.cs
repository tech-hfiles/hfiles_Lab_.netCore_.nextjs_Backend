﻿using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace HFiles_Backend.API.Services
{
    public class WhatsappService : IWhatsappService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiUrl;
        private readonly string _apiKey;
        private readonly ILogger<WhatsappService> _logger;

        public WhatsappService(IConfiguration config, HttpClient httpClient, ILogger<WhatsappService> logger)
        {
            _apiUrl = config["Interakt:ApiUrl"] ?? throw new ArgumentNullException(nameof(config), "Interakt:ApiUrl configuration is missing.");
            _apiKey = config["Interakt:ApiKey"] ?? throw new ArgumentNullException(nameof(config), "Interakt:ApiKey configuration is missing.");

            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _logger.LogInformation("WhatsappService initialized successfully.");
        }

        public async Task SendOtpAsync(string otp, string mobileNo)
        {
            _logger.LogInformation("Processing OTP send request for Mobile: {MobileNo}.", mobileNo);

            string countryCodeDigit = "";
            string pureMobileNumber = "";

            Match match = Regex.Match(mobileNo, @"^(\+?\d{1,4})?(\d{10})$");

            if (match.Success)
            {
                countryCodeDigit = match.Groups[1].Value;
                pureMobileNumber = match.Groups[2].Value;

                if (string.IsNullOrEmpty(countryCodeDigit))
                    countryCodeDigit = "+91";
            }
            else
            {
                _logger.LogWarning("OTP sending failed: Invalid mobile number format ({MobileNo}).", mobileNo);
                throw new ArgumentException("Invalid mobile number format.");
            }

            var requestBody = new
            {
                countryCode = countryCodeDigit,
                phoneNumber = pureMobileNumber,
                type = "Template",
                callbackData = "otp_callback",
                template = new
                {
                    name = "otp_template",
                    languageCode = "en",
                    headerValues = new[] { "OTP" },
                    bodyValues = new[] { otp },
                    buttonValues = new
                    {
                        _0 = new[] { otp }
                    }
                }
            };

            string jsonContent = JsonConvert.SerializeObject(requestBody).Replace("_0", "0");
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Basic {_apiKey}");

            try
            {
                _logger.LogInformation("Sending OTP {Otp} to {MobileNo} via WhatsApp API.", otp, mobileNo);

                var response = await _httpClient.PostAsync(_apiUrl, content);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to send WhatsApp OTP to {MobileNo}. API Response: {StatusCode}, Error: {Error}",
                        mobileNo, response.StatusCode, error);
                    throw new Exception($"Failed to send WhatsApp message. {_apiUrl} responded with {response.StatusCode}: {error}");
                }

                _logger.LogInformation("WhatsApp OTP successfully sent to {MobileNo}.", mobileNo);
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "Failed to send OTP to {MobileNo}. HTTP Request issue.", mobileNo);
                throw new Exception("Failed to send OTP. Please check API connectivity.", httpEx);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while sending OTP to {MobileNo}.", mobileNo);
                throw new Exception("An unexpected error occurred while sending OTP.", ex);
            }
        }
    }
}
