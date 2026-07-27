using PageToMovie.Web.Services;
using Xunit;

namespace PageToMovie.Tests;

public class WebPageHydrationGateTests
{
    [Fact]
    public void QualityGate_AdminSession_Uninitialized_State_Hydrates_Cleanly()
    {
        var session = new AdminSessionService(js: null);

        Assert.False(session.IsAuthenticated, "Cold session service must start un-authenticated.");
        Assert.False(session.IsAdmin, "Cold session service must default to non-admin.");
        Assert.Equal("local", session.UserId);
    }
}
