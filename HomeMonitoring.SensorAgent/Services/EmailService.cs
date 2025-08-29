using HomeMonitoring.SensorAgent.Models;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Text;

namespace HomeMonitoring.SensorAgent.Services;

public class EmailService : IEmailService
{
    private readonly EmailSettings _emailSettings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<EmailSettings> emailSettings, ILogger<EmailService> logger)
    {
        _emailSettings = emailSettings.Value;
        _logger = logger;
    }

    public async Task SendDeviceOfflineAlertAsync(
        string deviceName,
        string deviceType,
        string ipAddress,
        DateTime lastSeenAt,
        CancellationToken cancellationToken = default)
    {
        var subject = $"⚠️ Device Offline Alert: {deviceName}";

        var html = $$"""

                     <!DOCTYPE html>
                     <html>
                     <head>
                         <style>
                             body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif; }
                             .container { max-width: 600px; margin: 0 auto; padding: 20px; }
                             .header { background-color: #dc3545; color: white; padding: 20px; border-radius: 8px 8px 0 0; }
                             .content { background-color: #f8f9fa; padding: 20px; border: 1px solid #dee2e6; border-top: none; }
                             .device-info { background-color: white; padding: 15پپ0px; border-radius: 8px; margin: 20px 0; }
                             .info-row { display: flex; padding: 10px 0; border-bottom: 1px solid #dee2e6; }
                             .info-label { font-weight: bold; width: 150px; color: #495057; }
                             .info-value { color: #212529; }
                             .warning-box { background-color: #fff3cd; border: 1px solid #ffeaa7; padding: 15px; border-radius: 8px; margin: 20px 0; }
                             .footer { margin-top: 20px; padding: 20px; text-align: center; color: #6c757d; font-size: 0.875rem; }
                         </style>
                     </head>
                     <body>
                         <div class='container'>
                             <div class='header'>
                                 <h2 style='margin: 0;'>⚠️ Device Offline Alert</h2>
                             </div>
                             <div class='content'>
                                 <p>The following device has not reported data and appears to be offline:</p>
                                 
                                 <div class='device-info'>
                                     <div class='info-row'>
                                         <div class='info-label'>Device Name:</div>
                                         <div class='info-value'>{{deviceName}}</div>
                                     </div>
                                     <div class='info-row'>
                                         <div class='info-label'>Device Type:</div>
                                         <div class='info-value'>{{GetFriendlyDeviceType(deviceType)}}</div>
                                     </div>
                                     <div class='info-row'>
                                         <div class='info-label'>IP Address:</div>
                                         <div class='info-value'>{{ipAddress}}</div>
                                     </div>
                                     <div class='info-row'>
                                         <div class='info-label'>Last Seen:</div>
                                         <div class='info-value'>{{lastSeenAt:yyyy-MM-dd HH:mm:ss}} UTC</div>
                                     </div>
                                     <div class='info-row' style='border-bottom: none;'>
                                         <div class='info-label'>Offline Duration:</div>
                                         <div class='info-value' style='color: #dc3545; font-weight: bold;'>{{GetDurationString(DateTime.UtcNow - lastSeenAt)}}</div>
                                     </div>
                                 </div>
                                 
                                 <div class='warning-box'>
                                     <strong>⚡ Possible Causes:</strong>
                                     <ul style='margin-bottom: 0;'>
                                         <li>Device power loss (check powerbank if using P1 meter)</li>
                                         <li>Network connectivity issues</li>
                                         <li>Device malfunction or crash</li>
                                         <li>IP address changed (DHCP renewal)</li>
                                     </ul>
                                 </div>
                                 
                                 <p><strong>Recommended Actions:</strong></p>
                                 <ol>
                                     <li>Check if the device has power</li>
                                     <li>Verify network connectivity</li>
                                     <li>Try accessing the device directly at <a href='http://{{ipAddress}}'>http://{{ipAddress}}</a></li>
                                     <li>Restart the device if necessary</li>
                                 </ol>
                             </div>
                             <div class='footer'>
                                 <p>This is an automated alert from your Home Monitoring System</p>
                                 <p>Sent at {{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}} UTC</p>
                             </div>
                         </div>
                     </body>
                     </html>
                     """;

        await SendEmailAsync(_emailSettings.MonitoringEmail, subject, html, cancellationToken);
    }

    public async Task SendDeviceBackOnlineAlertAsync(
        string deviceName,
        string deviceType,
        string ipAddress,
        DateTime offlineSince,
        CancellationToken cancellationToken = default)
    {
        var subject = $"✅ Device Back Online: {deviceName}";
        var downtime = DateTime.UtcNow - offlineSince;

        var html = $$"""

                     <!DOCTYPE html>
                     <html>
                     <head>
                         <style>
                             body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif; }
                             .container { max-width: 600px; margin: 0 auto; padding: 20px; }
                             .header { background-color: #28a745; color: white; padding: 20px; border-radius: 8px 8px 0 0; }
                             .content { background-color: #f8f9fa; padding: 20px; border: 1px solid #dee2e6; border-top: none; }
                             .device-info { background-color: white; padding: 20px; border-radius: 8px; margin: 20px 0; }
                             .info-row { display: flex; padding: 10px 0; border-bottom: 1px solid #dee2e6; }
                             .info-label { font-weight: bold; width: 150px; color: #495057; }
                             .info-value { color: #212529; }
                             .success-box { background-color: #d4edda; border: 1px solid #c3e6cb; padding: 15px; border-radius: 8px; margin: 20px 0; }
                             .footer { margin-top: 20px; padding: 20px; text-align: center; color: #6c757d; font-size: 0.875rem; }
                         </style>
                     </head>
                     <body>
                         <div class='container'>
                             <div class='header'>
                                 <h2 style='margin: 0;'>✅ Device Back Online</h2>
                             </div>
                             <div class='content'>
                                 <p>Great news! The following device is back online and reporting data:</p>
                                 
                                 <div class='device-info'>
                                     <div class='info-row'>
                                         <div class='info-label'>Device Name:</div>
                                         <div class='info-value'>{{deviceName}}</div>
                                     </div>
                                     <div class='info-row'>
                                         <div class='info-label'>Device Type:</div>
                                         <div class='info-value'>{{GetFriendlyDeviceType(deviceType)}}</div>
                                     </div>
                                     <div class='info-row'>
                                         <div class='info-label'>IP Address:</div>
                                         <div class='info-value'>{{ipAddress}}</div>
                                     </div>
                                     <div class='info-row'>
                                         <div class='info-label'>Offline Since:</div>
                                         <div class='info-value'>{{offlineSince:yyyy-MM-dd HH:mm:ss}} UTC</div>
                                     </div>
                                     <div class='info-row' style='border-bottom: none;'>
                                         <div class='info-label'>Total Downtime:</div>
                                         <div class='info-value' style='color: #dc3545; font-weight: bold;'>{{GetDurationString(downtime)}}</div>
                                     </div>
                                 </div>
                                 
                                 <div class='success-box'>
                                     <strong>✅ Status:</strong> The device is now functioning normally and collecting data.
                                 </div>
                             </div>
                             <div class='footer'>
                                 <p>This is an automated alert from your Home Monitoring System</p>
                                 <p>Sent at {{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}} UTC</p>
                             </div>
                         </div>
                     </body>
                     </html>
                     """;

        await SendEmailAsync(_emailSettings.MonitoringEmail, subject, html, cancellationToken);
    }

    private async Task SendEmailAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_emailSettings.FromName, _emailSettings.FromEmail));
            message.To.Add(new MailboxAddress(to, to));
            message.Subject = subject;

            message.Body = new TextPart(TextFormat.Html) { Text = htmlBody };

            using var client = new SmtpClient();

            // For Mailpit, we don't need authentication
            await client.ConnectAsync(_emailSettings.SmtpHost, _emailSettings.SmtpPort, _emailSettings.UseSsl,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(_emailSettings.SmtpUsername))
                await client.AuthenticateAsync(_emailSettings.SmtpUsername, _emailSettings.SmtpPassword,
                    cancellationToken);

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation("Email sent successfully to {Recipient} with subject: {Subject}", to, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Recipient} with subject: {Subject}", to, subject);
            throw;
        }
    }

    private static string GetDurationString(TimeSpan duration)
    {
        if (duration.TotalDays >= 1)
            return $"{(int)duration.TotalDays} days, {duration.Hours} hours, {duration.Minutes} minutes";

        if (duration.TotalHours >= 1) return $"{(int)duration.TotalHours} hours, {duration.Minutes} minutes";

        return $"{(int)duration.TotalMinutes} minutes";
    }

    private static string GetFriendlyDeviceType(string deviceType)
    {
        return deviceType switch
        {
            "HWE_P1" => "P1 Smart Meter",
            "HWE_SKT" => "Energy Socket",
            _ => deviceType
        };
    }
}