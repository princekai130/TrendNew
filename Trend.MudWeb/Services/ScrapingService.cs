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

        public ScrapingService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _config = config;
        }

        public async Task<List<TrendData>> FetchTrendData()
        {
            // 1. Ambil nilai dari appsettings.json
            var baseUrl = _config["ScraperSettings:BaseUrl"] ?? "https://api.apify.com/v2/";
            var apiKey = _config["ScraperSettings:ApiKey"];
            var actorId = "clockworks~tiktok-scraper";

            // Pastikan baseUrl diakhiri dengan /
            if (!baseUrl.EndsWith("/")) baseUrl += "/";

            // 2. Jalankan Actor dan tunggu maksimal 120 detik
            var url = $"{baseUrl}acts/{actorId}/runs?token={apiKey}&waitForFinish=120";

            var input = new
            {
                hashtags = new[] { "skincare" },
                resultsPerPage = 5,
                // Tambahkan baris ini untuk membatasi RAM agar tidak kena limit
                memoryMbytes = 512
            };

            var runResponse = await _httpClient.PostAsJsonAsync(url, input);

            if (runResponse.IsSuccessStatusCode)
            {
                // 3. Gunakan JsonDocument untuk membaca properti secara aman (Menghindari error JsonElement)
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
                    throw new Exception("Dataset ID tidak ditemukan dalam respon Apify.");

                // 4. Ambil data item dari dataset
                var datasetUrl = $"{baseUrl}datasets/{datasetId}/items?token={apiKey}";
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                var rawData = await _httpClient.GetFromJsonAsync<List<ApifyTikTokResult>>(datasetUrl, options);

                return rawData?.Select(item => new TrendData
                {
                    // Di dalam mapping FetchTrendData
                    Keyword = item.text?.Length > 100 ? item.text.Substring(0, 97) + "..." : (item.text ?? "TikTok Trend"),
                    Platform = "TikTok",
                    // Kalkulasi Engagement Score berdasarkan stats (diggCount/likes)
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
    }
}