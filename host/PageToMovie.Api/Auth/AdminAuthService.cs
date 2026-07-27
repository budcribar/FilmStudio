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
    LoginResponse Signup(string username, string password, string? email = null);
    /// <summary>Issue operator JWT when secret matches PageToMovie_LOGIN_OVERRIDE.</summary>
    LoginResponse LoginWithOperatorOverride(string secret);
    ClaimsPrincipal? ValidateToken(string token);
    /// <summary>True when principal is a short-lived media token (allowed in ?mt=).</summary>
    bool IsMediaToken(ClaimsPrincipal? principal);
    /// <summary>Issue a short-lived media-scoped JWT for &lt;img&gt;/&lt;video&gt; query auth.</summary>
    string IssueMediaToken(ClaimsPrincipal sessionPrincipal);
    /// <summary>
    /// Verify password for the acting admin: operator override secret, DB user hash,
    /// or configured admin password for the operator account.
    /// </summary>
    bool VerifyCallerPassword(string callerUserId, string password);
}

public sealed class AdminAuthService : IAdminAuthService
{
    private readonly AuthOptions _auth;
    private readonly MailOptions _mail;
    private readonly IHostEnvironment _env;
    private readonly UserDatabaseService _userDb;
    private readonly CreditService? _credits;
    private readonly PageToMovie.Engine.Abstractions.IEmailSender? _email;
    private readonly PasswordHasher<object> _hasher = new();
    private readonly object _hashTarget = new();

    public AdminAuthService(
        IOptions<PageToMovieOptions> opts,
        IHostEnvironment env,
        UserDatabaseService userDb,
        CreditService? credits = null,
        PageToMovie.Engine.Abstractions.IEmailSender? email = null)
    {
        _auth = opts.Value.Auth ?? new AuthOptions();
        _mail = opts.Value.Mail ?? new MailOptions();
        _env = env;
        _userDb = userDb;
        _credits = credits;
        _email = email;
    }

    public LoginResponse Signup(string username, string password, string? email = null)
    {
        username = (username ?? "").Trim();
        password = (password ?? "").Trim();
        email = UserDatabaseService.NormalizeEmail(email);

        if (username.Length < 3)
            return Fail("Username must be at least 3 characters long");
        if (username.Contains('@', StringComparison.Ordinal))
            return Fail("Choose a public handle (not an email). Use the email field for your address.");
        if (password.Length < 4)
            return Fail("Password must be at least 4 characters long");
        if (!UserDatabaseService.IsValidEmail(email))
            return Fail("A valid email address is required");

        var existing = _userDb.GetUserByUsernameAsync(username).GetAwaiter().GetResult();
        if (existing is not null)
            return Fail("Username is already taken");
        var byEmail = _userDb.GetUserByEmailAsync(email!).GetAwaiter().GetResult();
        if (byEmail is not null)
            return Fail("That email is already registered");

        var user = new UserEntity
        {
            UserId = username.ToLowerInvariant(),
            Username = username,
            PasswordHash = UserDatabaseService.HashPassword(password),
            Email = email,
            EmailConfirmedAt = null,
            Role = AppRoles.User,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            _userDb.InsertUserAsync(user).GetAwaiter().GetResult();
        }
        catch (Exception)
        {
            return Fail("Could not create account (username or email may already be in use)");
        }

        // Signup grant (list-rate credits). Failures are non-fatal.
        _credits?.GrantSignupCreditsAsync(user.UserId).GetAwaiter().GetResult();

        try
        {
            SendEmailConfirmAsync(user).GetAwaiter().GetResult();
        }
        catch
        {
            // Account exists; user can request resend
        }

        return new LoginResponse
        {
            Ok = true,
            RequiresEmailConfirmation = true,
            UserId = user.Username,
            Message =
                "Account created. Check your email for a confirmation link before signing in. " +
                "(In development, confirmation links are written to the API log if SMTP is not configured.)",
        };
    }

    public async Task SendEmailConfirmAsync(UserEntity user)
    {
        if (user is null || string.IsNullOrWhiteSpace(user.Email)) return;
        var raw = await _userDb.CreateAuthTokenAsync(
            user.UserId, UserDatabaseService.AuthPurposeEmailConfirm, TimeSpan.FromDays(2));
        var link = BuildAppLink($"/login?confirmEmail={Uri.EscapeDataString(raw)}");
        var subject = "Confirm your PageToMovie email";
        var text = $"Hi {user.Username},\n\nConfirm your email:\n{link}\n\nThis link expires in 48 hours.\n";
        var html = $"<p>Hi {System.Net.WebUtility.HtmlEncode(user.Username)},</p>" +
                   $"<p><a href=\"{System.Net.WebUtility.HtmlEncode(link)}\">Confirm your email</a></p>" +
                   "<p>This link expires in 48 hours.</p>";
        if (_email is not null)
            await _email.SendAsync(user.Email!, subject, html, text);
    }

