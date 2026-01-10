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
        private readonly TrendRepo _repo;

        public ScrapingService(HttpClient httpClient, IConfiguration config, TrendRepo repo)
        {
            _httpClient = httpClient;
            _config = config;
            _repo = repo;
        }

        public async Task<List<TrendData>> FetchTrendDataByNiche(int nicheId, int userId)
        {
            // 1. Tentukan Hashtag berdasarkan Niche
            string hashtag = nicheId switch
            {
                1 => "beauty",
                2 => "technology",
                3 => "foodie",
                4 => "travel",
                5 => "career",
                _ => "trending"
            };

            var baseUrl = _config["ScraperSettings:BaseUrl"] ?? "https://api.apify.com/v2/";
            var apiKey = _config["ScraperSettings:ApiKey"];
            var actorId = _config["ScraperSettings:TikTokActorId"] ?? "clockworks~tiktok-scraper";
            var url = $"{baseUrl}acts/{actorId}/runs?token={apiKey}&waitForFinish=120";

            var input = new { hashtags = new[] { hashtag }, resultsPerPage = 5, memoryMbytes = 512 };
            var runResponse = await _httpClient.PostAsJsonAsync(url, input);

            if (runResponse.IsSuccessStatusCode)
            {
                using var document = await JsonDocument.ParseAsync(await runResponse.Content.ReadAsStreamAsync());
                var root = document.RootElement;
                string? datasetId = root.GetProperty("data").GetProperty("defaultDatasetId").GetString();

                if (!string.IsNullOrEmpty(datasetId))
                {
                    var datasetUrl = $"{baseUrl}datasets/{datasetId}/items?token={apiKey}";
                    var rawData = await _httpClient.GetFromJsonAsync<List<JsonElement>>(datasetUrl);

                    var trends = new List<TrendData>();

                    foreach (var item in rawData ?? new List<JsonElement>())
                    {
                        // --- EXTRACT LIKES/STATS ---
                        long diggCount = 0;
                        if (item.TryGetProperty("diggCount", out var d1)) diggCount = d1.GetInt64();
                        else if (item.TryGetProperty("stats", out var s) && s.TryGetProperty("diggCount", out var d2)) diggCount = d2.GetInt64();

                        // --- EXTRACT SOUND DATA (FITUR BARU) ---
                        string? musicName = null;
                        string? musicUrl = null;

                        // Mencoba mencari info musik di berbagai struktur JSON TikTok Apify
                        if (item.TryGetProperty("musicMeta", out var m1))
                        {
                            if (m1.TryGetProperty("musicName", out var mn)) musicName = mn.GetString();
                            if (m1.TryGetProperty("playUrl", out var mu)) musicUrl = mu.GetString();
                        }
                        else if (item.TryGetProperty("music", out var m2))
                        {
                            if (m2.TryGetProperty("title", out var mt)) musicName = mt.GetString();
                            if (m2.TryGetProperty("playUrl", out var mpu)) musicUrl = mpu.GetString();
                        }

                        double calculatedScore = Math.Min(100.0, (double)diggCount / 500.0);
                        if (diggCount > 0 && calculatedScore < 1) calculatedScore = 5.0;

                        var trendText = item.TryGetProperty("text", out var t) ? t.GetString() : "TikTok Trend";

                        var data = new TrendData
                        {
                            Keyword = trendText,
                            Platform = "TikTok",
                            GrowthScore = calculatedScore, // GANTI DARI EngagementScore
                            CreatedAt = DateTime.Now,
                            SoundName = musicName,
                            SoundUrl = musicUrl
                        };

                        // --- AUTO NOTIFICATION LOGIC ---
                        // Jika skor sangat tinggi (Viral), kirim notifikasi otomatis ke user
                        if (calculatedScore >= 90)
                        {
                            await _repo.AddNotificationAsync(userId, "Viral Alert!", $"Tren '{data.Keyword}' sedang meledak di niche Anda! Segera buat konten.");
                        }

                        // Jika ada sound yang trending
                        if (!string.IsNullOrEmpty(musicName) && calculatedScore > 80)
                        {
                            await _repo.AddNotificationAsync(userId, "Trending Sound", $"Sound '{musicName}' sedang banyak digunakan. Cek sekarang!");
                        }

                        trends.Add(data);
                    }
                    return trends;
                }
            }
            return new List<TrendData>();
        }

        public async Task FetchAndSaveCompetitorVideos(int userId)
        {
            var competitors = await _repo.GetUserCompetitorsAsync(userId);
            foreach (var comp in competitors)
            {
                var cleanHandle = comp.AccountHandle?.Replace("@", "").Trim();
                if (string.IsNullOrEmpty(cleanHandle)) continue;

                var videoUrls = await FetchUrlsFromApify(cleanHandle);

                foreach (var url in videoUrls)
                {
                    await _repo.SaveCompetitorPostAsync(comp.CompetitorId, url);
                }
            }
        }

        private async Task<List<string>> FetchUrlsFromApify(string username)
        {
            var baseUrl = _config["ScraperSettings:BaseUrl"] ?? "https://api.apify.com/v2/";
            var apiKey = _config["ScraperSettings:ApiKey"];
            var actorId = _config["ScraperSettings:TikTokActorId"] ?? "clockworks~tiktok-scraper";
            var url = $"{baseUrl}acts/{actorId}/run-sync-get-dataset-items?token={apiKey}";

            var input = new { profiles = new[] { username }, resultsPerPage = 3, memoryMbytes = 512 };
            var urls = new List<string>();

            try
            {
                var response = await _httpClient.PostAsJsonAsync(url, input);
                if (response.IsSuccessStatusCode)
                {
                    var rawData = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
                    if (rawData != null)
                    {
                        foreach (var item in rawData)
                        {
                            string? foundUrl = null;
                            if (item.TryGetProperty("webVideoUrl", out var p1)) foundUrl = p1.GetString();
                            else if (item.TryGetProperty("videoUrl", out var p2)) foundUrl = p2.GetString();

                            if (!string.IsNullOrEmpty(foundUrl)) urls.Add(foundUrl);
                        }
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine($"[SCRAPER ERROR] {ex.Message}"); }
            return urls;
        }
    }
}