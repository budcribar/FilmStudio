namespace PageToMovie.Core.Auth;

public static class AuthHeaders
{
    public const string UserId = "X-User-Id";
    public const string ApiKey = "X-Api-Key";
}

public static class AppRoles
{
    public const string User = "user";
    public const string Admin = "admin";
}

public sealed class LoginRequest
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    /// <summary>Required on signup; ignored on login (login still uses username).</summary>
    public string? Email { get; set; }
}

public sealed class ConfirmEmailRequest
{
    public string Token { get; set; } = "";
}

public sealed class TestEmailRequest
{
    public string ToEmail { get; set; } = "";
}

public sealed class ResetPasswordWithTokenRequest
{
    public string Token { get; set; } = "";
    public string NewPassword { get; set; } = "";
}

/// <summary>Body for POST /api/auth/operator-override.</summary>
public sealed class OperatorOverrideRequest
{
    public string? Secret { get; set; }
}

/// <summary>Body for POST /api/demos/{id}/report.</summary>
public sealed class DemoReportRequest
{
    public string? Note { get; set; }
}

/// <summary>Body for POST /api/admin/demos/{id}/review.</summary>
public sealed class DemoReviewRequest
{
    /// <summary>public | rejected | pending | removed</summary>
    public string? Status { get; set; }
    public string? Note { get; set; }
}

public sealed class LoginResponse
{
    public bool Ok { get; set; }
    public string? Token { get; set; }
    public string? UserId { get; set; }
    public List<string> Roles { get; set; } = new();
    public string? Error { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    /// <summary>When true, account needs email confirmation before a session token is issued.</summary>
    public bool RequiresEmailConfirmation { get; set; }
    public string? Message { get; set; }
}

public sealed class MeResponse
{
    public bool Ok { get; set; }
    public string? UserId { get; set; }
    public List<string> Roles { get; set; } = new();
    public bool IsAdmin { get; set; }
    /// <summary>True when the signed-in user has a personal Grok key saved.</summary>
    public bool HasApiKey { get; set; }
    public bool IsAuthenticated { get; set; }
}
