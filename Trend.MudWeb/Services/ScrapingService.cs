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

        public async Task<List<TrendData>> FetchTrendData()
        {
            var baseUrl = _config["ScraperSettings:BaseUrl"] ?? "https://api.apify.com/v2/";
            var apiKey = _config["ScraperSettings:ApiKey"];
            var actorId = _config["ScraperSettings:TikTokActorId"] ?? "clockworks~tiktok-scraper";

            if (!baseUrl.EndsWith("/")) baseUrl += "/";

            var hashtagFromDb = await _repo.GetSettingAsync("TargetHashtag") ?? "skincare";
            var url = $"{baseUrl}acts/{actorId}/runs?token={apiKey}&waitForFinish=120";

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
                if (root.TryGetProperty("data", out var dataElement) && dataElement.TryGetProperty("defaultDatasetId", out var idElement))
                {
                    datasetId = idElement.GetString();
                }

                if (string.IsNullOrEmpty(datasetId)) throw new Exception("Dataset ID tidak ditemukan.");

                var datasetUrl = $"{baseUrl}datasets/{datasetId}/items?token={apiKey}";
                var rawData = await _httpClient.GetFromJsonAsync<List<ApifyTikTokResult>>(datasetUrl);

                return rawData?.Select(item => new TrendData
                {
                    Keyword = item.text?.Length > 100 ? item.text.Substring(0, 97) + "..." : (item.text ?? "TikTok Trend"),
                    Platform = "TikTok",
                    EngagementScore = Math.Min(100, (double)(item.stats?.diggCount ?? 0) / 1000),
                    CreatedAt = DateTime.Now
                }).ToList() ?? new List<TrendData>();
            }
            throw new Exception("Gagal menjalankan Scraper.");
        }

        // --- PRIVATE METHOD UNTUK AMBIL URL DARI APIFY ---
        private async Task<List<string>> FetchUrlsFromApify(string username)
        {
            var baseUrl = _config["ScraperSettings:BaseUrl"] ?? "https://api.apify.com/v2/";
            var apiKey = _config["ScraperSettings:ApiKey"];
            var actorId = _config["ScraperSettings:TikTokActorId"] ?? "clockworks~tiktok-scraper";

            // Kita gunakan endpoint sync agar aplikasi menunggu hasil sebelum lanjut
            var url = $"{baseUrl}acts/{actorId}/run-sync-get-dataset-items?token={apiKey}";

            Console.WriteLine($"[SCRAPER] Menghubungi Apify untuk akun: @{username}...");

            var input = new
            {
                profiles = new[] { username },
                resultsPerPage = 3, // Mengambil 3 video terbaru
                memoryMbytes = 512
            };

            var urls = new List<string>();

            try
            {
                var response = await _httpClient.PostAsJsonAsync(url, input);

                if (response.IsSuccessStatusCode)
                {
                    var rawData = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
                    Console.WriteLine($"[SCRAPER] Berhasil! Mendapatkan {rawData?.Count ?? 0} data dari Apify untuk @{username}");

                    if (rawData != null)
                    {
                        foreach (var item in rawData)
                        {
                            string? foundUrl = null;
                            // Mencoba mencari URL video di berbagai kemungkinan nama properti JSON Apify
                            if (item.TryGetProperty("webVideoUrl", out var p1)) foundUrl = p1.GetString();
                            else if (item.TryGetProperty("videoUrl", out var p2)) foundUrl = p2.GetString();
                            else if (item.TryGetProperty("url", out var p3)) foundUrl = p3.GetString();

                            if (!string.IsNullOrEmpty(foundUrl))
                            {
                                urls.Add(foundUrl);
                                Console.WriteLine($"[SCRAPER] Menemukan video: {foundUrl}");
                            }
                        }
                    }
                }
                else
                {
                    // Jika status bukan 200 OK (misal 401 Unauthorized atau 404)
                    var errorDetail = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[SCRAPER] Gagal memanggil Apify. Status: {response.StatusCode}, Detail: {errorDetail}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SCRAPER] EXCEPTION saat memanggil Apify: {ex.Message}");
            }

            return urls;
        }

        // --- PUBLIC METHOD YANG DIPANGGIL DASHBOARD ---
        public async Task FetchAndSaveCompetitorVideos(int userId)
        {
            var competitors = await _repo.GetUserCompetitorsAsync(userId);
            Console.WriteLine($"[SCRAPER] Menjalankan update video untuk {competitors.Count} kompetitor user ID {userId}");

            foreach (var comp in competitors)
            {
                var cleanHandle = comp.AccountHandle?.Replace("@", "").Trim();
                if (string.IsNullOrEmpty(cleanHandle)) continue;

                var videoUrls = await FetchUrlsFromApify(cleanHandle);

                foreach (var url in videoUrls)
                {
                    // Tambahkan log ini:
                    Console.WriteLine($"[DB] Menyimpan video untuk {comp.AccountHandle} ke database...");
                    await _repo.SaveCompetitorPostAsync(comp.CompetitorId, url);
                }
            }
        }

        public async Task<List<TrendData>> FetchTrendDataByNiche(int nicheId)
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
                    // Menggunakan JsonElement agar lebih fleksibel mencari properti
                    var rawData = await _httpClient.GetFromJsonAsync<List<JsonElement>>(datasetUrl);

                    return rawData?.Select(item => {
                        // MENCARI JUMLAH LIKES (DIGG COUNT) DI BERBAGAI KEMUNGKINAN POSISI
                        long diggCount = 0;
                        if (item.TryGetProperty("diggCount", out var d1)) diggCount = d1.GetInt64();
                        else if (item.TryGetProperty("stats", out var s) && s.TryGetProperty("diggCount", out var d2)) diggCount = d2.GetInt64();
                        else if (item.TryGetProperty("statistics", out var st) && st.TryGetProperty("diggCount", out var d3)) diggCount = d3.GetInt64();

                        // RUMUS SKOR PERTUMBUHAN (Growth Score)
                        // Kita buat skala 0-100. Jika likes > 50.000, skor otomatis 100.
                        double calculatedScore = Math.Min(100.0, (double)diggCount / 500.0);

                        // Jika hasil sangat kecil (misal 0.001), kita bulatkan ke atas minimal jadi 1 
                        // agar tidak terlihat sebagai 0 di UI The Hot List
                        if (diggCount > 0 && calculatedScore < 1) calculatedScore = 5.0;

                        return new TrendData
                        {
                            Keyword = item.TryGetProperty("text", out var t) ? (t.GetString()?.Length > 100 ? t.GetString().Substring(0, 97) + "..." : t.GetString()) : "TikTok Trend",
                            Platform = "TikTok",
                            EngagementScore = calculatedScore,
                            CreatedAt = DateTime.Now
                        };
                    }).ToList() ?? new List<TrendData>();
                }
            }
            return new List<TrendData>();
        }
    }

    // Helper class untuk mapping JSON
    public class ApifyTikTokResult
    {
        public string? text { get; set; }
        public ApifyStats? stats { get; set; }
    }
    public class ApifyStats
    {
        public int diggCount { get; set; }
    }
}