using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PageToMovie.Api.Auth;
using PageToMovie.Core.Options;
using PageToMovie.Engine;
using PageToMovie.Engine.Abstractions;
using Xunit;

namespace PageToMovie.Tests;

public class AuthGateTests
{
    private sealed class FakeUserContext : IUserContext
    {
        public string UserId { get; init; } = "user_1";
        public bool IsAdmin { get; init; }
        public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
        public bool IsAuthenticated { get; init; } = true;
        public string? RequestApiKey => null;
    }

    private static (UserDatabaseService Db, string Root) MakeDb()
    {
        var root = Path.Combine(Path.GetTempPath(), "ptm_authgate_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var opts = Options.Create(new PageToMovieOptions { WorkspaceRoot = root });
        var db = new UserDatabaseService(opts, null, NullLogger<UserDatabaseService>.Instance);
        return (db, root);
    }

    private static void Cleanup(string root)
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(root, true); } catch { /* best effort */ }
    }

    private static int? StatusOf(IResult? result)
    {
        return result switch
        {
            IStatusCodeHttpResult s => s.StatusCode,
            _ => null,
        };
    }

    [Fact]
    public async Task Blocks_signed_in_user_who_has_not_accepted_terms()
    {
        var (db, root) = MakeDb();
        try
        {
            var user = new FakeUserContext { UserId = "user_no_terms" };
            var opts = Options.Create(new PageToMovieOptions { Auth = new AuthOptions { RequireLogin = true } });

            var result = await AuthGate.RequireTermsAcceptedAsync(user, db, opts);

            Assert.NotNull(result);
            Assert.Equal((int)HttpStatusCode.Forbidden, StatusOf(result));
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task Allows_signed_in_user_who_has_accepted_terms()
    {
        var (db, root) = MakeDb();
        try
        {
            var user = new FakeUserContext { UserId = "user_accepted" };
            await db.AcceptTermsAsync(user.UserId);
            var opts = Options.Create(new PageToMovieOptions { Auth = new AuthOptions { RequireLogin = true } });

            var result = await AuthGate.RequireTermsAcceptedAsync(user, db, opts);

            Assert.Null(result);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task Blocks_unauthenticated_caller_before_checking_terms()
    {
        var (db, root) = MakeDb();
        try
        {
            var user = new FakeUserContext { UserId = "anon", IsAuthenticated = false, IsAdmin = false };
            var opts = Options.Create(new PageToMovieOptions { Auth = new AuthOptions { RequireLogin = true } });

            var result = await AuthGate.RequireTermsAcceptedAsync(user, db, opts);

            Assert.NotNull(result);
            Assert.Equal((int)HttpStatusCode.Unauthorized, StatusOf(result));
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task Admin_bypasses_terms_check_like_RequireLogin_does()
    {
        var (db, root) = MakeDb();
        try
        {
            var user = new FakeUserContext { UserId = "admin_1", IsAdmin = true };
            var opts = Options.Create(new PageToMovieOptions { Auth = new AuthOptions { RequireLogin = true } });

            var result = await AuthGate.RequireTermsAcceptedAsync(user, db, opts);

            Assert.Null(result);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task Skips_entirely_when_RequireLogin_is_disabled_tests_or_loadsim()
    {
        var (db, root) = MakeDb();
        try
        {
            var user = new FakeUserContext { UserId = "loadsim_user", IsAuthenticated = false, IsAdmin = false };
            var opts = Options.Create(new PageToMovieOptions { Auth = new AuthOptions { RequireLogin = false } });

            var result = await AuthGate.RequireTermsAcceptedAsync(user, db, opts);

            Assert.Null(result);
        }
        finally
        {
            Cleanup(root);
        }
    }
}
