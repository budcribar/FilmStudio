namespace PageToMovie.Engine.Abstractions;

/// <summary>Minimal outbound email (confirm address, password reset).</summary>
public interface IEmailSender
{
    Task SendAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string? textBody = null,
        CancellationToken ct = default);
}
