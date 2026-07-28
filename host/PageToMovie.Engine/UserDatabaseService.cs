using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PageToMovie.Core.Models;
using PageToMovie.Core.Options;

namespace PageToMovie.Engine;

/// <summary>
/// SQLite user database service for PageToMovie (pagetomovie.db).
/// Manages user authentication, account settings, WAL mode concurrency pragmas,
/// and AES-256 encryption at rest for per-user provider API keys (xAI, Gemini, Anthropic).
/// </summary>
public class UserDatabaseService
{
    private readonly string _dbPath;
    private readonly IDataProtector? _protector;
    private readonly ILogger<UserDatabaseService> _logger;
    private readonly object _initLock = new();
    private bool _initialized;

    public UserDatabaseService(
        IOptions<PageToMovieOptions> options,
        IDataProtectionProvider? dataProtection = null,
        ILogger<UserDatabaseService>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<UserDatabaseService>.Instance;
        _protector = dataProtection?.CreateProtector("PageToMovie.UserApiKeys");

        var workspace = options?.Value?.WorkspaceRoot;
        var dataDir = ResolveDataDirectory(workspace);
        Directory.CreateDirectory(dataDir);
        _dbPath = Path.Combine(dataDir, "pagetomovie.db");

        EnsureDatabaseInitialized();
    }

    /// <summary>
    /// Pick a durable data dir. Order:
    /// <list type="number">
    /// <item>Env <c>PageToMovie_USER_DB_DIR</c> / <c>PAGETOMOVIE_USER_DB_DIR</c></item>
    /// <item>Isolated <see cref="PageToMovieOptions.WorkspaceRoot"/> under the process temp path (unit tests)</item>
    /// <item>Container volume <c>/data</c> or <c>/app/data</c> (Railway)</item>
    /// <item>WorkspaceRoot/data, else temp PageToMovie/data</item>
    /// </list>
    /// </summary>
    internal static string ResolveDataDirectory(string? workspace)
    {
        var envDir = Environment.GetEnvironmentVariable("PageToMovie_USER_DB_DIR")
                     ?? Environment.GetEnvironmentVariable("PAGETOMOVIE_USER_DB_DIR");
        if (!string.IsNullOrWhiteSpace(envDir))
            return envDir.Trim();

        // Unit tests pass a unique temp WorkspaceRoot — never share C:\data / /data with them.
        if (IsIsolatedTestWorkspace(workspace))
            return Path.Combine(workspace!.Trim(), "data");

        if (Directory.Exists("/data"))
            return "/data";
        if (Directory.Exists("/app/data"))
            return "/app/data";

        if (!string.IsNullOrWhiteSpace(workspace))
            return Path.Combine(workspace.Trim(), "data");

        return Path.Combine(Path.GetTempPath(), "PageToMovie", "data");
    }

