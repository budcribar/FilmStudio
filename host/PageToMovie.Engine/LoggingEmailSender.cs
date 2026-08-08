using Microsoft.Extensions.Logging;
using PageToMovie.Engine.Abstractions;
using PageToMovie.Engine.Collaboration;

namespace PageToMovie.Engine;

public sealed class LoggingEmailSender : IEmailSender, IProjectInviteMailer
{
    private readonly ILogger<LoggingEmailSender> _log;
    public LoggingEmailSender(ILogger<LoggingEmailSender> log) => _log = log;

    public Task SendAsync(string toEmail, string subject, string htmlBody, string? textBody = null, CancellationToken ct = default)
    {
        _log.LogInformation("EMAIL to={To} subject={Subject}\n{Body}", toEmail, subject, textBody ?? htmlBody);
        return Task.CompletedTask;
    }

    public Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        _log.LogInformation("EMAIL to={To} subject={Subject}\n{Body}", to, subject, body);
        return Task.CompletedTask;
    }
}
