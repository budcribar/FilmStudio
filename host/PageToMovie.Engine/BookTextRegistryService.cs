using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using PageToMovie.Core.Options;

namespace PageToMovie.Engine;

/// <summary>Server-side, content-addressed identity for uploaded book text.</summary>
public sealed class BookTextRegistryService
{
    private readonly string _connectionString;

    public BookTextRegistryService(IOptions<PageToMovieOptions> options)
    {
        var dir = UserDatabaseService.ResolveDataDirectory(options.Value.WorkspaceRoot);
        Directory.CreateDirectory(dir);
        _connectionString = $"Data Source={Path.Combine(dir, "pagetomovie.db")};Cache=Shared;Pooling=True;";
        EnsureSchema();
    }

    public async Task<BookTextIdentity> RegisterAsync(
        string text, string userId, string? projectId = null, string visibility = "Private",
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Book text is required.", nameof(text));
        var bytes = Encoding.UTF8.GetBytes(text);
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var id = "book_" + hash[..24];
        var now = DateTime.UtcNow.ToString("o");

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        await using var tx = await conn.BeginTransactionAsync(ct).ConfigureAwait(false);
        await using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = (SqliteTransaction)tx;
            cmd.CommandText = """
                INSERT INTO book_texts (book_id, sha256, text_content, byte_count, created_at)
                VALUES (@id, @hash, @text, @bytes, @now)
                ON CONFLICT(sha256) DO NOTHING;
                INSERT INTO book_text_access (book_id, user_id, project_id, visibility_mode, linked_at)
                VALUES (@id, @user, @project, @visibility, @now)
                ON CONFLICT(book_id, user_id, project_id) DO NOTHING;
                """;
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@hash", hash);
            cmd.Parameters.AddWithValue("@text", text);
            cmd.Parameters.AddWithValue("@bytes", bytes.Length);
            cmd.Parameters.AddWithValue("@now", now);
            cmd.Parameters.AddWithValue("@user", userId);
            cmd.Parameters.AddWithValue("@project", projectId ?? "");
            cmd.Parameters.AddWithValue("@visibility", NormalizeVisibility(visibility));
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        await tx.CommitAsync(ct).ConfigureAwait(false);
        return new(id, hash, bytes.Length, text);
    }

