using System.Net;
using System.Net.Mail;
using Choosr.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Choosr.Web.Services;

public class EmailReportNotificationService(IConfiguration config, ILogger<EmailReportNotificationService> logger) : IReportNotificationService
{
    public async Task NotifyStatusChangedAsync(ContentReport report, string? actorUserName, CancellationToken cancellationToken = default)
    {
        // Feature toggle
        var enabled = string.Equals(config["Notifications:Reports:Enabled"], "true", StringComparison.OrdinalIgnoreCase);
        if(!enabled) return;

        // We only have ReporterUserName, not email. If email needed, map username->email via Identity or skip.
        // For now, try to send to a configured fallback or skip if no To address template.
        var toTemplate = config["Notifications:Reports:ToTemplate"]; // e.g. "{username}@example.com"
        if(string.IsNullOrWhiteSpace(toTemplate) || string.IsNullOrWhiteSpace(report.ReporterUserName)) return;
        var toAddress = toTemplate.Replace("{username}", report.ReporterUserName!, StringComparison.OrdinalIgnoreCase);

        var smtpHost = config["Notifications:Smtp:Host"];
        var smtpPort = int.TryParse(config["Notifications:Smtp:Port"], out var p) ? p : 587;
        var smtpUser = config["Notifications:Smtp:User"];
        var smtpPass = config["Notifications:Smtp:Pass"];
        var from = config["Notifications:Smtp:From"] ?? smtpUser;
        if(string.IsNullOrWhiteSpace(smtpHost) || string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(toAddress)) return;

        try
        {
            using var client = new SmtpClient(smtpHost, smtpPort)
            {
                EnableSsl = true,
                Credentials = string.IsNullOrWhiteSpace(smtpUser) ? CredentialCache.DefaultNetworkCredentials : new NetworkCredential(smtpUser, smtpPass)
            };
            using var msg = new MailMessage(from!, toAddress)
            {
                Subject = $"Rapor Durumu Güncellendi: {report.Status}",
                Body = $"Merhaba {report.ReporterUserName},\n\nGöndermiş olduğunuz raporun durumu güncellendi.\n\nDurum: {report.Status}\nHedef: {report.TargetType} {report.TargetId}\nNot: {report.ModeratorNotes}\nGüncelleyen: {actorUserName}\n\nTeşekkürler.\n",
                IsBodyHtml = false
            };
            await client.SendMailAsync(msg, cancellationToken);
        }
        catch(Exception ex)
        {
            logger.LogWarning(ex, "Report email notification failed for {User}", report.ReporterUserName);
        }
    }
}
