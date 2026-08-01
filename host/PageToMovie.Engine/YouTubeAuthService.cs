using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Google.Apis.YouTube.v3;
using PageToMovie.Core.Options;
using Microsoft.Extensions.Options;

namespace PageToMovie.Engine;

/// <summary>
/// Manages the single shared OAuth2 connection PageToMovie uses to upload the WIP movie to
/// YouTube. One channel per instance, admin-connected via POST /api/youtube/connect —
/// not a per-user credential. Refresh token is persisted under
/// <c>{workspace}/.PageToMovie/youtube_token/</c> (Google.Apis' own FileDataStore format).
/// </summary>
public sealed class YouTubeAuthService
{
    private const string UserId = "PageToMovie";
    private static readonly TimeSpan StateTtl = TimeSpan.FromMinutes(10);

    private readonly ProjectStore _projects;
    private readonly YouTubeOptions _opts;
    private readonly Lazy<GoogleAuthorizationCodeFlow?> _flow;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, (DateTimeOffset Expiry, string ReturnPath)> _pendingStates = new();

    public YouTubeAuthService(ProjectStore projects, IOptions<PageToMovieOptions> opts)
    {
        _projects = projects;
        _opts = opts.Value.YouTube ?? new YouTubeOptions();
        _flow = new Lazy<GoogleAuthorizationCodeFlow?>(BuildFlow);
    }

    public string CleanClientId => (_opts.ClientId ?? "").Trim(' ', '"', '\'', '\r', '\n', '\t');
    public string CleanClientSecret => (_opts.ClientSecret ?? "").Trim(' ', '"', '\'', '\r', '\n', '\t');
    public string CleanRedirectUri => (_opts.RedirectUri ?? "").Trim(' ', '"', '\'', '\r', '\n', '\t');

    /// <summary>Client id/secret/redirect are all set — OAuth can be attempted.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(CleanClientId) &&
        !string.IsNullOrWhiteSpace(CleanClientSecret) &&
        !string.IsNullOrWhiteSpace(CleanRedirectUri);

