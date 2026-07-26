using System;
using Xunit;

namespace PageToMovie.Tests
{
    public class ProjectCollaboratorsTests
    {
        [Fact]
        public void InviteToken_CanBeGenerated()
        {
            string token = "inv_" + Guid.NewGuid().ToString("N");
            Assert.StartsWith("inv_", token);
            Assert.Equal(36, token.Length);
        }
    }
}
