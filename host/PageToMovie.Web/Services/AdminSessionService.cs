using System.Text.Json;
using Microsoft.JSInterop;

namespace PageToMovie.Web.Services;

/// <summary>
/// Scoped admin identity (Blazor WASM scope = browser tab). Mirrors JWT into
/// sessionStorage via JS so a full page reload can restore the session.
/// Persist/clear are awaited before forceLoad navigations so the old session
/// cannot win a race and reappear (e.g. after renaming a user in the DB).
/// </summary>
public sealed class AdminSessionService
{
    private const string StorageKey = "PageToMovie.admin.session";
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly IJSRuntime? _js;
    private bool _hydrated;

    public AdminSessionService(IJSRuntime? js = null) => _js = js;

    public string? Token { get; private set; }
    public string UserId { get; private set; } = "local";
    public IReadOnlyList<string> Roles { get; private set; } = Array.Empty<string>();
    public DateTimeOffset? ExpiresAt { get; private set; }

    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(Token);
    public bool IsLoggedIn => IsAuthenticated;
    public bool IsAdmin =>
        Roles.Any(r => string.Equals(r, "admin", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Public @handle for UI (never show raw email). If an old account used email as username,
    /// shows the local-part only as a best-effort display handle.
    /// </summary>
    public string DisplayHandle
    {
        get
        {
            var id = (UserId ?? "").Trim();
            if (id.Length == 0 || string.Equals(id, "local", StringComparison.OrdinalIgnoreCase))
                return "@local";
            if (id.Contains('@', StringComparison.Ordinal))
            {
                var at = id.IndexOf('@');
                if (at > 0) id = id[..at];
            }
            return id.StartsWith('@') ? id : "@" + id;
        }
    }

    public event Action? Changed;

    public void SetUserId(string? userId)
    {
        UserId = string.IsNullOrWhiteSpace(userId) ? "local" : userId.Trim();
        Changed?.Invoke();
        _ = PersistAsync();
    }

    /// <summary>In-memory update only. Prefer <see cref="SetSessionAsync"/> before forceLoad.</summary>
    public void SetSession(string token, string? userId, IEnumerable<string>? roles, DateTimeOffset? expiresAt)
    {
        ApplySession(token, userId, roles, expiresAt);
        _ = PersistAsync();
    }

    /// <summary>Write session to sessionStorage and wait (safe before forceLoad navigate).</summary>
    public async Task SetSessionAsync(
        string token,
        string? userId,
        IEnumerable<string>? roles,
        DateTimeOffset? expiresAt)
    {
        ApplySession(token, userId, roles, expiresAt);
        await PersistAsync().ConfigureAwait(false);
    }

    private void ApplySession(
        string token,
        string? userId,
        IEnumerable<string>? roles,
        DateTimeOffset? expiresAt)
    {
        Token = token;
        UserId = string.IsNullOrWhiteSpace(userId) ? "local" : userId.Trim();
        Roles = roles?.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                ?? new List<string>();
        ExpiresAt = expiresAt;
        _hydrated = true;
        Changed?.Invoke();
    }

    /// <summary>In-memory clear only. Prefer <see cref="ClearAsync"/> before forceLoad.</summary>
    public void Clear()
    {
        ApplyClear();
        _ = ClearPersistAsync();
    }

    /// <summary>Remove session from sessionStorage and wait (safe before forceLoad navigate).</summary>
    public async Task ClearAsync()
    {
        ApplyClear();
        await ClearPersistAsync().ConfigureAwait(false);
    }

    private void ApplyClear()
    {
        Token = null;
        UserId = "local";
        Roles = Array.Empty<string>();
        ExpiresAt = null;
        _hydrated = true;
        Changed?.Invoke();
    }

    /// <summary>
    /// Restore from sessionStorage. Safe only after interactive render (JS available).
    /// Never throws; never hangs longer than a quick JS call.
    /// </summary>
    public async Task EnsureHydratedAsync()
    {
        if (_hydrated)
            return;

        if (_js is null)
        {
            _hydrated = true;
            return;
        }

        try
        {
            var json = await _js.InvokeAsync<string?>("sessionStorage.getItem", StorageKey);
            if (!string.IsNullOrWhiteSpace(json))
            {
                var s = JsonSerializer.Deserialize<StoredSession>(json, JsonOpts);
                if (s is not null && !string.IsNullOrWhiteSpace(s.Token))
                {
                    if (s.ExpiresAt is DateTimeOffset exp && exp < DateTimeOffset.UtcNow)
                    {
                        await ClearPersistAsync();
                    }
                    else
                    {
                        Token = s.Token;
                        UserId = string.IsNullOrWhiteSpace(s.UserId) ? "local" : s.UserId!;
                        Roles = s.Roles?.ToList() ?? new List<string>();
                        ExpiresAt = s.ExpiresAt;
                    }
                }
            }
        }
        catch
        {
            // JS not ready or disabled
        }
        finally
        {
            _hydrated = true;
        }
    }

    private async Task PersistAsync()
    {
        if (_js is null || string.IsNullOrWhiteSpace(Token))
            return;
        try
        {
            var json = JsonSerializer.Serialize(new StoredSession
            {
                Token = Token,
                UserId = UserId,
                Roles = Roles.ToList(),
                ExpiresAt = ExpiresAt,
            }, JsonOpts);
            await _js.InvokeVoidAsync("sessionStorage.setItem", StorageKey, json);
        }
        catch
        {
            // ignore
        }
    }

    private async Task ClearPersistAsync()
    {
        if (_js is null) return;
        try { await _js.InvokeVoidAsync("sessionStorage.removeItem", StorageKey); }
        catch { /* ignore */ }
    }

    private sealed class StoredSession
    {
        public string? Token { get; set; }
        public string? UserId { get; set; }
        public List<string>? Roles { get; set; }
        public DateTimeOffset? ExpiresAt { get; set; }
    }
}