    public async Task<BookTextIdentity?> ResolveAsync(
        string idOrHash, string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idOrHash)) return null;
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT b.book_id, b.sha256, b.byte_count, b.text_content
            FROM book_texts b
            JOIN book_text_access a ON a.book_id = b.book_id
            WHERE (b.book_id = @key OR b.sha256 = LOWER(@key))
              AND (a.user_id = @user OR a.visibility_mode IN ('Public', 'Forkable'))
            LIMIT 1;
            """;
        cmd.Parameters.AddWithValue("@key", idOrHash.Trim());
        cmd.Parameters.AddWithValue("@user", userId);
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false)
            ? new(reader.GetString(0), reader.GetString(1), reader.GetInt32(2), reader.GetString(3))
            : null;
    }

    public async Task LinkToProjectAsync(
        string bookId, string userId, string projectId, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO book_text_access (book_id, user_id, project_id, visibility_mode, linked_at)
            SELECT b.book_id, @user, @project, 'Private', @now
            FROM book_texts b
            WHERE b.book_id = @book AND EXISTS (
                SELECT 1 FROM book_text_access a
                WHERE a.book_id=b.book_id
                  AND (a.user_id=@user OR a.visibility_mode='Forkable'))
            ON CONFLICT(book_id, user_id, project_id) DO NOTHING;
            """;
        cmd.Parameters.AddWithValue("@book", bookId);
        cmd.Parameters.AddWithValue("@user", userId);
        cmd.Parameters.AddWithValue("@project", projectId);
        cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
        if (await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false) == 0)
            throw new KeyNotFoundException($"Book '{bookId}' does not exist.");
    }

    public async Task SetProjectVisibilityAsync(
        string userId, string projectId, string visibility, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE book_text_access SET visibility_mode=@visibility
            WHERE user_id=@user AND project_id=@project;
            """;
        cmd.Parameters.AddWithValue("@visibility", NormalizeVisibility(visibility));
        cmd.Parameters.AddWithValue("@user", userId);
        cmd.Parameters.AddWithValue("@project", projectId);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task LinkForkAsync(
        string sourceProjectId,
        string targetUserId,
        string targetProjectId,
        bool invitationAuthorized,
        CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO book_text_access (book_id, user_id, project_id, visibility_mode, linked_at)
            SELECT DISTINCT book_id, @user, @target, 'Private', @now
            FROM book_text_access
            WHERE project_id=@source
              AND (@invited=1 OR visibility_mode='Forkable')
            ON CONFLICT(book_id, user_id, project_id) DO NOTHING;
            """;
        cmd.Parameters.AddWithValue("@source", sourceProjectId);
        cmd.Parameters.AddWithValue("@user", targetUserId);
        cmd.Parameters.AddWithValue("@target", targetProjectId);
        cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
        cmd.Parameters.AddWithValue("@invited", invitationAuthorized ? 1 : 0);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<DerivedBookArtifact> RegisterArtifactAsync(
        string bookId,
        string userId,
        string artifactKind,
        string content,
        string modelId,
        string promptVersion,
        string promptSha256,
        double temperature,
        string behaviorVersionsJson,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Derived artifact content is required.", nameof(content));
        var derivationHash = DerivationHash(
            bookId, artifactKind, modelId, promptVersion, promptSha256, temperature, behaviorVersionsJson);
        var artifactId = "artifact_" + derivationHash[..24];
        var contentHash = Hash(content);
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO book_derived_artifacts
                (artifact_id, derivation_sha256, book_id, artifact_kind, content, content_sha256,
                 model_id, prompt_version, prompt_sha256, temperature, behavior_versions_json, created_at)
            SELECT @id, @derivation, @book, @kind, @content, @contentHash,
                   @model, @promptVersion, @promptHash, @temperature, @behaviors, @now
            WHERE EXISTS (
                SELECT 1 FROM book_text_access
                WHERE book_id = @book AND user_id = @user)
            ON CONFLICT(derivation_sha256) DO NOTHING;
            """;
        cmd.Parameters.AddWithValue("@id", artifactId);
        cmd.Parameters.AddWithValue("@derivation", derivationHash);
        cmd.Parameters.AddWithValue("@book", bookId);
        cmd.Parameters.AddWithValue("@user", userId);
        cmd.Parameters.AddWithValue("@kind", artifactKind);
        cmd.Parameters.AddWithValue("@content", content);
        cmd.Parameters.AddWithValue("@contentHash", contentHash);
        cmd.Parameters.AddWithValue("@model", modelId);
        cmd.Parameters.AddWithValue("@promptVersion", promptVersion);
        cmd.Parameters.AddWithValue("@promptHash", promptSha256.ToLowerInvariant());
        cmd.Parameters.AddWithValue("@temperature", temperature);
        cmd.Parameters.AddWithValue("@behaviors", behaviorVersionsJson);
        cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
        if (await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false) == 0)
        {
            await using var access = conn.CreateCommand();
            access.CommandText = "SELECT 1 FROM book_text_access WHERE book_id=@book AND user_id=@user LIMIT 1;";
            access.Parameters.AddWithValue("@book", bookId);
            access.Parameters.AddWithValue("@user", userId);
            if (await access.ExecuteScalarAsync(ct).ConfigureAwait(false) is null)
                throw new UnauthorizedAccessException("The caller does not have access to this book identity.");
        }
        return new(artifactId, derivationHash, bookId, artifactKind, contentHash, content);
    }

    public Task<DerivedBookArtifact?> FindArtifactAsync(
        string bookId,
        string userId,
        string artifactKind,
        string modelId,
        string promptVersion,
        string promptSha256,
        double temperature,
        string behaviorVersionsJson,
        CancellationToken ct = default) =>
        ResolveArtifactByDerivationHashAsync(
            DerivationHash(bookId, artifactKind, modelId, promptVersion, promptSha256,
                temperature, behaviorVersionsJson), userId, ct);

    public async Task<DerivedBookArtifact?> ResolveArtifactAsync(
        string artifactId, string userId, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT d.artifact_id, d.derivation_sha256, d.book_id, d.artifact_kind,
                   d.content_sha256, d.content
            FROM book_derived_artifacts d
            JOIN book_text_access a ON a.book_id=d.book_id
            WHERE d.artifact_id=@id
              AND (a.user_id=@user OR a.visibility_mode IN ('Public', 'Forkable')) LIMIT 1;
            """;
        cmd.Parameters.AddWithValue("@id", artifactId);
        cmd.Parameters.AddWithValue("@user", userId);
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false)
            ? new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5))
            : null;
    }

    private async Task<DerivedBookArtifact?> ResolveArtifactByDerivationHashAsync(
        string derivationHash, string userId, CancellationToken ct)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT d.artifact_id, d.derivation_sha256, d.book_id, d.artifact_kind,
                   d.content_sha256, d.content
            FROM book_derived_artifacts d
            JOIN book_text_access a ON a.book_id=d.book_id
            WHERE d.derivation_sha256=@hash
              AND (a.user_id=@user OR a.visibility_mode IN ('Public', 'Forkable')) LIMIT 1;
            """;
        cmd.Parameters.AddWithValue("@hash", derivationHash);
        cmd.Parameters.AddWithValue("@user", userId);
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false)
            ? new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5))
            : null;
    }

    private void EnsureSchema()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS book_texts (
                book_id TEXT PRIMARY KEY,
                sha256 TEXT NOT NULL UNIQUE,
                text_content TEXT NOT NULL,
                byte_count INTEGER NOT NULL,
                created_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS book_text_access (
                book_id TEXT NOT NULL,
                user_id TEXT NOT NULL,
                project_id TEXT NOT NULL DEFAULT '',
                visibility_mode TEXT NOT NULL DEFAULT 'Private',
                linked_at TEXT NOT NULL,
                PRIMARY KEY (book_id, user_id, project_id),
                FOREIGN KEY (book_id) REFERENCES book_texts(book_id)
            );
            CREATE INDEX IF NOT EXISTS idx_book_text_access_user ON book_text_access(user_id, book_id);
            CREATE TABLE IF NOT EXISTS book_derived_artifacts (
                artifact_id TEXT PRIMARY KEY,
                derivation_sha256 TEXT NOT NULL UNIQUE,
                book_id TEXT NOT NULL,
                artifact_kind TEXT NOT NULL,
                content TEXT NOT NULL,
                content_sha256 TEXT NOT NULL,
                model_id TEXT NOT NULL,
                prompt_version TEXT NOT NULL,
                prompt_sha256 TEXT NOT NULL,
                temperature REAL NOT NULL,
                behavior_versions_json TEXT NOT NULL,
                created_at TEXT NOT NULL,
                FOREIGN KEY (book_id) REFERENCES book_texts(book_id)
            );
            CREATE INDEX IF NOT EXISTS idx_book_artifacts_book_kind
                ON book_derived_artifacts(book_id, artifact_kind);
            """;
        cmd.ExecuteNonQuery();
        try
        {
            using var migrate = conn.CreateCommand();
            migrate.CommandText = "ALTER TABLE book_text_access ADD COLUMN visibility_mode TEXT NOT NULL DEFAULT 'Private';";
            migrate.ExecuteNonQuery();
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 1)
        {
            // Existing databases already containing the column are current.
        }
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static string DerivationHash(
        string bookId, string artifactKind, string modelId, string promptVersion,
        string promptSha256, double temperature, string behaviorVersionsJson) =>
        Hash(string.Join("\n", bookId, artifactKind, modelId, promptVersion,
            promptSha256.ToLowerInvariant(),
            temperature.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            behaviorVersionsJson));

    private static string NormalizeVisibility(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "public" => "Public",
        "open" or "forkable" => "Forkable",
        _ => "Private",
    };
}

public sealed record BookTextIdentity(string BookId, string Sha256, int ByteCount, string Text);
public sealed record DerivedBookArtifact(
    string ArtifactId,
    string DerivationSha256,
    string BookId,
    string ArtifactKind,
    string ContentSha256,
    string Content);
