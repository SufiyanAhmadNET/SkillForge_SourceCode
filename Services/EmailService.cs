using System.Net.Http.Json;
using SkillForge.Interfaces;

namespace SkillForge.Services
{
    // Sends OTP emails via Brevo HTTP API (replaces SMTP — works on Render)
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendOtpEmail(string toEmail, string otp)
        {
            // 1. Load Configuration
            var senderName = _config["EmailSettings:SenderName"];
            var senderEmail = _config["EmailSettings:SenderEmail"];
            var apiKey = _config["EmailSettings:SenderPassword"];

            // 2. Validate Configuration
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new Exception("Brevo API Key is missing. Ensure 'EmailSettings:SenderPassword' is set in User Secrets or Render Environment Variables.");
            }

            if (apiKey.StartsWith("xsmtpsib-", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception("Invalid Key Type: You are using an SMTP Password (xsmtpsib-). For the HTTP API, you MUST use an API Key (starts with 'xkeysib-' or 'xms-'). Generate one in Brevo -> SMTP & API -> API Keys.");
            }

            if (string.IsNullOrWhiteSpace(senderEmail))
            {
                throw new Exception("Sender Email is missing. Check 'EmailSettings:SenderEmail' in appsettings.json.");
            }

            // 3. Prepare Payload
            var htmlContent = $@"
                <div style='font-family:Arial;padding:20px;text-align:center;'>
                    <h2>Email Verification</h2>
                    <p>Your OTP is:</p>
                    <div style='font-size:28px;font-weight:bold;letter-spacing:8px;
                                padding:10px;background:#f1f1f1;border-radius:8px;'>
                        {otp}
                    </div>
                    <p>This OTP is valid for 5 minutes.</p>
                </div>";

            var payload = new
            {
                sender = new { name = senderName ?? "SkillForge", email = senderEmail },
                to = new[] { new { email = toEmail } },
                subject = "Your SkillForge OTP Code",
                htmlContent = htmlContent
            };

            // 4. Send Request
            using var httpClient = new HttpClient();
            // Use TryAddWithoutValidation to handle special characters safely
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("api-key", apiKey.Trim());

            try 
            {
                var response = await httpClient.PostAsJsonAsync("https://api.brevo.com/v3/smtp/email", payload);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    // Log to console for Render troubleshooting
                    Console.WriteLine($"Brevo API Error: {response.StatusCode} - {error}");
                    throw new Exception($"Brevo API failed: {response.StatusCode}. Details: {error}");
                }
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Network error while calling Brevo API: {ex.Message}");
            }
        }
    }
}
