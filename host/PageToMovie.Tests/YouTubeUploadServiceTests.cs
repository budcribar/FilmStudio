using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using PageToMovie.Engine;
using Xunit;

namespace PageToMovie.Tests
{
    public class YouTubeUploadServiceTests
    {
        [Fact]
        public void IsConfigured_Returns_False_When_Credentials_Missing()
        {
            var inMemoryConfig = new Dictionary<string, string>();
            var config = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig!).Build();

            var service = new YouTubeUploadService(null!, config);
            Assert.False(service.IsConfigured());
        }

        [Fact]
        public void IsConfigured_Returns_True_When_Credentials_Present()
        {
            var inMemoryConfig = new Dictionary<string, string>
            {
                { "YouTube:ClientId", "mock-client-id" },
                { "YouTube:ClientSecret", "mock-client-secret" },
                { "YouTube:RefreshToken", "mock-refresh-token" }
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig!).Build();

            var service = new YouTubeUploadService(null!, config);
            Assert.True(service.IsConfigured());
        }

        [Fact]
        public async Task UploadVideoAsync_Returns_Error_If_File_Not_Found()
        {
            var inMemoryConfig = new Dictionary<string, string>
            {
                { "YouTube:ClientId", "mock-client-id" },
                { "YouTube:ClientSecret", "mock-client-secret" },
                { "YouTube:RefreshToken", "mock-refresh-token" }
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig!).Build();

            var service = new YouTubeUploadService(null!, config);
            var req = new YouTubeUploadRequest
            {
                Title = "Test Movie",
                VideoFilePath = "non_existent_file.mp4"
            };

            var res = await service.UploadVideoAsync(req);
            Assert.False(res.Success);
            Assert.Contains("not found", res.ErrorMessage);
        }
    }
}
