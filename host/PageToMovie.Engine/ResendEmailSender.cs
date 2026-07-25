using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PageToMovie.Core.Options;
using PageToMovie.Engine.Abstractions;

namespace PageToMovie.Engine;

/// <summary>
/// Sends mail via Resend HTTPS API (<c>POST https://api.resend.com/emails</c>).
/// Preferred on Railway (SMTP ports often blocked on Hobby).
/// </summary>
public sealed class ResendEmailSender : IEmailSender
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly MailOptions _mail;
    private readonly string _apiKey;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<ResendEmailSender> _log;

    public ResendEmailSender(
        IOptions<PageToMovieOptions> opts,
        IHttpClientFactory httpFactory,
        ILogger<ResendEmailSender> log)
    {
        _mail = opts.Value.Mail ?? new MailOptions();
        _apiKey = MailOptions.ResolveResendApiKey(_mail)
                  ?? throw new InvalidOperationException("Resend API key is not configured.");
        _httpFactory = httpFactory;
        _log = log;
    }

    public async Task SendAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string? textBody = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
            throw new ArgumentException("Recipient email is required.", nameof(toEmail));

        var fromAddr = string.IsNullOrWhiteSpace(_mail.FromAddress)
            ? "onboarding@resend.dev"
            : _mail.FromAddress.Trim();
        var fromName = string.IsNullOrWhiteSpace(_mail.FromName) ? "PageToMovie" : _mail.FromName.Trim();
        var from = $"{fromName} <{fromAddr}>";

        var payload = new ResendSendRequest
        {
            From = from,
            To = new[] { toEmail.Trim() },
            Subject = subject ?? "",
            Html = string.IsNullOrWhiteSpace(htmlBody) ? null : htmlBody,
            Text = string.IsNullOrWhiteSpace(textBody) ? null : textBody,
            ReplyTo = string.IsNullOrWhiteSpace(_mail.ReplyTo) ? null : _mail.ReplyTo.Trim(),
        };
        // Resend requires at least html or text
        if (payload.Html is null && payload.Text is null)
            payload.Text = subject ?? "(no body)";

        var client = _httpFactory.CreateClient("resend");
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails")
        {
            Content = JsonContent.Create(payload, options: JsonOpts),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        HttpResponseMessage resp;
        try
        {
            resp = await client.SendAsync(req, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Resend request failed To={To}", toEmail);
            throw;
        }

        var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            _log.LogError(
                "Resend send failed To={To} Status={Status} Body={Body}",
                toEmail,
                (int)resp.StatusCode,
                body);
            throw new InvalidOperationException(
                $"Resend email failed ({(int)resp.StatusCode}): {Truncate(body, 300)}");
        }

        _log.LogInformation(
            "Resend email sent To={To} Subject={Subject} Response={Body}",
            toEmail,
            subject,
            Truncate(body, 120));
    }

    private static string Truncate(string? s, int max)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = s.Replace('\n', ' ').Replace('\r', ' ');
        return s.Length <= max ? s : s[..max] + "…";
    }

    private sealed class ResendSendRequest
    {
        [JsonPropertyName("from")]
        public string From { get; set; } = "";

        [JsonPropertyName("to")]
        public string[] To { get; set; } = Array.Empty<string>();

        [JsonPropertyName("subject")]
        public string Subject { get; set; } = "";

        [JsonPropertyName("html")]
        public string? Html { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("reply_to")]
        public string? ReplyTo { get; set; }
    }
}