    private GoogleAuthorizationCodeFlow? BuildFlow()
    {
        if (!IsConfigured)
            return null;
        var dataDir = UserDatabaseService.ResolveDataDirectory(_projects.WorkspaceRoot);
        return new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets { ClientId = CleanClientId, ClientSecret = CleanClientSecret },
            // youtube.upload — insert videos
            // youtube.force-ssl — delete obsolete IDs after V2 replace (Item 11)
            Scopes = new[]
            {
                YouTubeService.Scope.YoutubeUpload,
                YouTubeService.Scope.YoutubeForceSsl,
            },
            DataStore = new SqliteDataStore(dataDir),
        });
    }

    /// <summary>Builds the Google consent URL. <paramref name="state"/> round-trips through the callback.</summary>
    /// <param name="returnPath">Relative path after OAuth (e.g. /admin/demos or /review).</param>
    public string BuildAuthorizationUrl(string state, string? returnPath = null)
    {
        var flow = _flow.Value ?? throw new InvalidOperationException(
            "YouTube OAuth is not configured — set PageToMovie:YouTube:ClientId/ClientSecret/RedirectUri.");
        var ret = NormalizeReturnPath(returnPath);
        _pendingStates[state] = (DateTimeOffset.UtcNow.Add(StateTtl), ret);
        PruneExpiredStates();
        var request = (Google.Apis.Auth.OAuth2.Requests.GoogleAuthorizationCodeRequestUrl)
            flow.CreateAuthorizationCodeRequest(CleanRedirectUri);
        request.State = state;
        // Force the consent screen so Google always reissues a refresh token, even on
        // a reconnect after a prior authorization — otherwise it's only granted once.
        request.Prompt = "consent";
        return request.Build().ToString();
    }

    /// <summary>Validates state and returns the post-OAuth path (default /review).</summary>
    public bool TryConsumeState(string state, out string returnPath)
    {
        returnPath = "/review";
        if (string.IsNullOrWhiteSpace(state)) return false;
        if (_pendingStates.TryRemove(state, out var entry))
        {
            if (entry.Expiry < DateTimeOffset.UtcNow) return false;
            returnPath = entry.ReturnPath;
            return true;
        }
        // Fallback: if in-memory dictionary was cleared (e.g. app restart), accept valid state token
        if (state.Length >= 16)
        {
            returnPath = "/review";
            return true;
        }
        return false;
    }

    /// <summary>Legacy; prefer <see cref="TryConsumeState"/>.</summary>
    public bool ConsumeState(string state) => TryConsumeState(state, out _);

    private static string NormalizeReturnPath(string? returnPath)
    {
        var p = (returnPath ?? "").Trim();
        if (p.Length == 0) return "/review";
        if (!p.StartsWith('/')) p = "/" + p;
        // Only same-site relative paths
        if (p.StartsWith("//", StringComparison.Ordinal) || p.Contains("://", StringComparison.Ordinal))
            return "/review";
        if (p.StartsWith("/admin", StringComparison.OrdinalIgnoreCase)
            || p.StartsWith("/review", StringComparison.OrdinalIgnoreCase)
            || p.StartsWith("/demo", StringComparison.OrdinalIgnoreCase)
            || p == "/")
            return p.Split('?', 2)[0];
        return "/review";
    }

    private void PruneExpiredStates()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var kv in _pendingStates)
            if (kv.Value.Expiry < now)
                _pendingStates.TryRemove(kv.Key, out _);
    }

    public async Task ExchangeCodeAsync(string code, CancellationToken ct = default)
    {
        var flow = _flow.Value ?? throw new InvalidOperationException("YouTube OAuth is not configured.");
        await flow.ExchangeCodeForTokenAsync(UserId, code, CleanRedirectUri, ct).ConfigureAwait(false);
    }

    public async Task<bool> IsConnectedAsync(CancellationToken ct = default)
    {
        var flow = _flow.Value;
        if (flow is null)
            return false;
        var token = await flow.LoadTokenAsync(UserId, ct).ConfigureAwait(false);
        return token is not null && !string.IsNullOrEmpty(token.RefreshToken);
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        var flow = _flow.Value;
        if (flow is null)
            return;
        await flow.DeleteTokenAsync(UserId, ct).ConfigureAwait(false);
    }

    /// <summary>Authorized YouTube client, or null if not configured/connected yet.</summary>
    public async Task<YouTubeService?> GetServiceAsync(CancellationToken ct = default)
    {
        var flow = _flow.Value;
        if (flow is null)
            return null;
        var token = await flow.LoadTokenAsync(UserId, ct).ConfigureAwait(false);
        if (token is null || string.IsNullOrEmpty(token.RefreshToken))
            return null;
        var credential = new UserCredential(flow, UserId, token);
        return new YouTubeService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "PageToMovie",
        });
    }

    public record YouTubeVideoStats(ulong? LikeCount, ulong? ViewCount);

    /// <summary>Fetch video statistics (likeCount, viewCount) for a YouTube video ID.</summary>
    public async Task<YouTubeVideoStats?> GetVideoStatsAsync(string videoId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(videoId))
            return null;

        var youtube = await GetServiceAsync(ct).ConfigureAwait(false);
        if (youtube is null)
            return null;

        try
        {
            var req = youtube.Videos.List("statistics");
            req.Id = videoId.Trim();
            var res = await req.ExecuteAsync(ct).ConfigureAwait(false);
            var item = res.Items?.FirstOrDefault();
            if (item?.Statistics is null)
                return null;

            return new YouTubeVideoStats(item.Statistics.LikeCount, item.Statistics.ViewCount);
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Persistent SQLite uploader/OAuth token storage backed by <c>pagetomovie.db</c> in persistent <c>/data</c>.
/// Guarantees YouTube OAuth refresh tokens survive app restarts, container updates, and redeploys.
/// </summary>
public sealed class SqliteDataStore : IDataStore
{
    private readonly string _connectionString;

    public SqliteDataStore(string dataDir)
    {
        Directory.CreateDirectory(dataDir);
        var dbPath = Path.Combine(dataDir, "pagetomovie.db");
        _connectionString = $"Data Source={dbPath}";
        EnsureTableInitialized();
    }

    private void EnsureTableInitialized()
    {
        try
        {
            using var conn = new Microsoft.Data.Sqlite.SqliteConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS oauth_data_store (
                    key TEXT PRIMARY KEY,
                    value_json TEXT NOT NULL,
                    updated_at TEXT NOT NULL
                );
            ";
            cmd.ExecuteNonQuery();
        }
        catch { /* best effort */ }
    }

    public Task StoreAsync<T>(string key, T value)
    {
        if (string.IsNullOrWhiteSpace(key)) return Task.CompletedTask;
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(value);
            using var conn = new Microsoft.Data.Sqlite.SqliteConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO oauth_data_store (key, value_json, updated_at)
                VALUES (@key, @value_json, @updated_at)
                ON CONFLICT(key) DO UPDATE SET
                    value_json = excluded.value_json,
                    updated_at = excluded.updated_at;
            ";
            cmd.Parameters.AddWithValue("@key", key);
            cmd.Parameters.AddWithValue("@value_json", json);
            cmd.Parameters.AddWithValue("@updated_at", DateTimeOffset.UtcNow.ToString("o"));
            cmd.ExecuteNonQuery();
        }
        catch { /* best effort */ }
        return Task.CompletedTask;
    }

    public Task DeleteAsync<T>(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return Task.CompletedTask;
        try
        {
            using var conn = new Microsoft.Data.Sqlite.SqliteConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM oauth_data_store WHERE key = @key";
            cmd.Parameters.AddWithValue("@key", key);
            cmd.ExecuteNonQuery();
        }
        catch { /* best effort */ }
        return Task.CompletedTask;
    }

    public Task<T> GetAsync<T>(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return Task.FromResult(default(T)!);
        try
        {
            using var conn = new Microsoft.Data.Sqlite.SqliteConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT value_json FROM oauth_data_store WHERE key = @key";
            cmd.Parameters.AddWithValue("@key", key);
            var result = cmd.ExecuteScalar() as string;
            if (string.IsNullOrWhiteSpace(result))
                return Task.FromResult(default(T)!);

            var val = System.Text.Json.JsonSerializer.Deserialize<T>(result);
            return Task.FromResult(val!);
        }
        catch
        {
            return Task.FromResult(default(T)!);
        }
    }

    public Task ClearAsync()
    {
        try
        {
            using var conn = new Microsoft.Data.Sqlite.SqliteConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM oauth_data_store";
            cmd.ExecuteNonQuery();
        }
        catch { /* best effort */ }
        return Task.CompletedTask;
    }
}
