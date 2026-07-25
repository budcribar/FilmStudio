using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using PageToMovie.Core.Auth;
using PageToMovie.Core.Models;
using PageToMovie.Core.Options;
using PageToMovie.Engine;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace PageToMovie.Api.Auth;

public interface IAdminAuthService
{
    /// <summary>JWT claim marking short-lived media tokens safe for URL query use.</summary>
    public const string TokenUseClaim = "token_use";
    public const string TokenUseMedia = "media";
    /// <summary>Default media-token lifetime (minutes). Full session JWT must not go in query strings.</summary>
    public const int MediaTokenMinutes = 30;

    LoginResponse Login(string username, string password);
    LoginResponse Signup(string username, string password);
    /// <summary>Issue operator JWT when secret matches PageToMovie_LOGIN_OVERRIDE.</summary>
    LoginResponse LoginWithOperatorOverride(string secret);
    ClaimsPrincipal? ValidateToken(string token);
    /// <summary>True when principal is a short-lived media token (allowed in ?mt=).</summary>
    bool IsMediaToken(ClaimsPrincipal? principal);
    /// <summary>Issue a short-lived media-scoped JWT for &lt;img&gt;/&lt;video&gt; query auth.</summary>
    string IssueMediaToken(ClaimsPrincipal sessionPrincipal);
}

public sealed class AdminAuthService : IAdminAuthService
{
    private readonly AuthOptions _auth;
    private readonly IHostEnvironment _env;
    private readonly UserDatabaseService _userDb;
    private readonly CreditService? _credits;
    private readonly PasswordHasher<object> _hasher = new();
    private readonly object _hashTarget = new();

    public AdminAuthService(
        IOptions<PageToMovieOptions> opts,
        IHostEnvironment env,
        UserDatabaseService userDb,
        CreditService? credits = null)
    {
        _auth = opts.Value.Auth ?? new AuthOptions();
        _env = env;
        _userDb = userDb;
        _credits = credits;
    }

    public LoginResponse Signup(string username, string password)
    {
        username = (username ?? "").Trim();
        password = (password ?? "").Trim();

        if (username.Length < 3)
            return Fail("Username must be at least 3 characters long");
        if (password.Length < 4)
            return Fail("Password must be at least 4 characters long");

        var existing = _userDb.GetUserByUsernameAsync(username).GetAwaiter().GetResult();
        if (existing is not null)
            return Fail("Username is already taken");

        var user = new UserEntity
        {
            UserId = username.ToLowerInvariant(),
            Username = username,
            PasswordHash = UserDatabaseService.HashPassword(password),
            Role = AppRoles.User,
            CreatedAt = DateTime.UtcNow
        };

        _userDb.InsertUserAsync(user).GetAwaiter().GetResult();

        // Signup grant (list-rate credits). Failures are non-fatal.
        _credits?.GrantSignupCreditsAsync(user.UserId).GetAwaiter().GetResult();

        var hours = Math.Clamp(_auth.JwtHours, 1, 168);
        var expires = DateTimeOffset.UtcNow.AddHours(hours);
        var token = IssueJwt(user.Username, new[] { AppRoles.User }, expires);

        return new LoginResponse
        {
            Ok = true,
            Token = token,
            UserId = user.Username,
            Roles = new List<string> { AppRoles.User },
            ExpiresAt = expires,
        };
    }