    private static bool IsIsolatedTestWorkspace(string? workspace)
    {
        if (string.IsNullOrWhiteSpace(workspace)) return false;
        try
        {
            var full = Path.GetFullPath(workspace.Trim());
            var temp = Path.GetFullPath(Path.GetTempPath());
            return full.StartsWith(temp, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private string ConnectionString => $"Data Source={_dbPath};Cache=Shared;Pooling=True;";

    /// <summary>
    /// Ensures SQLite database and users table exist with WAL mode pragmas enabled.
    /// </summary>
    public void EnsureDatabaseInitialized()
    {
        if (_initialized) return;

        lock (_initLock)
        {
            if (_initialized) return;

            try
            {
                using var conn = new SqliteConnection(ConnectionString);
                conn.Open();

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        PRAGMA journal_mode = WAL;
                        PRAGMA busy_timeout = 5000;
                        PRAGMA synchronous = NORMAL;
                        PRAGMA temp_store = MEMORY;
                        PRAGMA cache_size = -8000;
                    ";
                    cmd.ExecuteNonQuery();
                }

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS users (
                            user_id TEXT PRIMARY KEY,
                            username TEXT NOT NULL UNIQUE,
                            password_hash TEXT NOT NULL,
                            encrypted_xai_api_key TEXT,
                            encrypted_gemini_api_key TEXT,
                            encrypted_anthropic_api_key TEXT,
                            encrypted_fal_api_key TEXT,
                            role TEXT NOT NULL DEFAULT 'User',
                            created_at TEXT NOT NULL,
                            last_login_at TEXT,
                            credits_balance_usd REAL NOT NULL DEFAULT 0,
                            credits_lifetime_granted_usd REAL NOT NULL DEFAULT 0,
                            credits_lifetime_used_usd REAL NOT NULL DEFAULT 0
                        );
                    ";
                    cmd.ExecuteNonQuery();
                }

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS user_api_keys (
                            user_id TEXT NOT NULL,
                            provider_id TEXT NOT NULL,
                            encrypted_api_key TEXT NOT NULL,
                            updated_at TEXT NOT NULL,
                            PRIMARY KEY (user_id, provider_id)
                        );
                    ";
                    cmd.ExecuteNonQuery();
                }

                // Database Schema Migrations & Version Tracking (PRAGMA user_version)
                using (var vCmd = conn.CreateCommand())
                {
                    vCmd.CommandText = "PRAGMA user_version;";
                    var curVer = Convert.ToInt32(vCmd.ExecuteScalar() ?? 0);

                    // Migration v1 -> v2: Ensure provider key columns including Fal.ai
                    EnsureColumn(conn, "users", "encrypted_gemini_api_key", "TEXT");
                    EnsureColumn(conn, "users", "encrypted_anthropic_api_key", "TEXT");
                    EnsureColumn(conn, "users", "encrypted_fal_api_key", "TEXT");

                    if (curVer < 2)
                    {
                        using var setVer = conn.CreateCommand();
                        setVer.CommandText = "PRAGMA user_version = 2;";
                        setVer.ExecuteNonQuery();
                        _logger.LogInformation("Migrated SQLite schema to user_version 2 (added provider key columns)");
                    }

                    if (curVer < 3)
                    {
                        // Migration v2 -> v3: Auto-copy legacy column keys into unified user_api_keys table
                        using (var copyCmd = conn.CreateCommand())
                        {
                            copyCmd.CommandText = @"
                                INSERT OR IGNORE INTO user_api_keys (user_id, provider_id, encrypted_api_key, updated_at)
                                SELECT user_id, 'grok', encrypted_xai_api_key, datetime('now') FROM users WHERE encrypted_xai_api_key IS NOT NULL AND encrypted_xai_api_key != '';
                                
                                INSERT OR IGNORE INTO user_api_keys (user_id, provider_id, encrypted_api_key, updated_at)
                                SELECT user_id, 'gemini', encrypted_gemini_api_key, datetime('now') FROM users WHERE encrypted_gemini_api_key IS NOT NULL AND encrypted_gemini_api_key != '';

                                INSERT OR IGNORE INTO user_api_keys (user_id, provider_id, encrypted_api_key, updated_at)
                                SELECT user_id, 'anthropic', encrypted_anthropic_api_key, datetime('now') FROM users WHERE encrypted_anthropic_api_key IS NOT NULL AND encrypted_anthropic_api_key != '';

                                INSERT OR IGNORE INTO user_api_keys (user_id, provider_id, encrypted_api_key, updated_at)
                                SELECT user_id, 'fal', encrypted_fal_api_key, datetime('now') FROM users WHERE encrypted_fal_api_key IS NOT NULL AND encrypted_fal_api_key != '';
                            ";
                            copyCmd.ExecuteNonQuery();
                        }

                        using var setVer3 = conn.CreateCommand();
                        setVer3.CommandText = "PRAGMA user_version = 3;";
                        setVer3.ExecuteNonQuery();
                        _logger.LogInformation("Migrated SQLite schema to user_version 3 (unified dynamic user_api_keys table)");
                    }
                }

                // User billing credits (list-rate USD; 1 credit = $0.01).
                EnsureColumn(conn, "users", "credits_balance_usd", "REAL NOT NULL DEFAULT 0");
                EnsureColumn(conn, "users", "credits_lifetime_granted_usd", "REAL NOT NULL DEFAULT 0");
                EnsureColumn(conn, "users", "credits_lifetime_used_usd", "REAL NOT NULL DEFAULT 0");

                // User Terms of Service acceptance tracking
                EnsureColumn(conn, "users", "terms_accepted_at", "TEXT");
                EnsureColumn(conn, "users", "terms_version", "TEXT");

                // Admin disable (soft ban) — blocks login / API without deleting ledger.
                EnsureColumn(conn, "users", "is_disabled", "INTEGER NOT NULL DEFAULT 0");

                // Forgot-password request marker (legacy admin path; email reset preferred).
                EnsureColumn(conn, "users", "password_reset_requested_at", "TEXT");
                EnsureColumn(conn, "users", "email", "TEXT");
                EnsureColumn(conn, "users", "email_confirmed_at", "TEXT");

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        CREATE UNIQUE INDEX IF NOT EXISTS idx_users_email
                        ON users(email) WHERE email IS NOT NULL AND TRIM(email) != '';
                    ";
                    try { cmd.ExecuteNonQuery(); } catch { /* index may already exist */ }
                }

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS auth_tokens (
                            token_hash TEXT PRIMARY KEY,
                            user_id TEXT NOT NULL,
                            purpose TEXT NOT NULL,
                            expires_at TEXT NOT NULL,
                            created_at TEXT NOT NULL,
                            used_at TEXT
                        );
                        CREATE INDEX IF NOT EXISTS idx_auth_tokens_user ON auth_tokens(user_id);
                    ";
                    cmd.ExecuteNonQuery();
                }

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS credit_ledger (
                            id INTEGER PRIMARY KEY AUTOINCREMENT,
                            user_id TEXT NOT NULL,
                            ts TEXT NOT NULL,
                            kind TEXT NOT NULL,
                            amount_usd REAL NOT NULL,
                            balance_after_usd REAL NOT NULL,
                            project_id TEXT,
                            note TEXT,
                            meta_kind TEXT
                        );
                        CREATE INDEX IF NOT EXISTS idx_credit_ledger_user ON credit_ledger(user_id);
                        CREATE INDEX IF NOT EXISTS idx_credit_ledger_ts ON credit_ledger(ts);
                    ";
                    cmd.ExecuteNonQuery();
                }

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        INSERT INTO users (user_id, username, password_hash, role, created_at, email_confirmed_at)
                        VALUES ('admin', 'admin', @hash, 'Admin', @created, @created)
                        ON CONFLICT(user_id) DO UPDATE SET
                            password_hash = @hash,
                            role = 'Admin',
                            email_confirmed_at = COALESCE(users.email_confirmed_at, @created);
                    ";
                    cmd.Parameters.AddWithValue("@hash", HashPassword("admin"));
                    cmd.Parameters.AddWithValue("@created", DateTime.UtcNow.ToString("o"));
                    cmd.ExecuteNonQuery();
                }

                _initialized = true;
                _logger.LogInformation("SQLite database initialized at {DbPath} (WAL mode enabled)", _dbPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize SQLite database at {DbPath}", _dbPath);
                throw;
            }
        }
    }

    private static void EnsureColumn(SqliteConnection conn, string table, string column, string typeSql)
    {
        using var check = conn.CreateCommand();
        check.CommandText = $"PRAGMA table_info({table})";
        using var reader = check.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                return;
        }
        reader.Close();

        try
        {
            using var alter = conn.CreateCommand();
            alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {typeSql}";
            alter.ExecuteNonQuery();
        }
        catch (SqliteException ex) when (ex.Message.Contains("duplicate column", StringComparison.OrdinalIgnoreCase))
        {
            // Race-safe: another process may have added the column between PRAGMA and ALTER.
        }
    }

    public async Task<UserEntity?> GetUserByIdAsync(string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return null;

        using var conn = new SqliteConnection(ConnectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = UserSelectSql + " WHERE user_id = @id LIMIT 1";
        cmd.Parameters.AddWithValue("@id", userId.Trim());

        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (await reader.ReadAsync(ct).ConfigureAwait(false))
            return ReadUserFromReader(reader);

        return null;
    }

    public async Task<UserEntity?> GetUserByUsernameAsync(string username, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(username)) return null;

        using var conn = new SqliteConnection(ConnectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = UserSelectSql + " WHERE LOWER(username) = LOWER(@name) LIMIT 1";
        cmd.Parameters.AddWithValue("@name", username.Trim());

        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (await reader.ReadAsync(ct).ConfigureAwait(false))
            return ReadUserFromReader(reader);

        return null;
    }

    /// <summary>
    /// Privacy-safe handle search: returns usernames only (never emails).
    /// Exact match first, then prefix matches. Disabled accounts excluded.
    /// </summary>
    public async Task<IReadOnlyList<string>> SearchUsernamesAsync(
        string query,
        int take = 8,
        CancellationToken ct = default)
    {
        var q = (query ?? "").Trim().TrimStart('@');
        if (q.Length < 1) return Array.Empty<string>();
        take = Math.Clamp(take, 1, 20);

        using var conn = new SqliteConnection(ConnectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);

        using var cmd = conn.CreateCommand();
        // Exact first (ORDER BY exact), then prefix; hide disabled; never return email
        cmd.CommandText = """
            SELECT username FROM users
            WHERE COALESCE(is_disabled, 0) = 0
              AND username IS NOT NULL
              AND TRIM(username) != ''
              AND (
                    LOWER(username) = LOWER(@exact)
                 OR LOWER(username) LIKE LOWER(@prefix)
              )
            ORDER BY CASE WHEN LOWER(username) = LOWER(@exact) THEN 0 ELSE 1 END,
                     LENGTH(username),
                     username COLLATE NOCASE
            LIMIT @take
            """;
        cmd.Parameters.AddWithValue("@exact", q);
        cmd.Parameters.AddWithValue("@prefix", q + "%");
        cmd.Parameters.AddWithValue("@take", take);

        var list = new List<string>();
        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var name = reader.GetString(0).Trim();
            if (name.Length == 0) continue;
            // Prefer not listing pure email usernames as "handles" in search
            if (name.Contains('@', StringComparison.Ordinal)) continue;
            list.Add(name);
        }
        return list;
    }

    /// <summary>Resolve by user_id, then username (case-insensitive).</summary>
    public async Task<UserEntity?> ResolveUserAsync(string userIdOrName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userIdOrName)) return null;
        var byId = await GetUserByIdAsync(userIdOrName, ct).ConfigureAwait(false);
        if (byId is not null) return byId;
        return await GetUserByUsernameAsync(userIdOrName, ct).ConfigureAwait(false);
    }

    public const string AuthPurposeEmailConfirm = "email_confirm";
    public const string AuthPurposePasswordReset = "password_reset";

    private const string UserSelectSql = @"
            SELECT user_id, username, password_hash,
                   encrypted_xai_api_key, encrypted_gemini_api_key, encrypted_anthropic_api_key, encrypted_fal_api_key,
                   role, created_at, last_login_at,
                   COALESCE(credits_balance_usd, 0),
                   COALESCE(credits_lifetime_granted_usd, 0),
                   COALESCE(credits_lifetime_used_usd, 0),
                   COALESCE(is_disabled, 0),
                   email,
                   email_confirmed_at
            FROM users";

    /// <summary>Saves or updates a user's encrypted xAI API key in SQLite.</summary>
    public Task SaveXaiApiKeyAsync(string userId, string? apiKey, CancellationToken ct = default) =>
        SaveProviderApiKeyAsync(userId, "grok", apiKey, ct);

    /// <summary>
    /// Saves a personal provider key dynamically into user_api_keys. Empty/whitespace clears the stored key.
    /// Provider: grok, gemini, anthropic, fal, replicate, or any arbitrary provider ID.
    /// </summary>
    public async Task SaveProviderApiKeyAsync(string userId, string providerId, string? apiKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(providerId)) return;
        var normId = NormalizeProvider(providerId);

        using var conn = new SqliteConnection(ConnectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            using var delCmd = conn.CreateCommand();
            delCmd.CommandText = "DELETE FROM user_api_keys WHERE user_id = @userId AND provider_id = @providerId";
            delCmd.Parameters.AddWithValue("@userId", userId.Trim());
            delCmd.Parameters.AddWithValue("@providerId", normId);
            await delCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            return;
        }

        var encrypted = EncryptApiKey(apiKey.Trim());
        using var upsertCmd = conn.CreateCommand();
        upsertCmd.CommandText = @"
            INSERT INTO user_api_keys (user_id, provider_id, encrypted_api_key, updated_at)
            VALUES (@userId, @providerId, @key, @updated)
            ON CONFLICT(user_id, provider_id) DO UPDATE SET
                encrypted_api_key = excluded.encrypted_api_key,
                updated_at = excluded.updated_at;";
        upsertCmd.Parameters.AddWithValue("@userId", userId.Trim());
        upsertCmd.Parameters.AddWithValue("@providerId", normId);
        upsertCmd.Parameters.AddWithValue("@key", encrypted);
        upsertCmd.Parameters.AddWithValue("@updated", DateTime.UtcNow.ToString("o"));
        await upsertCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Applies provider API key updates dynamically from the request.
    /// </summary>
    public async Task UpdateUserSettingsAsync(string userId, UpdateUserSettingsRequest req, CancellationToken ct = default)
    {
        if (req.ProviderApiKeys is { Count: > 0 })
        {
            foreach (var kvp in req.ProviderApiKeys)
            {
                await SaveProviderApiKeyAsync(userId, kvp.Key, kvp.Value, ct).ConfigureAwait(false);
            }
        }
    }

    public async Task<string?> GetDecryptedXaiApiKeyAsync(string userId, CancellationToken ct = default) =>
        await GetDecryptedProviderApiKeyAsync(userId, "grok", ct).ConfigureAwait(false);

    public async Task<string?> GetDecryptedProviderApiKeyAsync(string userId, string providerId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(providerId)) return null;
        var normId = NormalizeProvider(providerId);

        using var conn = new SqliteConnection(ConnectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT encrypted_api_key FROM user_api_keys WHERE user_id = @userId AND provider_id = @providerId";
        cmd.Parameters.AddWithValue("@userId", userId.Trim());
        cmd.Parameters.AddWithValue("@providerId", normId);

        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        if (result is string encrypted && !string.IsNullOrWhiteSpace(encrypted))
        {
            try
            {
                return DecryptApiKey(encrypted);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "GetDecryptedProviderApiKeyAsync failed for {UserId}/{Provider}", userId, providerId);
            }
        }

        // Fallback check against legacy columns for backward compatibility if user_api_keys table row was deleted
        var user = await GetUserByIdAsync(userId, ct).ConfigureAwait(false);
        if (user is not null)
        {
            var legacyEncrypted = GetEncryptedFromEntity(user, providerId);
            if (!string.IsNullOrWhiteSpace(legacyEncrypted))
            {
                try { return DecryptApiKey(legacyEncrypted); } catch { }
            }
        }

        return null;
    }

    public async Task<UserSettingsDto> GetUserSettingsDtoAsync(string userId, CancellationToken ct = default)
    {
        var user = await GetUserByIdAsync(userId, ct).ConfigureAwait(false);
        var username = user?.Username ?? userId;

        // Fetch all encrypted keys for this user from user_api_keys table
        var personalKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using (var conn = new SqliteConnection(ConnectionString))
        {
            await conn.OpenAsync(ct).ConfigureAwait(false);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT provider_id, encrypted_api_key FROM user_api_keys WHERE user_id = @userId";
            cmd.Parameters.AddWithValue("@userId", userId.Trim());
            using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                var pid = reader.GetString(0);
                var enc = reader.GetString(1);
                var plain = DecryptOptional(enc);
                if (!string.IsNullOrWhiteSpace(plain))
                {
                    personalKeys[pid] = plain;
                }
            }
        }

        // Fallback for legacy columns if user_api_keys table hasn't populated them yet
        if (!personalKeys.ContainsKey("grok") && DecryptOptional(user?.EncryptedXaiApiKey) is { } x) personalKeys["grok"] = x;
        if (!personalKeys.ContainsKey("gemini") && DecryptOptional(user?.EncryptedGeminiApiKey) is { } g) personalKeys["gemini"] = g;
        if (!personalKeys.ContainsKey("anthropic") && DecryptOptional(user?.EncryptedAnthropicApiKey) is { } a) personalKeys["anthropic"] = a;
        if (!personalKeys.ContainsKey("fal") && DecryptOptional(user?.EncryptedFalApiKey) is { } f) personalKeys["fal"] = f;

        // Dynamically discover all unique providers defined in SupportedModelCatalog (driven by models_catalog.json!)
        var catalogEntries = SupportedModelCatalog.Entries;
        var providerGroups = catalogEntries.GroupBy(e => NormalizeProvider(e.ProviderId));

        var providers = new List<ProviderKeyStatusDto>();
        foreach (var group in providerGroups)
        {
            var pId = group.Key;
            var sample = group.First();
            var familyName = sample.Provider.ToString();
            var displayName = sample.Provider switch
            {
                ModelProviderFamily.Xai => "xAI / Grok",
                ModelProviderFamily.Google => "Google Gemini",
                ModelProviderFamily.Anthropic => "Anthropic Claude",
                ModelProviderFamily.Fal => "Fal.ai",
                _ => char.ToUpperInvariant(pId[0]) + pId[1..],
            };

            var requiredKeys = group.SelectMany(m => m.RequiredEnvKeys).Distinct().ToList();
            var hasServer = requiredKeys.Any(EnvPresent);
            personalKeys.TryGetValue(pId, out var personal);

            var supportsVideoGen = group.Any(m => m.Capability == ModelCapability.Video && m.SupportsVideoContinue);
            var supportsVideoReview = group.Any(m => m.SupportsVideoReview);
            var supportsImageGen = group.Any(m => m.Capability == ModelCapability.Image);
            var supportsScriptPlanning = group.Any(m => m.Capability == ModelCapability.Chat);
            var supportsImageVision = group.Any(m => m.Capability == ModelCapability.Vision);

            var notes = string.Join("; ", group.Select(m => m.Notes).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().Take(2));

            providers.Add(BuildProviderStatus(
                providerId: pId,
                displayName: displayName,
                family: familyName,
                personal: personal,
                hasServer: hasServer,
                supportsVideoGen: supportsVideoGen,
                supportsVideoReview: supportsVideoReview,
                supportsImageGen: supportsImageGen,
                supportsScriptPlanning: supportsScriptPlanning,
                supportsImageVision: supportsImageVision,
                notes: notes));
        }

        return new UserSettingsDto
        {
            UserId = user?.UserId ?? userId,
            Username = username,
            Providers = providers,
        };
    }

    public async Task InsertUserAsync(UserEntity user, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(ConnectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO users (
                user_id, username, password_hash,
                encrypted_xai_api_key, encrypted_gemini_api_key, encrypted_anthropic_api_key, encrypted_fal_api_key,
                role, created_at, last_login_at,
                credits_balance_usd, credits_lifetime_granted_usd, credits_lifetime_used_usd,
                is_disabled, email, email_confirmed_at)
            VALUES (@id, @name, @hash, @xai, @gemini, @anthropic, @fal, @role, @created, @login,
                    @bal, @granted, @used, @disabled, @email, @email_confirmed)
            ON CONFLICT(user_id) DO UPDATE SET
                username = excluded.username,
                encrypted_xai_api_key = COALESCE(excluded.encrypted_xai_api_key, users.encrypted_xai_api_key),
                encrypted_gemini_api_key = COALESCE(excluded.encrypted_gemini_api_key, users.encrypted_gemini_api_key),
                encrypted_anthropic_api_key = COALESCE(excluded.encrypted_anthropic_api_key, users.encrypted_anthropic_api_key),
                encrypted_fal_api_key = COALESCE(excluded.encrypted_fal_api_key, users.encrypted_fal_api_key);
        ";
        cmd.Parameters.AddWithValue("@id", user.UserId);
        cmd.Parameters.AddWithValue("@name", user.Username);
        cmd.Parameters.AddWithValue("@hash", user.PasswordHash);
        cmd.Parameters.AddWithValue("@xai", (object?)user.EncryptedXaiApiKey ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@gemini", (object?)user.EncryptedGeminiApiKey ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@anthropic", (object?)user.EncryptedAnthropicApiKey ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@fal", (object?)user.EncryptedFalApiKey ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@role", user.Role);
        cmd.Parameters.AddWithValue("@created", user.CreatedAt.ToString("o"));
        cmd.Parameters.AddWithValue("@login", (object?)user.LastLoginAt?.ToString("o") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@bal", user.CreditsBalanceUsd);
        cmd.Parameters.AddWithValue("@granted", user.CreditsLifetimeGrantedUsd);
        cmd.Parameters.AddWithValue("@used", user.CreditsLifetimeUsedUsd);
        cmd.Parameters.AddWithValue("@disabled", user.IsDisabled ? 1 : 0);
        cmd.Parameters.AddWithValue("@email", (object?)NormalizeEmail(user.Email) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@email_confirmed",
            (object?)user.EmailConfirmedAt?.ToString("o") ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    /// <summary>True when the account exists and is admin-disabled.</summary>
    public async Task<bool> IsUserDisabledAsync(string? userIdOrName, CancellationToken ct = default)
    {
        var user = await ResolveUserAsync(userIdOrName ?? "", ct).ConfigureAwait(false);
        return user?.IsDisabled == true;
    }

    /// <summary>
    /// Enable or disable an account. Returns null when user not found.
    /// Caller must enforce self-disable and last-admin rules.
    /// </summary>
    public async Task<UserCreditSummaryDto?> SetUserDisabledAsync(
        string userId,
        bool disabled,
        CancellationToken ct = default)
    {
        var user = await ResolveUserAsync(userId, ct).ConfigureAwait(false);
        if (user is null) return null;

        using var conn = new SqliteConnection(ConnectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE users SET is_disabled = @d WHERE user_id = @id";
        cmd.Parameters.AddWithValue("@d", disabled ? 1 : 0);
        cmd.Parameters.AddWithValue("@id", user.UserId);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        user.IsDisabled = disabled;
        return ToCreditSummary(user);
    }

    /// <summary>Count non-disabled accounts with Role = Admin.</summary>
    public async Task<int> CountActiveAdminsAsync(CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(ConnectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT COUNT(*) FROM users
            WHERE LOWER(role) = 'admin'
              AND COALESCE(is_disabled, 0) = 0";
        var scalar = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return Convert.ToInt32(scalar ?? 0);
    }

    /// <summary>
    /// Hard-delete user row + credit ledger. Does not touch projects/demos (API orchestrates those).
    /// </summary>
    public async Task<bool> HardDeleteUserAsync(string userId, CancellationToken ct = default)
    {
        var user = await ResolveUserAsync(userId, ct).ConfigureAwait(false);
        if (user is null) return false;

        using var conn = new SqliteConnection(ConnectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        using var tx = (SqliteTransaction)await conn
            .BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct)
            .ConfigureAwait(false);

        using (var delLedger = conn.CreateCommand())
        {
            delLedger.Transaction = tx;
            delLedger.CommandText = "DELETE FROM credit_ledger WHERE user_id = @id OR LOWER(user_id) = LOWER(@id)";
            delLedger.Parameters.AddWithValue("@id", user.UserId);
            await delLedger.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        using (var delUser = conn.CreateCommand())
        {
            delUser.Transaction = tx;
            delUser.CommandText = "DELETE FROM users WHERE user_id = @id";
            delUser.Parameters.AddWithValue("@id", user.UserId);
            var rows = await delUser.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            if (rows == 0)
            {
                await tx.RollbackAsync(ct).ConfigureAwait(false);
                return false;
            }
        }

        await tx.CommitAsync(ct).ConfigureAwait(false);
        _logger.LogInformation("Hard-deleted user {UserId} ({Username})", user.UserId, user.Username);
        return true;
    }

    /// <summary>True when password matches the stored hash for this user.</summary>
    public bool VerifyPasswordHash(UserEntity user, string password)
    {
        if (user is null || string.IsNullOrEmpty(user.PasswordHash))
            return false;
        var hash = HashPassword(password ?? "");
        return string.Equals(user.PasswordHash, hash, StringComparison.Ordinal);
    }

    /// <summary>
    /// Marks a password-reset request if the account exists. Does not reveal whether it exists.
    /// </summary>
    public async Task NotePasswordResetRequestedAsync(string usernameOrId, CancellationToken ct = default)
    {
        var user = await ResolveUserAsync(usernameOrId, ct).ConfigureAwait(false);
        if (user is null || user.IsDisabled) return;

        using var conn = new SqliteConnection(ConnectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE users SET password_reset_requested_at = @t WHERE user_id = @id";
        cmd.Parameters.AddWithValue("@t", DateTimeOffset.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("@id", user.UserId);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        _logger.LogInformation("Password reset requested for user {UserId}", user.UserId);
    }

    /// <summary>Sets a new password hash and clears any forgot-password marker.</summary>
    public async Task<bool> SetPasswordAsync(string userId, string newPassword, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return false;
        newPassword ??= "";
        if (newPassword.Length < 4) return false;

        using var conn = new SqliteConnection(ConnectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE users
            SET password_hash = @hash,
                password_reset_requested_at = NULL
            WHERE user_id = @id";
        cmd.Parameters.AddWithValue("@hash", HashPassword(newPassword));
        cmd.Parameters.AddWithValue("@id", userId.Trim());
        var n = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        if (n > 0)
            _logger.LogInformation("Password set for user {UserId}", userId.Trim());
        return n > 0;
    }

    public async Task<Dictionary<string, DateTimeOffset>> GetPasswordResetRequestedMapAsync(
        CancellationToken ct = default)
    {
        var map = new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);
        using var conn = new SqliteConnection(ConnectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT user_id, password_reset_requested_at
            FROM users
            WHERE password_reset_requested_at IS NOT NULL
              AND TRIM(password_reset_requested_at) != ''";
        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var id = reader.GetString(0);
            var raw = reader.IsDBNull(1) ? null : reader.GetString(1);
            if (DateTimeOffset.TryParse(raw, out var when))
                map[id] = when;
        }
        return map;
    }

    // ── Credits ──────────────────────────────────────────────────────────────

    public async Task<List<UserCreditSummaryDto>> ListUserCreditSummariesAsync(CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(ConnectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = UserSelectSql + " ORDER BY LOWER(username)";

        var list = new List<UserCreditSummaryDto>();
        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            list.Add(ToCreditSummary(ReadUserFromReader(reader)));

        var resets = await GetPasswordResetRequestedMapAsync(ct).ConfigureAwait(false);
        foreach (var u in list)
        {
            if (resets.TryGetValue(u.UserId, out var when))
                u.PasswordResetRequestedAt = when;
        }
        return list;
    }

    public async Task<UserCreditSummaryDto?> GetUserCreditSummaryAsync(string userId, CancellationToken ct = default)
    {
        var user = await ResolveUserAsync(userId, ct).ConfigureAwait(false);
        return user is null ? null : ToCreditSummary(user);
    }

    public async Task<AdminCreditsOverviewDto> GetAdminCreditsOverviewAsync(
        int recentLedger = 40,
        CancellationToken ct = default)
    {
        var users = await ListUserCreditSummariesAsync(ct).ConfigureAwait(false);
        var ledger = await GetRecentCreditLedgerAsync(Math.Clamp(recentLedger, 1, 200), ct).ConfigureAwait(false);

        return new AdminCreditsOverviewDto
        {
            UserCount = users.Count,
            TotalBalanceUsd = users.Sum(u => u.CreditsBalanceUsd),
            TotalGrantedUsd = users.Sum(u => u.CreditsLifetimeGrantedUsd),
            TotalUsedUsd = users.Sum(u => u.CreditsLifetimeUsedUsd),
            Users = users,
            RecentLedger = ledger,
            UsdPerCredit = CreditUnits.UsdPerCredit,
        };
    }

    public async Task<List<CreditLedgerEntryDto>> GetRecentCreditLedgerAsync(
        int take = 40,
        CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(ConnectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, user_id, ts, kind, amount_usd, balance_after_usd, project_id, note, meta_kind
            FROM credit_ledger
            ORDER BY id DESC
            LIMIT @take";
        cmd.Parameters.AddWithValue("@take", Math.Clamp(take, 1, 500));

        var list = new List<CreditLedgerEntryDto>();
        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            list.Add(ReadLedgerEntry(reader));
        return list;
    }

    /// <summary>
    /// Per-user in-process gate so concurrent ASP.NET requests for the same user
    /// cannot race read-modify-write even under SQLite deferred transactions.
    /// </summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim>
        CreditLocks = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Atomically apply a credit delta. Positive = grant, negative = debit/claw-back.
    /// Updates balance + lifetime counters and appends a ledger row.
    /// Uses BEGIN IMMEDIATE + SQL relative UPDATE so concurrent debits/grants cannot lose updates.
    /// </summary>
    public async Task<UserCreditSummaryDto?> ApplyCreditDeltaAsync(
        string userId,
        double amountUsd,
        string kind,
        string? note,
        string? metaKind,
        string? projectId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return null;

        // Round to 4 decimal places (cents of a cent) for stable math.
        amountUsd = Math.Round(amountUsd, 4, MidpointRounding.AwayFromZero);
        if (Math.Abs(amountUsd) < 0.00005)
            return await GetUserCreditSummaryAsync(userId, ct).ConfigureAwait(false);

        var lockKey = userId.Trim().ToLowerInvariant();
        var gate = CreditLocks.GetOrAdd(lockKey, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await ApplyCreditDeltaCoreAsync(userId, amountUsd, kind, note, metaKind, projectId, ct)
                .ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<UserCreditSummaryDto?> ApplyCreditDeltaCoreAsync(
        string userId,
        double amountUsd,
        string kind,
        string? note,
        string? metaKind,
        string? projectId,
        CancellationToken ct)
    {
        using var conn = new SqliteConnection(ConnectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);

        // IMMEDIATE locks the DB for write at begin — prevents concurrent deferred txs
        // from both reading the same balance and losing an update.
        using var tx = (SqliteTransaction)await conn
            .BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct)
            .ConfigureAwait(false);

        string resolvedUserId;
        using (var find = conn.CreateCommand())
        {
            find.Transaction = tx;
            find.CommandText =
                "SELECT user_id FROM users WHERE user_id = @id OR LOWER(username) = LOWER(@id) LIMIT 1";
            find.Parameters.AddWithValue("@id", userId.Trim());
            var found = await find.ExecuteScalarAsync(ct).ConfigureAwait(false);
            if (found is null || found is DBNull)
            {
                await tx.RollbackAsync(ct).ConfigureAwait(false);
                return null;
            }
            resolvedUserId = Convert.ToString(found) ?? userId.Trim();
        }

        var grantDelta = amountUsd > 0 ? amountUsd : 0d;
        var usedDelta = amountUsd < 0 ? Math.Abs(amountUsd) : 0d;

        // Relative UPDATE so the column math happens inside the write lock.
        using (var upd = conn.CreateCommand())
        {
            upd.Transaction = tx;
            upd.CommandText = @"
                UPDATE users SET
                    credits_balance_usd = ROUND(credits_balance_usd + @amt, 4),
                    credits_lifetime_granted_usd = ROUND(credits_lifetime_granted_usd + @grant, 4),
                    credits_lifetime_used_usd = ROUND(credits_lifetime_used_usd + @used, 4)
                WHERE user_id = @id";
            upd.Parameters.AddWithValue("@amt", amountUsd);
            upd.Parameters.AddWithValue("@grant", grantDelta);
            upd.Parameters.AddWithValue("@used", usedDelta);
            upd.Parameters.AddWithValue("@id", resolvedUserId);
            var n = await upd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            if (n == 0)
            {
                await tx.RollbackAsync(ct).ConfigureAwait(false);
                return null;
            }
        }

        UserEntity user;
        using (var reload = conn.CreateCommand())
        {
            reload.Transaction = tx;
            reload.CommandText = UserSelectSql + " WHERE user_id = @id LIMIT 1";
            reload.Parameters.AddWithValue("@id", resolvedUserId);
            using var reader = await reload.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                await tx.RollbackAsync(ct).ConfigureAwait(false);
                return null;
            }
            user = ReadUserFromReader(reader);
        }

        var ts = DateTimeOffset.UtcNow;
        using (var ins = conn.CreateCommand())
        {
            ins.Transaction = tx;
            ins.CommandText = @"
                INSERT INTO credit_ledger
                    (user_id, ts, kind, amount_usd, balance_after_usd, project_id, note, meta_kind)
                VALUES (@uid, @ts, @kind, @amt, @bal, @proj, @note, @meta)";
            ins.Parameters.AddWithValue("@uid", user.UserId);
            ins.Parameters.AddWithValue("@ts", ts.ToString("o"));
            ins.Parameters.AddWithValue("@kind", string.IsNullOrWhiteSpace(kind) ? "adjust" : kind.Trim());
            ins.Parameters.AddWithValue("@amt", amountUsd);
            ins.Parameters.AddWithValue("@bal", user.CreditsBalanceUsd);
            ins.Parameters.AddWithValue("@proj", (object?)projectId ?? DBNull.Value);
            ins.Parameters.AddWithValue("@note", (object?)note ?? DBNull.Value);
            ins.Parameters.AddWithValue("@meta", (object?)metaKind ?? DBNull.Value);
            await ins.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        await tx.CommitAsync(ct).ConfigureAwait(false);
        return ToCreditSummary(user);
    }

    private static UserCreditSummaryDto ToCreditSummary(UserEntity u) => new()
    {
        UserId = u.UserId,
        Username = u.Username,
        Role = u.Role,
        CreatedAt = u.CreatedAt,
        LastLoginAt = u.LastLoginAt,
        HasXaiApiKey = !string.IsNullOrWhiteSpace(u.EncryptedXaiApiKey),
        IsDisabled = u.IsDisabled,
        CreditsBalanceUsd = u.CreditsBalanceUsd,
        CreditsLifetimeGrantedUsd = u.CreditsLifetimeGrantedUsd,
        CreditsLifetimeUsedUsd = u.CreditsLifetimeUsedUsd,
    };

    private static CreditLedgerEntryDto ReadLedgerEntry(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt64(0),
        UserId = reader.GetString(1),
        Ts = DateTimeOffset.TryParse(reader.GetString(2), out var ts) ? ts : DateTimeOffset.UtcNow,
        Kind = reader.GetString(3),
        AmountUsd = reader.GetDouble(4),
        BalanceAfterUsd = reader.GetDouble(5),
        ProjectId = reader.IsDBNull(6) ? null : reader.GetString(6),
        Note = reader.IsDBNull(7) ? null : reader.GetString(7),
        MetaKind = reader.IsDBNull(8) ? null : reader.GetString(8),
    };

    private string? DecryptOptional(string? encrypted)
    {
        if (string.IsNullOrWhiteSpace(encrypted)) return null;
        try
        {
            return DecryptApiKey(encrypted);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DecryptOptional failed — treating personal key as missing");
            return null;
        }
    }

    private static bool EnvPresent(string name) =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name));

    private static string? MaskKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        if (key.Length > 8)
            return key.Substring(0, 4) + "..." + key.Substring(key.Length - 4);
        return "****";
    }

    private static ProviderKeyStatusDto BuildProviderStatus(
        string providerId,
        string displayName,
        string family,
        string? personal,
        bool hasServer,
        bool supportsVideoGen,
        bool supportsVideoReview,
        bool supportsImageGen,
        bool supportsScriptPlanning,
        bool supportsImageVision,
        string? notes)
    {
        var hasPersonal = !string.IsNullOrWhiteSpace(personal);
        var caps = new List<string>();
        if (supportsVideoGen) caps.Add("Video Gen");
        if (supportsVideoReview) caps.Add("Video Review");
        if (supportsImageGen) caps.Add("Image Gen");
        if (supportsScriptPlanning) caps.Add("Script & Planning");
        if (supportsImageVision) caps.Add("Image Vision / OCR");
        if (caps.Count == 0) caps.Add("—");

        return new ProviderKeyStatusDto
        {
            ProviderId = providerId,
            DisplayName = displayName,
            Family = family,
            HasPersonalKey = hasPersonal,
            MaskedPersonalKey = MaskKey(personal),
            HasServerKey = hasServer,
            ActiveSource = hasPersonal ? "personal" : hasServer ? "server" : "none",
            CapabilitiesSummary = string.Join(", ", caps),
            SupportsVideo = supportsVideoGen || supportsVideoReview,
            SupportsImage = supportsImageGen,
            SupportsChat = supportsScriptPlanning,
            SupportsVision = supportsImageVision,
            SupportsVideoGen = supportsVideoGen,
            SupportsVideoReview = supportsVideoReview,
            SupportsImageGen = supportsImageGen,
            SupportsScriptPlanning = supportsScriptPlanning,
            SupportsImageVision = supportsImageVision,
            Notes = notes,
        };
    }

    private static string? ProviderColumn(string providerId) =>
        NormalizeProvider(providerId) switch
        {
            "grok" => "encrypted_xai_api_key",
            "gemini" => "encrypted_gemini_api_key",
            "anthropic" => "encrypted_anthropic_api_key",
            "fal" => "encrypted_fal_api_key",
            _ => null,
        };

    private static string NormalizeProvider(string? providerId)
    {
        if (string.IsNullOrWhiteSpace(providerId)) return "";
        var p = providerId.Trim().ToLowerInvariant();
        return p switch
        {
            "xai" or "grok" => "grok",
            "google" or "gemini" => "gemini",
            "claude" or "anthropic" => "anthropic",
            "fal" => "fal",
            _ => p,
        };
    }

    private static string? GetEncryptedFromEntity(UserEntity user, string providerId) =>
        NormalizeProvider(providerId) switch
        {
            "grok" => user.EncryptedXaiApiKey,
            "gemini" => user.EncryptedGeminiApiKey,
            "anthropic" => user.EncryptedAnthropicApiKey,
            "fal" => user.EncryptedFalApiKey,
            _ => null,
        };

    private static void SetEncryptedOnEntity(UserEntity user, string providerId, string? encrypted)
    {
        switch (NormalizeProvider(providerId))
        {
            case "grok": user.EncryptedXaiApiKey = encrypted; break;
            case "gemini": user.EncryptedGeminiApiKey = encrypted; break;
            case "anthropic": user.EncryptedAnthropicApiKey = encrypted; break;
        }
    }

    private string EncryptApiKey(string plainText)
    {
        if (_protector != null)
            return _protector.Protect(plainText);

        return "plain:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(plainText));
    }

    private string DecryptApiKey(string cipherText)
    {
        if (cipherText.StartsWith("plain:"))
        {
            var raw = cipherText.Substring(6);
            return Encoding.UTF8.GetString(Convert.FromBase64String(raw));
        }

        if (_protector is null)
        {
            // No protector: only accept plain: payloads (dev). Never return opaque ciphertext as a key.
            throw new InvalidOperationException(
                "Cannot decrypt personal API key (DataProtection not configured). Re-save the key in Configuration.");
        }

        try
        {
            return _protector.Unprotect(cipherText);
        }
        catch (Exception ex)
        {
            // Common on Railway after redeploy without a Volume on /data: DP keys rotate and
            // stored ciphertexts become unreadable. Returning ciphertext as the API key caused
            // "Key Active" in UI with 401s on xAI. Treat as missing instead.
            _logger.LogWarning(ex,
                "Failed to decrypt API key with DataProtector — re-save the key in Configuration " +
                "(and mount a Railway Volume at /data so keys survive restarts)");
            throw new InvalidOperationException(
                "Personal API key could not be decrypted (encryption keys changed after redeploy). " +
                "Open Configuration, re-save your xAI / Grok key. Mount a Railway Volume at /data " +
                "so the key and data-protection store persist.", ex);
        }
    }

    public static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password + "PageToMovieSalt"));
        return Convert.ToBase64String(bytes);
    }

    public static string? NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        return email.Trim().ToLowerInvariant();
    }

    public static bool IsValidEmail(string? email)
    {
        var e = NormalizeEmail(email);
        if (e is null || e.Length < 5 || e.Length > 254) return false;
        var at = e.IndexOf('@');
        if (at <= 0 || at != e.LastIndexOf('@')) return false;
        var domain = e[(at + 1)..];
        return domain.Contains('.') && !e.Contains(' ');
    }

    /// <summary>Legacy accounts with no email are treated as confirmed.</summary>
    public static bool IsEmailConfirmed(UserEntity? user)
    {
        if (user is null) return false;
        if (string.IsNullOrWhiteSpace(user.Email)) return true;
        return user.EmailConfirmedAt is not null;
    }

    public async Task<UserEntity?> GetUserByEmailAsync(string email, CancellationToken ct = default)
    {
        var e = NormalizeEmail(email);
        if (e is null) return null;
        using var conn = new SqliteConnection(ConnectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = UserSelectSql + " WHERE LOWER(email) = @e LIMIT 1";
        cmd.Parameters.AddWithValue("@e", e);
        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (await reader.ReadAsync(ct).ConfigureAwait(false))
            return ReadUserFromReader(reader);
        return null;
    }

    /// <summary>Creates a single-use token; returns the raw token (email to the user). Stores only a hash.</summary>
    public async Task<string> CreateAuthTokenAsync(
        string userId,
        string purpose,
        TimeSpan lifetime,
        CancellationToken ct = default)
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var hash = HashToken(raw);
        var now = DateTimeOffset.UtcNow;
        using var conn = new SqliteConnection(ConnectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        // Invalidate previous unused tokens of same purpose for this user
        using (var clear = conn.CreateCommand())
        {
            clear.CommandText = @"
                DELETE FROM auth_tokens
                WHERE user_id = @u AND purpose = @p AND used_at IS NULL";
            clear.Parameters.AddWithValue("@u", userId.Trim());
            clear.Parameters.AddWithValue("@p", purpose);
            await clear.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO auth_tokens (token_hash, user_id, purpose, expires_at, created_at)
            VALUES (@h, @u, @p, @exp, @c)";
        cmd.Parameters.AddWithValue("@h", hash);
        cmd.Parameters.AddWithValue("@u", userId.Trim());
        cmd.Parameters.AddWithValue("@p", purpose);
        cmd.Parameters.AddWithValue("@exp", (now + lifetime).ToString("o"));
        cmd.Parameters.AddWithValue("@c", now.ToString("o"));
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        return raw;
    }

    /// <summary>Validates and consumes a token. Returns user_id or null.</summary>
    public async Task<string?> ConsumeAuthTokenAsync(
        string rawToken,
        string purpose,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken)) return null;
        var hash = HashToken(rawToken.Trim());
        using var conn = new SqliteConnection(ConnectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        using var tx = (SqliteTransaction)await conn.BeginTransactionAsync(ct).ConfigureAwait(false);
        string? userId = null;
        string? expRaw = null;
        using (var sel = conn.CreateCommand())
        {
            sel.Transaction = tx;
            sel.CommandText = @"
                SELECT user_id, expires_at, used_at FROM auth_tokens
                WHERE token_hash = @h AND purpose = @p LIMIT 1";
            sel.Parameters.AddWithValue("@h", hash);
            sel.Parameters.AddWithValue("@p", purpose);
            using var r = await sel.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (!await r.ReadAsync(ct).ConfigureAwait(false))
            {
                await tx.RollbackAsync(ct).ConfigureAwait(false);
                return null;
            }
            userId = r.GetString(0);
            expRaw = r.GetString(1);
            if (!r.IsDBNull(2))
            {
                await tx.RollbackAsync(ct).ConfigureAwait(false);
                return null; // already used
            }
        }
        if (!DateTimeOffset.TryParse(expRaw, out var exp) || exp < DateTimeOffset.UtcNow)
        {
            await tx.RollbackAsync(ct).ConfigureAwait(false);
            return null;
        }
        using (var upd = conn.CreateCommand())
        {
            upd.Transaction = tx;
            upd.CommandText = "UPDATE auth_tokens SET used_at = @t WHERE token_hash = @h";
            upd.Parameters.AddWithValue("@t", DateTimeOffset.UtcNow.ToString("o"));
            upd.Parameters.AddWithValue("@h", hash);
            await upd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        await tx.CommitAsync(ct).ConfigureAwait(false);
        return userId;
    }

    public async Task<bool> ConfirmEmailAsync(string userId, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(ConnectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE users SET email_confirmed_at = @t WHERE user_id = @id";
        cmd.Parameters.AddWithValue("@t", DateTimeOffset.UtcNow.ToString("o"));
        cmd.Parameters.AddWithValue("@id", userId.Trim());
        return await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false) > 0;
    }

    public async Task<bool> AcceptTermsAsync(string userId, string termsVersion = "1.0", CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return false;
        var trimmed = userId.Trim();
        using var conn = new SqliteConnection(ConnectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO users (user_id, username, password_hash, created_at, terms_accepted_at, terms_version)
            VALUES (@id, @name, '', @t, @t, @v)
            ON CONFLICT(user_id) DO UPDATE SET terms_accepted_at = @t, terms_version = @v;";
        cmd.Parameters.AddWithValue("@id", trimmed);
        cmd.Parameters.AddWithValue("@name", trimmed);
        cmd.Parameters.AddWithValue("@t", DateTimeOffset.UtcNow.ToString("o"));
        cmd.Parameters.AddWithValue("@v", termsVersion);
        return await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false) > 0;
    }

    public async Task<bool> HasAcceptedTermsAsync(string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return false;
        using var conn = new SqliteConnection(ConnectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT terms_accepted_at FROM users WHERE user_id = @id";
        cmd.Parameters.AddWithValue("@id", userId.Trim());
        var val = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return val != null && val != DBNull.Value && !string.IsNullOrWhiteSpace(val.ToString());
    }

    public static string HashToken(string raw)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes("ptm-token:" + raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static UserEntity ReadUserFromReader(SqliteDataReader reader)
    {
        // 0 id, 1 name, 2 hash, 3 xai, 4 gemini, 5 anthropic, 6 fal, 7 role, 8 created, 9 login,
        // 10 balance, 11 granted, 12 used, 13 is_disabled, 14 email, 15 email_confirmed_at
        DateTimeOffset? confirmed = null;
        if (reader.FieldCount > 15 && !reader.IsDBNull(15))
        {
            var raw = reader.GetString(15);
            if (DateTimeOffset.TryParse(raw, out var c)) confirmed = c;
        }
        return new UserEntity
        {
            UserId = reader.GetString(0),
            Username = reader.GetString(1),
            PasswordHash = reader.GetString(2),
            EncryptedXaiApiKey = reader.IsDBNull(3) ? null : reader.GetString(3),
            EncryptedGeminiApiKey = reader.IsDBNull(4) ? null : reader.GetString(4),
            EncryptedAnthropicApiKey = reader.IsDBNull(5) ? null : reader.GetString(5),
            EncryptedFalApiKey = reader.IsDBNull(6) ? null : reader.GetString(6),
            Role = reader.GetString(7),
            CreatedAt = DateTime.TryParse(reader.GetString(8), out var dt) ? dt : DateTime.UtcNow,
            LastLoginAt = reader.IsDBNull(9) ? null : (DateTime.TryParse(reader.GetString(9), out var ldt) ? ldt : null),
            CreditsBalanceUsd = reader.FieldCount > 10 && !reader.IsDBNull(10) ? reader.GetDouble(10) : 0,
            CreditsLifetimeGrantedUsd = reader.FieldCount > 11 && !reader.IsDBNull(11) ? reader.GetDouble(11) : 0,
            CreditsLifetimeUsedUsd = reader.FieldCount > 12 && !reader.IsDBNull(12) ? reader.GetDouble(12) : 0,
            IsDisabled = reader.FieldCount > 13 && !reader.IsDBNull(13) && reader.GetInt64(13) != 0,
            Email = reader.FieldCount > 14 && !reader.IsDBNull(14) ? reader.GetString(14) : null,
            EmailConfirmedAt = confirmed,
        };
    }
}
