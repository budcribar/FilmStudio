using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace PageToMovie.Engine
{
    public class YouTubeUploadRequest
    {
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public bool MadeForKids { get; set; } = false;
        public bool IsAiSyntheticContent { get; set; } = true;
        public string PrivacyStatus { get; set; } = "public";
        public string CategoryId { get; set; } = "1"; // Film & Animation
        public string[] Tags { get; set; } = new[] { "AI Movie", "PageToMovie", "Fountain Screenplay" };
        public string VideoFilePath { get; set; } = "";
        public string? ReplacingOldYoutubeId { get; set; }
    }

    public class YouTubeUploadResult
    {
        public bool Success { get; set; }
        public string? YoutubeId { get; set; }
        public string? VideoUrl { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Service managing automated uploads, replacement, and metadata declaration for YouTube Data API v3.
    /// </summary>
    public class YouTubeUploadService
    {
        private readonly ILogger<YouTubeUploadService> _logger;
        private readonly IConfiguration _config;
        private readonly HttpClient _http;

        public YouTubeUploadService(
            ILogger<YouTubeUploadService> logger,
            IConfiguration config,
            HttpClient? httpClient = null)
        {
            _logger = logger;
            _config = config;
            _http = httpClient ?? new HttpClient();
        }

        public bool IsConfigured()
        {
            var clientId = _config["YouTube:ClientId"] ?? _config["YouTube__ClientId"];
            var clientSecret = _config["YouTube:ClientSecret"] ?? _config["YouTube__ClientSecret"];
            var refreshToken = _config["YouTube:RefreshToken"] ?? _config["YouTube__RefreshToken"];
            return !string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(clientSecret) && !string.IsNullOrWhiteSpace(refreshToken);
        }

        /// <summary>
        /// Uploads a movie file to YouTube via YouTube Data API v3.
        /// </summary>
        public async Task<YouTubeUploadResult> UploadVideoAsync(YouTubeUploadRequest request, CancellationToken ct = default)
        {
            if (!IsConfigured())
            {
                return new YouTubeUploadResult
                {
                    Success = false,
                    ErrorMessage = "YouTube Data API credentials (ClientId, ClientSecret, RefreshToken) are not configured."
                };
            }

            if (!File.Exists(request.VideoFilePath))
            {
                return new YouTubeUploadResult
                {
                    Success = false,
                    ErrorMessage = $"Video file not found at path: {request.VideoFilePath}"
                };
            }

            try
            {
                var accessToken = await RefreshAccessTokenAsync(ct);
                if (string.IsNullOrWhiteSpace(accessToken))
                {
                    return new YouTubeUploadResult
                    {
                        Success = false,
                        ErrorMessage = "Failed to obtain access token from Google OAuth2 server."
                    };
                }

                // 1. Prepare video metadata JSON payload
                var metadata = new
                {
                    snippet = new
                    {
                        title = request.Title.Length > 100 ? request.Title.Substring(0, 100) : request.Title,
                        description = request.Description,
                        tags = request.Tags,
                        categoryId = request.CategoryId
                    },
                    status = new
                    {
                        privacyStatus = request.PrivacyStatus,
                        madeForKids = request.MadeForKids,
                        selfDeclaredMadeForKids = request.MadeForKids
                    }
                };

                string metadataJson = JsonSerializer.Serialize(metadata);

                // 2. Initiate Resumable Upload Session
                var initRequest = new HttpRequestMessage(HttpMethod.Post, "https://www.googleapis.com/upload/youtube/v3/videos?uploadType=resumable&part=snippet,status");
                initRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                initRequest.Headers.Add("X-Upload-Content-Type", "video/mp4");
                initRequest.Headers.Add("X-Upload-Content-Length", new FileInfo(request.VideoFilePath).Length.ToString());
                initRequest.Content = new StringContent(metadataJson, Encoding.UTF8, "application/json");

                var initResponse = await _http.SendAsync(initRequest, ct);
                if (!initResponse.IsSuccessStatusCode)
                {
                    string errBody = await initResponse.Content.ReadAsStringAsync(ct);
                    return new YouTubeUploadResult
                    {
                        Success = false,
                        ErrorMessage = $"Failed to initiate YouTube upload session. HTTP {(int)initResponse.StatusCode}: {errBody}"
                    };
                }

                var uploadUrl = initResponse.Headers.Location?.ToString();
                if (string.IsNullOrWhiteSpace(uploadUrl))
                {
                    return new YouTubeUploadResult
                    {
                        Success = false,
                        ErrorMessage = "YouTube API did not return a valid resumable upload URL location header."
                    };
                }

                // 3. Upload raw MP4 bytes
                using var fileStream = File.OpenRead(request.VideoFilePath);
                var uploadRequest = new HttpRequestMessage(HttpMethod.Put, uploadUrl);
                uploadRequest.Content = new StreamContent(fileStream);
                uploadRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");

                var uploadResponse = await _http.SendAsync(uploadRequest, ct);
                if (!uploadResponse.IsSuccessStatusCode)
                {
                    string errBody = await uploadResponse.Content.ReadAsStringAsync(ct);
                    return new YouTubeUploadResult
                    {
                        Success = false,
                        ErrorMessage = $"Failed streaming video bytes to YouTube. HTTP {(int)uploadResponse.StatusCode}: {errBody}"
                    };
                }

                string responseJson = await uploadResponse.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(responseJson);
                string newYoutubeId = doc.RootElement.TryGetProperty("id", out var idProp)
                    ? (idProp.GetString() ?? "")
                    : "";

                if (string.IsNullOrWhiteSpace(newYoutubeId))
                {
                    _logger?.LogError("YouTube API upload response did not contain a video id. Response: {Body}", responseJson);
                    return new YouTubeUploadResult
                    {
                        Success = false,
                        ErrorMessage = $"YouTube upload succeeded but returned no video id. Response: {responseJson}"
                    };
                }

                _logger?.LogInformation("Successfully uploaded video to YouTube! Video ID: {YoutubeId}", newYoutubeId);

                // 4. Cleanup old video if replacing Version 1
                if (!string.IsNullOrWhiteSpace(request.ReplacingOldYoutubeId))
                {
                    _ = DeleteOldVideoAsync(request.ReplacingOldYoutubeId, accessToken, ct);
                }

                // 5. Delete local MP4 from server disk to save space
                try
                {
                    File.Delete(request.VideoFilePath);
                    _logger?.LogInformation("Deleted temporary local MP4 file {Path} after YouTube upload.", request.VideoFilePath);
                }
                catch { }

                return new YouTubeUploadResult
                {
                    Success = true,
                    YoutubeId = newYoutubeId,
                    VideoUrl = $"https://www.youtube.com/watch?v={newYoutubeId}"
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Exception during YouTube API video upload execution.");
                return new YouTubeUploadResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        private async Task<string?> RefreshAccessTokenAsync(CancellationToken ct)
        {
            var clientId = _config["YouTube:ClientId"] ?? _config["YouTube__ClientId"];
            var clientSecret = _config["YouTube:ClientSecret"] ?? _config["YouTube__ClientSecret"];
            var refreshToken = _config["YouTube:RefreshToken"] ?? _config["YouTube__RefreshToken"];

            var form = new System.Collections.Generic.Dictionary<string, string>
            {
                { "client_id", clientId! },
                { "client_secret", clientSecret! },
                { "refresh_token", refreshToken! },
                { "grant_type", "refresh_token" }
            };

            var response = await _http.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(form), ct);
            if (!response.IsSuccessStatusCode) return null;

            string body = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("access_token", out var tokenProp)
                ? tokenProp.GetString()
                : null;
        }

        private async Task DeleteOldVideoAsync(string oldYoutubeId, string accessToken, CancellationToken ct)
        {
            try
            {
                var req = new HttpRequestMessage(HttpMethod.Delete, $"https://www.googleapis.com/youtube/v3/videos?id={oldYoutubeId}");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                await _http.SendAsync(req, ct);
                _logger?.LogInformation("Deleted old YouTube video v1 ID: {OldId}", oldYoutubeId);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed deleting obsolete YouTube video {OldId}", oldYoutubeId);
            }
        }
    }
}