    public LoginResponse Login(string username, string password)
    {
        username = (username ?? "").Trim();
        password ??= "";

        if (string.IsNullOrWhiteSpace(username))
            return Fail("Username is required");

        // 0. Operator override: password only (never match username — usernames are log-prone).
        if (MatchesOperatorOverride(password))
            return IssueOperatorLogin();

        // 1. Check SQLite database for user
        var dbUser = _userDb.GetUserByUsernameAsync(username).GetAwaiter().GetResult();
        if (dbUser is not null)
        {
            var hash = UserDatabaseService.HashPassword(password);
            if (dbUser.PasswordHash == hash)
            {
                var userRoles = new List<string> { AppRoles.User };
                if (string.Equals(dbUser.Role, "Admin", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(username, _auth.AdminUsername, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(username, OperatorUserId, StringComparison.OrdinalIgnoreCase))
                {
                    userRoles.Add(AppRoles.Admin);
                }

                var userHours = Math.Clamp(_auth.JwtHours, 1, 168);
                var userExpires = DateTimeOffset.UtcNow.AddHours(userHours);
                var userToken = IssueJwt(dbUser.Username, userRoles, userExpires);

                return new LoginResponse
                {
                    Ok = true,
                    Token = userToken,
                    UserId = dbUser.Username,
                    Roles = userRoles,
                    ExpiresAt = userExpires,
                };
            }
            return Fail("Invalid username or password");
        }

        // 2. Fallback check for configured admin / operator user
        if (string.Equals(username, _auth.AdminUsername, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(username, OperatorUserId, StringComparison.OrdinalIgnoreCase))
        {
            var ok = VerifyPassword(password) || MatchesOperatorOverride(password);
            if (!ok)
                return Fail("Invalid username or password");

            return IssueOperatorLogin(username);
        }

        return Fail("Invalid username or password");
    }

    public LoginResponse LoginWithOperatorOverride(string secret)
    {
        if (!MatchesOperatorOverride(secret))
            return Fail("Operator override is not configured or secret does not match.");
        return IssueOperatorLogin();
    }

    private string OperatorUserId =>
        string.IsNullOrWhiteSpace(_auth.OperatorUserId) ? "admin" : _auth.OperatorUserId.Trim();

    private string? ResolveOperatorOverrideSecret()
    {
        var env = Environment.GetEnvironmentVariable("PageToMovie_LOGIN_OVERRIDE")
                  ?? Environment.GetEnvironmentVariable("PAGETOMOVIE_LOGIN_OVERRIDE")
                  ?? Environment.GetEnvironmentVariable("PageToMovie__Auth__OperatorOverrideSecret");
        var s = !string.IsNullOrWhiteSpace(env) ? env.Trim() : (_auth.OperatorOverrideSecret ?? "").Trim();
        // Refuse trivial secrets so a mis-set "1" never opens production.
        // Keep modest (8+) so common operator secrets like Hal576501! work; still blocks single-char accidents.
        if (s.Length < AuthOptions.MinOperatorOverrideSecretLength)
            return null;
        return s;
    }

    private bool MatchesOperatorOverride(string? candidate)
    {
        var secret = ResolveOperatorOverrideSecret();
        if (secret is null || string.IsNullOrEmpty(candidate))
            return false;
        return FixedTimeEquals(secret, candidate);
    }

    private LoginResponse IssueOperatorLogin(string? preferredUserId = null)
    {
        var uid = string.IsNullOrWhiteSpace(preferredUserId)
            ? OperatorUserId
            : preferredUserId.Trim();
        if (string.IsNullOrWhiteSpace(uid))
            uid = "admin";

        var hours = Math.Clamp(_auth.JwtHours, 1, 168);
        var expires = DateTimeOffset.UtcNow.AddHours(hours);
        var token = IssueJwt(uid, new[] { AppRoles.User, AppRoles.Admin }, expires);

        return new LoginResponse
        {
            Ok = true,
            Token = token,
            UserId = uid,
            Roles = new List<string> { AppRoles.User, AppRoles.Admin },
            ExpiresAt = expires,
        };
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        var ba = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);
        if (ba.Length != bb.Length)
            return false;
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(ba, bb);
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, TokenValidationParameters(), out _);
            return principal;
        }
        catch
        {
            return null;
        }
    }

    public bool IsMediaToken(ClaimsPrincipal? principal)
    {
        if (principal?.Identity?.IsAuthenticated != true)
            return false;
        var use = principal.FindFirst(IAdminAuthService.TokenUseClaim)?.Value
                  ?? principal.FindFirst("token_use")?.Value;
        return string.Equals(use, IAdminAuthService.TokenUseMedia, StringComparison.Ordinal);
    }

