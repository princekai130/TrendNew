using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;
using System.Text.Json;
using Trend.MudWeb.Models;

namespace Trend.MudWeb.Services
{
    public class ScrapingService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;
        private readonly TrendRepo _repo; // 1. Tambahkan field repo

        // 2. Update Constructor untuk Inject TrendRepo
        public ScrapingService(HttpClient httpClient, IConfiguration config, TrendRepo repo)
        {
            _httpClient = httpClient;
            _config = config;
            _repo = repo;
        }

        public async Task<List<TrendData>> FetchTrendData()
        {
            // 1. Ambil nilai dasar dari config
            var baseUrl = _config["ScraperSettings:BaseUrl"] ?? "https://api.apify.com/v2/";
            var apiKey = _config["ScraperSettings:ApiKey"];
            var actorId = "clockworks~tiktok-scraper";

            if (!baseUrl.EndsWith("/")) baseUrl += "/";

            // 2. AMBIL HASHTAG DINAMIS DARI DATABASE (Hashtag Manager Admin)
            var hashtagFromDb = await _repo.GetSettingAsync("TargetHashtag") ?? "skincare";

            // 3. Jalankan Actor
            var url = $"{baseUrl}acts/{actorId}/runs?token={apiKey}&waitForFinish=120";

            // 4. Input diletakkan di sini (HANYA SATU KALI)
            var input = new
            {
                hashtags = new[] { hashtagFromDb },
                resultsPerPage = 5,
                memoryMbytes = 512
            };

            var runResponse = await _httpClient.PostAsJsonAsync(url, input);

            if (runResponse.IsSuccessStatusCode)
            {
                using var document = await JsonDocument.ParseAsync(await runResponse.Content.ReadAsStreamAsync());
                var root = document.RootElement;

                string? datasetId = null;
                if (root.TryGetProperty("data", out var dataElement))
                {
                    if (dataElement.TryGetProperty("defaultDatasetId", out var idElement))
                    {
                        datasetId = idElement.GetString();
                    }
                }

                if (string.IsNullOrEmpty(datasetId))
                    throw new Exception("Dataset ID tidak ditemukan.");

                var datasetUrl = $"{baseUrl}datasets/{datasetId}/items?token={apiKey}";
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                var rawData = await _httpClient.GetFromJsonAsync<List<ApifyTikTokResult>>(datasetUrl, options);

                return rawData?.Select(item => new TrendData
                {
                    Keyword = item.text?.Length > 100 ? item.text.Substring(0, 97) + "..." : (item.text ?? "TikTok Trend"),
                    Platform = "TikTok",
                    EngagementScore = Math.Min(100, (double)(item.stats?.diggCount ?? 0) / 1000),
                    CreatedAt = DateTime.Now
                }).ToList() ?? new List<TrendData>();
            }
            else
            {
                var error = await runResponse.Content.ReadAsStringAsync();
                throw new Exception($"Gagal menjalankan Scraper. Detail: {error}");
            }
        }

        public async Task<List<string>> FetchCompetitorVideos(string username)
        {
            var baseUrl = _config["ScraperSettings:BaseUrl"] ?? "https://api.apify.com/v2/";
            var apiKey = _config["ScraperSettings:ApiKey"];
            var actorId = "clockworks~tiktok-scraper";

            var url = $"{baseUrl}acts/{actorId}/run-sync-get-dataset-items?token={apiKey}";

            var input = new
            {
                profiles = new[] { username },
                resultsPerPage = 3,
                memoryMbytes = 512
            };

            var response = await _httpClient.PostAsJsonAsync(url, input);

            if (response.IsSuccessStatusCode)
            {
                var rawData = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
                return rawData?
                    .Select(item => item.TryGetProperty("webVideoUrl", out var urlProp) ? urlProp.GetString() : null)
                    .Where(u => !string.IsNullOrEmpty(u))
                    .Cast<string>()
                    .ToList() ?? new List<string>();
            }

            return new List<string>();
        }
    }
}