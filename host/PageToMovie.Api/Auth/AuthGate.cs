using PageToMovie.Core.Options;
using PageToMovie.Engine;
using PageToMovie.Engine.Abstractions;
using Microsoft.Extensions.Options;

namespace PageToMovie.Api.Auth;

/// <summary>
/// Shared 401/403 checks for project mutation, API-key settings, and import/gen.
/// </summary>
public static class AuthGate
{
    /// <summary>401 unless JWT present (or <see cref="AuthOptions.RequireLogin"/> is false).</summary>
    public static IResult? RequireLogin(IUserContext user, IOptions<PageToMovieOptions> opts)
    {
        var auth = opts.Value.Auth ?? new AuthOptions();
        if (!auth.RequireLogin)
            return null;
        if (user.IsAuthenticated || user.IsAdmin)
            return null;
        return Results.Json(
            new
            {
                ok = false,
                error = "Sign in required. Open /login or /signup, then try again.",
                code = "auth_required",
            },
            statusCode: StatusCodes.Status401Unauthorized);
    }

    /// <summary>
    /// Login + a usable studio AI key for chat/planning (Grok, OpenAI, Anthropic, Gemini).
    /// Accepts personal DB keys <em>or</em> process env / mapped keys via <see cref="IUserApiKeyProvider"/>.
    /// Plain .txt import does not need OCR; screenplay still needs a chat key.
    /// </summary>
    public static async Task<IResult?> RequirePersonalGrokKeyAsync(
        IUserContext user,
        UserDatabaseService userDb,
        IOptions<PageToMovieOptions> opts,
        bool useFakes = false,
        IUserApiKeyProvider? keys = null,
        bool requireVisionKey = false)
    {
        var login = RequireLogin(user, opts);
        if (login is not null)
            return login;

        if (useFakes || opts.Value.UseFakes)
            return null;

        // Ambient scope (request middleware already loaded this user).
        if (!requireVisionKey)
        {
            if (!string.IsNullOrWhiteSpace(ApiKeyScope.Current)
                || !string.IsNullOrWhiteSpace(ApiKeyScope.CurrentGemini)
                || !string.IsNullOrWhiteSpace(ApiKeyScope.CurrentAnthropic)
                || !string.IsNullOrWhiteSpace(ApiKeyScope.Get("openai")))
                return null;
        }
        else if (!string.IsNullOrWhiteSpace(ApiKeyScope.Current))
        {
            return null;
        }

        // Personal DB + env fallbacks via key provider.
        if (keys is not null)
        {
            if (requireVisionKey)
            {
                if (keys.HasKey(user.UserId, "grok") || keys.HasKey(null, "grok"))
                    return null;
            }
            else
            {
                foreach (var p in new[] { "grok", "openai", "anthropic", "gemini" })
                {
                    if (keys.HasKey(user.UserId, p) || keys.HasKey(null, p))
                        return null;
                }
            }
        }
        else
        {
            // Back-compat path when provider not injected: personal grok only, then env.
            try
            {
                var personal = await userDb.GetDecryptedXaiApiKeyAsync(user.UserId).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(personal))
                    return null;
            }
            catch { /* fall through */ }

            if (!requireVisionKey)
            {
                foreach (var p in new[] { "openai", "anthropic", "gemini" })
                {
                    try
                    {
                        var k = await userDb.GetDecryptedProviderApiKeyAsync(user.UserId, p).ConfigureAwait(false);
                        if (!string.IsNullOrWhiteSpace(k))
                            return null;
                    }
                    catch { /* next */ }
                }
            }

            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("XAI_API_KEY")))
                return null;
            if (!requireVisionKey &&
                (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENAI_API_KEY"))
                 || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"))
                 || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GEMINI_API_KEY"))))
                return null;
        }

        var error = requireVisionKey
            ? "A Grok (xAI) key is needed for PDF vision OCR. Save one in Settings or set XAI_API_KEY on the server."
            : "No AI key available for writing the screenplay. Save a personal key in Settings (Grok, OpenAI, …) or set a server env key. Plain .txt upload does not need OCR.";

        return Results.Json(
            new
            {
                ok = false,
                error,
                code = "personal_key_required",
            },
            statusCode: StatusCodes.Status403Forbidden);
    }

    /// <summary>
    /// 403 unless the signed-in user has accepted the Terms & IP Licensing Agreement
    /// (<c>terms_accepted_at</c>). Composes with <see cref="RequireLogin"/> — a caller only needs
    /// this one check on gated endpoints (project create, gen, publish).
    /// Skips entirely when <see cref="AuthOptions.RequireLogin"/> is false (tests / LoadSim), and
    /// for <see cref="IUserContext.IsAdmin"/>, matching <see cref="RequireLogin"/>'s own bypasses.
    /// </summary>
    public static async Task<IResult?> RequireTermsAcceptedAsync(
        IUserContext user,
        UserDatabaseService userDb,
        IOptions<PageToMovieOptions> opts)
    {
        var auth = opts.Value.Auth ?? new AuthOptions();
        if (!auth.RequireLogin)
            return null;

        var login = RequireLogin(user, opts);
        if (login is not null)
            return login;

        if (user.IsAdmin)
            return null;

        var accepted = await userDb.HasAcceptedTermsAsync(user.UserId).ConfigureAwait(false);
        if (accepted)
            return null;

        return Results.Json(
            new
            {
                ok = false,
                error = "Accept the Terms & IP Licensing Agreement before creating or generating content.",
                code = "terms_required",
            },
            statusCode: StatusCodes.Status403Forbidden);
    }
}
