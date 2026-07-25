using System.Security.Claims;

namespace PageToMovie.Api.Auth;

/// <summary>
/// Accepts Authorization: Bearer JWT and populates HttpContext.User (admin or future users).
/// </summary>
public sealed class JwtHeaderMiddleware
{
    private readonly RequestDelegate _next;

    public JwtHeaderMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx, IAdminAuthService auth)
    {
        if (ctx.User?.Identity?.IsAuthenticated != true)
        {
            string? token = null;
            var header = ctx.Request.Headers.Authorization.ToString();
            if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                token = header["Bearer ".Length..].Trim();
            // <video>/<img src> cannot send Authorization — allow JWT as query for media URLs.
            if (string.IsNullOrWhiteSpace(token) &&
                ctx.Request.Query.TryGetValue("access_token", out var q) &&
                !string.IsNullOrWhiteSpace(q))
                token = q.ToString().Trim();

            if (!string.IsNullOrWhiteSpace(token))
            {
                var principal = auth.ValidateToken(token);
                if (principal is not null)
                    ctx.User = principal;
            }
        }

        await _next(ctx);
    }
}
