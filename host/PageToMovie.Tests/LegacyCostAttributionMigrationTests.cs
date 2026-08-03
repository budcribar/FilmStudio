using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using PageToMovie.Core.Options;
using PageToMovie.Engine;
using Xunit;

namespace PageToMovie.Tests;

public class LegacyCostAttributionMigrationTests
{
    [Fact]
    public void V5_migration_assigns_orphan_api_calls_to_budcribar_development()
    {
        var root = Path.Combine(Path.GetTempPath(), "ptm-mig-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "data"));
        var dbPath = Path.Combine(root, "data", "pagetomovie.db");

        // Seed a pre-v5-shaped DB with orphan cost rows (user_version 4).
        using (var conn = new SqliteConnection($"Data Source={dbPath}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                PRAGMA user_version = 4;
                CREATE TABLE users (
                    user_id TEXT PRIMARY KEY,
                    username TEXT,
                    password_hash TEXT,
                    role TEXT,
                    created_at TEXT,
                    email_confirmed_at TEXT
                );
                CREATE TABLE user_api_calls (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    user_id TEXT NOT NULL,
                    ts TEXT NOT NULL,
                    project_id TEXT,
                    kind TEXT NOT NULL,
                    ok INTEGER NOT NULL DEFAULT 1,
                    estimated_usd REAL,
                    charge_usd REAL,
                    charge_multiplier REAL
                );
                CREATE TABLE credit_ledger (
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
                CREATE TABLE generation_errors (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ts TEXT NOT NULL,
                    user_id TEXT,
                    project_id TEXT,
                    stage TEXT NOT NULL,
                    error_type TEXT NOT NULL,
                    attempt INTEGER NOT NULL DEFAULT 1,
                    resolved INTEGER NOT NULL DEFAULT 0
                );
                INSERT INTO user_api_calls (user_id, ts, project_id, kind, ok, estimated_usd)
                VALUES ('local', '2026-01-01T00:00:00Z', NULL, 'chat', 1, 1.25);
                INSERT INTO user_api_calls (user_id, ts, project_id, kind, ok, estimated_usd)
                VALUES ('alice', '2026-01-02T00:00:00Z', 'Buster', 'video', 1, 9.99);
                INSERT INTO generation_errors (ts, user_id, project_id, stage, error_type)
                VALUES ('2026-01-01T00:00:00Z', '', NULL, 'video', 'timeout');
            ";
            cmd.ExecuteNonQuery();
        }

        var opts = Options.Create(new PageToMovieOptions
        {
            WorkspaceRoot = root,
            Billing = new BillingOptions
            {
                ChargeMultiplier = 1.5,
                LegacyCostOwnerUserId = "budcribar",
                LegacyCostOwnerUsername = "Bud Cribar",
                LegacyCostProjectId = "development",
            },
        });

        // Constructor runs EnsureDatabaseInitialized → v5 migration.
        _ = new UserDatabaseService(opts);

        using (var conn = new SqliteConnection($"Data Source={dbPath}"))
        {
            conn.Open();
            using var ver = conn.CreateCommand();
            ver.CommandText = "PRAGMA user_version;";
            Assert.True(Convert.ToInt32(ver.ExecuteScalar()) >= 5);

            using var q = conn.CreateCommand();
            q.CommandText = "SELECT user_id, project_id, charge_usd, charge_multiplier FROM user_api_calls WHERE estimated_usd = 1.25";
            using var r = q.ExecuteReader();
            Assert.True(r.Read());
            Assert.Equal("budcribar", r.GetString(0));
            Assert.Equal("development", r.GetString(1));
            Assert.Equal(1.875, r.GetDouble(2), 3);
            Assert.Equal(1.5, r.GetDouble(3), 3);

            using var q2 = conn.CreateCommand();
            q2.CommandText = "SELECT user_id, project_id FROM user_api_calls WHERE estimated_usd = 9.99";
            using var r2 = q2.ExecuteReader();
            Assert.True(r2.Read());
            Assert.Equal("alice", r2.GetString(0)); // real user left alone
            Assert.Equal("Buster", r2.GetString(1));

            using var q3 = conn.CreateCommand();
            q3.CommandText = "SELECT user_id, project_id FROM generation_errors LIMIT 1";
            using var r3 = q3.ExecuteReader();
            Assert.True(r3.Read());
            Assert.Equal("budcribar", r3.GetString(0));
            Assert.Equal("development", r3.GetString(1));
        }

        try { Directory.Delete(root, recursive: true); } catch { /* ignore */ }
    }
}
