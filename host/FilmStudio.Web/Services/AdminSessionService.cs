namespace FilmStudio.Web.Services;

/// <summary>Circuit-scoped admin JWT session (Phase B).</summary>
public sealed class AdminSessionService
{
    public string? Token { get; private set; }
    public string? UserId { get; private set; }
    public IReadOnlyList<string> Roles { get; private set; } = Array.Empty<string>();
    public DateTimeOffset? ExpiresAt { get; private set; }

    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(Token);
    public bool IsAdmin =>
        Roles.Any(r => string.Equals(r, "admin", StringComparison.OrdinalIgnoreCase));

    public event Action? Changed;

    public void SetSession(string token, string? userId, IEnumerable<string>? roles, DateTimeOffset? expiresAt)
    {
        Token = token;
        UserId = userId;
        Roles = roles?.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                ?? new List<string>();
        ExpiresAt = expiresAt;
        Changed?.Invoke();
    }

    public void Clear()
    {
        Token = null;
        UserId = null;
        Roles = Array.Empty<string>();
        ExpiresAt = null;
        Changed?.Invoke();
    }
}
