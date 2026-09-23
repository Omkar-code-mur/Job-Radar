using System.Net;
using System.Net.Mail;

namespace JobRadar.Api;

public sealed class BulkEmailService(IConfiguration configuration, ILogger<BulkEmailService> logger)
{
    public async Task<BulkEmailSendResult> SendAsync(BulkEmailSendRequest request, CancellationToken ct)
    {
        if (request.Contacts is null || request.Contacts.Count is < 1 or > 50)
            throw new ArgumentException("Each send must contain between 1 and 50 contacts.");
        if (string.IsNullOrWhiteSpace(request.Subject) || string.IsNullOrWhiteSpace(request.Body))
            throw new ArgumentException("Subject and body are required.");

        var host = configuration["SMTP_HOST"] ?? Environment.GetEnvironmentVariable("SMTP_HOST");
        var username = configuration["SMTP_USERNAME"] ?? Environment.GetEnvironmentVariable("SMTP_USERNAME");
        var password = configuration["SMTP_PASSWORD"] ?? Environment.GetEnvironmentVariable("SMTP_PASSWORD");
        var fromEmail = configuration["SMTP_FROM_EMAIL"] ?? Environment.GetEnvironmentVariable("SMTP_FROM_EMAIL") ?? username;
        var fromName = configuration["SMTP_FROM_NAME"] ?? Environment.GetEnvironmentVariable("SMTP_FROM_NAME") ?? "Job Radar";
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(fromEmail))
            throw new InvalidOperationException("SMTP is not configured. Set SMTP_HOST, SMTP_USERNAME, SMTP_PASSWORD and SMTP_FROM_EMAIL.");

        var portText = configuration["SMTP_PORT"] ?? Environment.GetEnvironmentVariable("SMTP_PORT");
        var port = int.TryParse(portText, out var parsedPort) ? parsedPort : 587;
        var useSslText = configuration["SMTP_USE_SSL"] ?? Environment.GetEnvironmentVariable("SMTP_USE_SSL");
        var useSsl = !string.Equals(useSslText, "false", StringComparison.OrdinalIgnoreCase);

        using var smtp = new SmtpClient(host, port) {
            EnableSsl = useSsl,
            Credentials = new NetworkCredential(username, password),
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        var sent = new List<string>();
        var failed = new List<BulkEmailFailure>();
        foreach (var contact in request.Contacts)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(contact.Email) || string.IsNullOrWhiteSpace(contact.Name) || string.IsNullOrWhiteSpace(contact.Company))
            {
                failed.Add(new(contact.Email ?? string.Empty, "Missing name, email, or company."));
                continue;
            }

            var subject = Render(request.Subject, contact);
            var body = Render(request.Body, contact);
            try
            {
                using var message = new MailMessage {
                    From = new MailAddress(fromEmail, fromName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = false
                };
                message.To.Add(new MailAddress(contact.Email, contact.Name));
                await smtp.SendMailAsync(message, ct);
                sent.Add(contact.Email);
                logger.LogInformation("Bulk outreach email sent to {Recipient}", contact.Email);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Bulk outreach email failed for {Recipient}", contact.Email);
                failed.Add(new(contact.Email, ex.Message));
            }
        }

        return new BulkEmailSendResult(sent.Count, failed.Count, sent, failed);
    }

    private static string Render(string template, BulkEmailContact contact)
    {
        var firstName = contact.Name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? contact.Name;
        return template
            .Replace("{{firstName}}", firstName, StringComparison.OrdinalIgnoreCase)
            .Replace("{{name}}", contact.Name, StringComparison.OrdinalIgnoreCase)
            .Replace("{{company}}", contact.Company, StringComparison.OrdinalIgnoreCase)
            .Replace("{{role}}", contact.Role ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }
}

public record BulkEmailContact(string Name, string Email, string? Role, string Company);
public record BulkEmailSendRequest(IReadOnlyList<BulkEmailContact> Contacts, string Subject, string Body);
public record BulkEmailFailure(string Email, string Error);
public record BulkEmailSendResult(int Sent, int Failed, IReadOnlyList<string> SentEmails, IReadOnlyList<BulkEmailFailure> Failures);