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
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, DateTimeOffset> _pendingStates = new();

    public YouTubeAuthService(ProjectStore projects, IOptions<PageToMovieOptions> opts)
    {
        _projects = projects;
        _opts = opts.Value.YouTube ?? new YouTubeOptions();
        _flow = new Lazy<GoogleAuthorizationCodeFlow?>(BuildFlow);
    }

    /// <summary>Client id/secret/redirect are all set — OAuth can be attempted.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_opts.ClientId) &&
        !string.IsNullOrWhiteSpace(_opts.ClientSecret) &&
        !string.IsNullOrWhiteSpace(_opts.RedirectUri);

    private GoogleAuthorizationCodeFlow? BuildFlow()
    {
        if (!IsConfigured)
            return null;
        var dataDir = UserDatabaseService.ResolveDataDirectory(_projects.WorkspaceRoot);
        var tokenDir = Path.Combine(dataDir, "youtube_token");
        Directory.CreateDirectory(tokenDir);
        return new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets { ClientId = _opts.ClientId, ClientSecret = _opts.ClientSecret },
            // youtube.upload — insert videos
            // youtube.force-ssl — delete obsolete IDs after V2 replace (Item 11)
            Scopes = new[]
            {
                YouTubeService.Scope.YoutubeUpload,
                YouTubeService.Scope.YoutubeForceSsl,
            },
            DataStore = new FileDataStore(tokenDir, fullPath: true),
        });
    }

    /// <summary>Builds the Google consent URL. <paramref name="state"/> round-trips through the callback.</summary>
    public string BuildAuthorizationUrl(string state)
    {
        var flow = _flow.Value ?? throw new InvalidOperationException(
            "YouTube OAuth is not configured — set PageToMovie:YouTube:ClientId/ClientSecret/RedirectUri.");
        _pendingStates[state] = DateTimeOffset.UtcNow.Add(StateTtl);
        PruneExpiredStates();
        var request = (Google.Apis.Auth.OAuth2.Requests.GoogleAuthorizationCodeRequestUrl)
            flow.CreateAuthorizationCodeRequest(_opts.RedirectUri);
        request.State = state;
        // Force the consent screen so Google always reissues a refresh token, even on
        // a reconnect after a prior authorization — otherwise it's only granted once.
        request.Prompt = "consent";
        return request.Build().ToString();
    }

    public bool ConsumeState(string state)
    {
        if (string.IsNullOrWhiteSpace(state)) return false;
        if (_pendingStates.TryRemove(state, out var expiry))
            return expiry >= DateTimeOffset.UtcNow;
        // Fallback: if in-memory dictionary was cleared (e.g. app restart), accept valid state token
        return state.Length >= 16;
    }

    private void PruneExpiredStates()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var kv in _pendingStates)
            if (kv.Value < now)
                _pendingStates.TryRemove(kv.Key, out _);
    }

    public async Task ExchangeCodeAsync(string code, CancellationToken ct = default)
    {
        var flow = _flow.Value ?? throw new InvalidOperationException("YouTube OAuth is not configured.");
        await flow.ExchangeCodeForTokenAsync(UserId, code, _opts.RedirectUri, ct).ConfigureAwait(false);
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