    public string IssueMediaToken(ClaimsPrincipal sessionPrincipal)
    {
        if (sessionPrincipal?.Identity?.IsAuthenticated != true)
            throw new InvalidOperationException("Not authenticated");

        var userId = sessionPrincipal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? sessionPrincipal.FindFirst("sub")?.Value
                     ?? sessionPrincipal.Identity?.Name
                     ?? "";
        if (string.IsNullOrWhiteSpace(userId))
            throw new InvalidOperationException("No user id on session");

        var roles = sessionPrincipal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        if (roles.Count == 0)
            roles.Add(AppRoles.User);

        var minutes = Math.Clamp(IAdminAuthService.MediaTokenMinutes, 5, 120);
        var expires = DateTimeOffset.UtcNow.AddMinutes(minutes);
        return IssueJwt(userId.Trim(), roles, expires, tokenUse: IAdminAuthService.TokenUseMedia);
    }

    private bool VerifyPassword(string password)
    {
        if (_auth.AllowDevBypass && _env.IsDevelopment())
            return true;

        if (MatchesOperatorOverride(password))
            return true;

        var envPw = Environment.GetEnvironmentVariable("PageToMovie_ADMIN_PASSWORD");
        if (!string.IsNullOrEmpty(envPw) && password == envPw)
            return true;

        if (!string.IsNullOrWhiteSpace(_auth.AdminPasswordHash))
        {
            var r = _hasher.VerifyHashedPassword(_hashTarget, _auth.AdminPasswordHash, password);
            return r is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
        }

        if (!string.IsNullOrEmpty(_auth.AdminPassword))
            return password == _auth.AdminPassword;

        // No password configured: allow only in Development with empty password
        return _env.IsDevelopment() && password.Length == 0;
    }

    private string IssueJwt(
        string userId,
        IEnumerable<string> roles,
        DateTimeOffset expires,
        string? tokenUse = null)
    {
        var key = ResolveSigningKey();
        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, userId),
            new("sub", userId),
        };
        foreach (var r in roles.Distinct(StringComparer.OrdinalIgnoreCase))
            claims.Add(new Claim(ClaimTypes.Role, r));
        if (!string.IsNullOrWhiteSpace(tokenUse))
            claims.Add(new Claim(IAdminAuthService.TokenUseClaim, tokenUse.Trim()));

        var token = new JwtSecurityToken(
            issuer: "PageToMovie.Api",
            audience: "PageToMovie",
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: expires.UtcDateTime,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private TokenValidationParameters TokenValidationParameters() => new()
    {
        ValidateIssuer = true,
        ValidIssuer = "PageToMovie.Api",
        ValidateAudience = true,
        ValidAudience = "PageToMovie",
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ResolveSigningKey())),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(2),
    };

    private string ResolveSigningKey()
    {
        var env = Environment.GetEnvironmentVariable("PageToMovie_JWT_KEY")
                  ?? Environment.GetEnvironmentVariable("PAGETOMOVIE_JWT_KEY")
                  ?? Environment.GetEnvironmentVariable("PageToMovie__Auth__JwtSigningKey")
                  ?? Environment.GetEnvironmentVariable("FILMSTUDIO_JWT_KEY");

        var key = !string.IsNullOrWhiteSpace(env) ? env.Trim() : (_auth.JwtSigningKey ?? "");
        if (AuthOptions.IsInsecureDefaultJwtSigningKey(key) && !_env.IsDevelopment())
        {
            key = System.Security.Cryptography.RandomNumberGenerator.GetString("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*", 64);
            _auth.JwtSigningKey = key;
        }
        if (key.Length < 32)
            key = (key + "PageToMovie-Pad-Key-To-32-Chars!!!!").PadRight(32)[..64];
        return key;
    }

    private static LoginResponse Fail(string error) => new() { Ok = false, Error = error };
}
