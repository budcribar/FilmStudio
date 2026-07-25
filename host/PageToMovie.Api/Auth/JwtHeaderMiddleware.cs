using System.Security.Claims;

namespace PageToMovie.Api.Auth;

/// <summary>
/// Accepts Authorization: Bearer JWT (full session) and short-lived media tokens
/// via query <c>mt</c> for &lt;video&gt;/&lt;img&gt; (cannot send Authorization).
/// Full session JWTs are never accepted from the query string.
/// </summary>
public sealed class JwtHeaderMiddleware
{
    private readonly RequestDelegate _next;

    public JwtHeaderMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx, IAdminAuthService auth)
    {
        if (ctx.User?.Identity?.IsAuthenticated != true)
        {
            ClaimsPrincipal? principal = null;

            var header = ctx.Request.Headers.Authorization.ToString();
            if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                var bearer = header["Bearer ".Length..].Trim();
                if (!string.IsNullOrWhiteSpace(bearer))
                    principal = auth.ValidateToken(bearer);
            }

            // Media-only tokens for element src= (short TTL, token_use=media).
            // Reject full-session JWTs from query to avoid log/history/Referer leakage of long-lived tokens.
            if (principal is null)
            {
                var mediaRaw = TryQueryToken(ctx, "mt")
                               ?? TryQueryToken(ctx, "media_token");
                // Legacy name: still parse but only accept if media-scoped.
                mediaRaw ??= TryQueryToken(ctx, "access_token");

                if (!string.IsNullOrWhiteSpace(mediaRaw))
                {
                    var candidate = auth.ValidateToken(mediaRaw);
                    if (candidate is not null && auth.IsMediaToken(candidate))
                        principal = candidate;
                }
            }

            if (principal is not null)
                ctx.User = principal;
        }

        await _next(ctx);
    }

    private static string? TryQueryToken(HttpContext ctx, string name)
    {
        if (!ctx.Request.Query.TryGetValue(name, out var q) || string.IsNullOrWhiteSpace(q))
            return null;
        return q.ToString().Trim();
    }
}
