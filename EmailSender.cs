using System.Net;
using System.Net.Mail;

namespace WPUService;

internal sealed class EmailSettings
{
    public string Host { get; init; } = "";
    public int Port { get; init; }
    public bool UseSsl { get; init; }
    public string Username { get; init; } = "";
    public string Password { get; init; } = "";
    public string From { get; init; } = "";
    public string To { get; init; } = "";

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Host) &&
        Port > 0 &&
        !string.IsNullOrWhiteSpace(Username) &&
        !string.IsNullOrWhiteSpace(Password) &&
        !string.IsNullOrWhiteSpace(From) &&
        !string.IsNullOrWhiteSpace(To);
}

internal static class EmailSender
{
    public static async Task<(bool Ok, string Error)> SendAsync(EmailSettings s, string subject, string body)
    {
        if (!s.IsValid) return (false, "Email settings are incomplete.");
        try
        {
            using var msg = new MailMessage();
            msg.From = new MailAddress(s.From, "WPUService");
            msg.To.Add(new MailAddress(s.To));
            msg.Subject = subject ?? "";
            msg.Body = body ?? "";
            msg.IsBodyHtml = false;

#pragma warning disable SYSLIB0014
            using var client = new SmtpClient(s.Host, s.Port)
            {
                EnableSsl = s.UseSsl,
                Credentials = new NetworkCredential(s.Username, s.Password),
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = 15000,
            };
            await client.SendMailAsync(msg).ConfigureAwait(false);
#pragma warning restore SYSLIB0014
            return (true, "");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}
