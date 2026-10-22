using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace RentLog.Services;

public interface IEmailNotificationService
{
    Task<bool> SendContractorJobNotificationAsync(string recipientEmail, string contractorName, string jobTitle, string priority, string? propertyAddress, string directPortalLink, string pinCode);
}

public class ResendEmailNotificationService : IEmailNotificationService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly ILogger<ResendEmailNotificationService> _logger;

    public ResendEmailNotificationService(HttpClient httpClient, IConfiguration config, ILogger<ResendEmailNotificationService> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
    }

    public async Task<bool> SendContractorJobNotificationAsync(
        string recipientEmail,
        string contractorName,
        string jobTitle,
        string priority,
        string? propertyAddress,
        string directPortalLink,
        string pinCode)
    {
        var apiKey = _config["Resend:ApiKey"] ?? "re_Y1Xnkqc1_JruZgmT5r2fcWshEeqD4ExA3";
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("Resend API key is not configured.");
            return false;
        }

        var html = $@"
<!DOCTYPE html>
<html>
<head>
  <meta charset='utf-8'>
  <style>
    body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background-color: #0f172a; color: #f8fafc; margin: 0; padding: 24px; }}
    .container {{ max-width: 600px; margin: 0 auto; background-color: #1e293b; border-radius: 16px; border: 1px solid #334155; padding: 32px; }}
    .badge {{ display: inline-block; padding: 4px 12px; border-radius: 9999px; font-size: 11px; font-weight: bold; text-transform: uppercase; letter-spacing: 0.05em; background: rgba(245, 158, 11, 0.2); color: #fbbf24; border: 1px solid rgba(245, 158, 11, 0.3); }}
    h1 {{ color: #ffffff; font-size: 22px; margin-top: 16px; }}
    p {{ color: #cbd5e1; font-size: 14px; line-height: 1.6; }}
    .details {{ background: #0f172a; border-radius: 12px; padding: 16px; margin: 20px 0; border: 1px solid #334155; }}
    .details-row {{ display: flex; justify-content: space-between; margin-bottom: 8px; font-size: 13px; }}
    .label {{ color: #94a3b8; }}
    .value {{ color: #f8fafc; font-weight: 600; }}
    .pin-box {{ background: linear-gradient(135deg, rgba(245, 158, 11, 0.15), rgba(99, 102, 241, 0.15)); border: 2px dashed #f59e0b; border-radius: 12px; padding: 16px; text-align: center; margin: 20px 0; }}
    .pin-code {{ font-size: 28px; font-weight: 800; font-family: monospace; letter-spacing: 6px; color: #fbbf24; }}
    .btn {{ display: inline-block; background-color: #f59e0b; color: #0f172a !important; font-weight: bold; font-size: 14px; text-decoration: none; padding: 14px 28px; border-radius: 10px; margin: 16px 0; }}
    .footer {{ font-size: 11px; color: #64748b; margin-top: 24px; text-align: center; }}
  </style>
</head>
<body>
  <div class='container'>
    <span class='badge'>New Work Order Dispatched</span>
    <h1>New Job Assignment</h1>
    <p>Hi <strong>{contractorName}</strong>,</p>
    <p>You have been assigned to an active maintenance work order on RentLog. Please review the job specs and use your security PIN to access the contractor portal:</p>
    
    <div class='pin-box'>
      <div style='font-size: 11px; text-transform: uppercase; font-weight: bold; color: #94a3b8; letter-spacing: 1px; margin-bottom: 4px;'>Your Security Dispatch PIN</div>
      <div class='pin-code'>{pinCode}</div>
      <div style='font-size: 11px; color: #cbd5e1; margin-top: 4px;'>Use this PIN if prompted at contractor login or verification</div>
    </div>

    <div class='details'>
      <div class='details-row'><span class='label'>Work Order:</span> <span class='value'>{jobTitle}</span></div>
      <div class='details-row'><span class='label'>Priority:</span> <span class='value'>{priority}</span></div>
      {(string.IsNullOrWhiteSpace(propertyAddress) ? "" : $"<div class='details-row'><span class='label'>Property / Address:</span> <span class='value'>{propertyAddress}</span></div>")}
    </div>

    <center>
      <a href='{directPortalLink}' class='btn'>Open Contractor Portal</a>
    </center>

    <p style='font-size: 12px; color: #94a3b8;'>
      Direct link with automatic authorization:<br>
      <a href='{directPortalLink}' style='color: #818cf8; word-break: break-all;'>{directPortalLink}</a>
    </p>

    <div class='footer'>
      RentLog Property Management Dispatch &bull; Automated Service Notification
    </div>
  </div>
</body>
</html>";

        var payload = new
        {
            from = "RentLog Dispatch <onboarding@resend.dev>",
            to = new[] { recipientEmail.Trim() },
            subject = $"[RentLog Dispatch] New Job Assigned: {jobTitle}",
            html = html
        };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully sent job dispatch email to {Email} via Resend.", recipientEmail);
                return true;
            }
            else
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to send email via Resend. Status: {StatusCode}, Body: {Body}", response.StatusCode, errorBody);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception sending email via Resend.");
            return false;
        }
    }
}
