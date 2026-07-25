using Microsoft.Extensions.Logging;
using PageToMovie.Engine.Abstractions;

namespace PageToMovie.Engine;

/// <summary>Dev/default: writes messages to the log (no SMTP).</summary>
public sealed class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _log;

    public LoggingEmailSender(ILogger<LoggingEmailSender> log) => _log = log;

    public Task SendAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string? textBody = null,
        CancellationToken ct = default)
    {
        _log.LogInformation(
            "EMAIL (log-only) To={To} Subject={Subject}\n{Body}",
            toEmail,
            subject,
            textBody ?? htmlBody);
        return Task.CompletedTask;
    }
}