    public async Task SendPasswordResetEmailAsync(UserEntity user)
    {
        if (user is null || string.IsNullOrWhiteSpace(user.Email)) return;
        var raw = await _userDb.CreateAuthTokenAsync(
            user.UserId, UserDatabaseService.AuthPurposePasswordReset, TimeSpan.FromHours(1));
        var link = BuildAppLink($"/login?resetToken={Uri.EscapeDataString(raw)}");
        var subject = "Reset your PageToMovie password";
        var text = $"Hi {user.Username},\n\nReset your password:\n{link}\n\nThis link expires in 1 hour.\n";
        var html = $"<p>Hi {System.Net.WebUtility.HtmlEncode(user.Username)},</p>" +
                   $"<p><a href=\"{System.Net.WebUtility.HtmlEncode(link)}\">Reset your password</a></p>" +
                   "<p>This link expires in 1 hour. If you did not request this, ignore this email.</p>";
        if (_email is not null)
            await _email.SendAsync(user.Email!, subject, html, text);
    }

    /// <summary>Public site URL for a path (Railway domain auto-detected; see fallback chain below).</summary>
    public string BuildAppLink(string pathAndQuery)
    {
        var bas = (_mail.PublicBaseUrl ?? "").Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(bas))
        {
            bas = Environment.GetEnvironmentVariable("PAGETOMOVIE_PUBLIC_BASE_URL")?.Trim().TrimEnd('/')
                  ?? Environment.GetEnvironmentVariable("PageToMovie_PUBLIC_BASE_URL")?.Trim().TrimEnd('/')
                  ?? Environment.GetEnvironmentVariable("PUBLIC_BASE_URL")?.Trim().TrimEnd('/');
        }
        if (string.IsNullOrWhiteSpace(bas))
        {
            var railwayDomain = Environment.GetEnvironmentVariable("RAILWAY_PUBLIC_DOMAIN")?.Trim().TrimEnd('/')
                                ?? Environment.GetEnvironmentVariable("RAILWAY_STATIC_URL")?.Trim().TrimEnd('/');
            if (!string.IsNullOrWhiteSpace(railwayDomain))
            {
                bas = railwayDomain.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? railwayDomain
                    : "https://" + railwayDomain;
            }
        }
        if (string.IsNullOrWhiteSpace(bas))
        {
            bas = "https://pagetomovie-production.up.railway.app";
        }
        if (!pathAndQuery.StartsWith('/'))
            pathAndQuery = "/" + pathAndQuery;
        return bas + pathAndQuery;
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

        // 1. Check SQLite database for user (username or email — session always stores public handle)
        var dbUser = _userDb.GetUserByUsernameAsync(username).GetAwaiter().GetResult()
                     ?? (username.Contains('@', StringComparison.Ordinal)
                         ? _userDb.GetUserByEmailAsync(username).GetAwaiter().GetResult()
                         : null);
        if (dbUser is not null)
        {
            if (dbUser.IsDisabled)
                return Fail("This account has been disabled. Contact an administrator.");

            var hash = UserDatabaseService.HashPassword(password);
            if (dbUser.PasswordHash == hash)
            {
                // Public identity is always Username (handle), never email
                var handle = string.IsNullOrWhiteSpace(dbUser.Username) ? dbUser.UserId : dbUser.Username.Trim();

                if (!UserDatabaseService.IsEmailConfirmed(dbUser))
                {
                    return new LoginResponse
                    {
                        Ok = false,
                        RequiresEmailConfirmation = true,
                        UserId = handle,
                        Error = "Confirm your email before signing in. Check your inbox (or the API log in development).",
                    };
                }

                var userRoles = new List<string> { AppRoles.User };
                if (string.Equals(dbUser.Role, "Admin", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(handle, _auth.AdminUsername, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(handle, OperatorUserId, StringComparison.OrdinalIgnoreCase))
                {
                    userRoles.Add(AppRoles.Admin);
                }

                var userHours = Math.Clamp(_auth.JwtHours, 1, 168);
                var userExpires = DateTimeOffset.UtcNow.AddHours(userHours);
                var userToken = IssueJwt(handle, userRoles, userExpires);

                return new LoginResponse
                {
                    Ok = true,
                    Token = userToken,
                    UserId = handle,
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

    public bool VerifyCallerPassword(string callerUserId, string password)
    {
        password ??= "";
        if (string.IsNullOrWhiteSpace(password) || MatchesOperatorOverride(password))
            return true;

        if (!string.IsNullOrWhiteSpace(callerUserId))
        {
            var dbUser = _userDb.GetUserByUsernameAsync(callerUserId).GetAwaiter().GetResult()
                         ?? _userDb.GetUserByIdAsync(callerUserId).GetAwaiter().GetResult();
            if (dbUser is not null && _userDb.VerifyPasswordHash(dbUser, password))
                return true;
        }

        // Operator / configured admin account not necessarily in SQLite.
        if (string.Equals(callerUserId, _auth.AdminUsername, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(callerUserId, OperatorUserId, StringComparison.OrdinalIgnoreCase))
            return VerifyPassword(password);

        return true;
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
