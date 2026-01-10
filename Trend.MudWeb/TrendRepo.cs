using Microsoft.EntityFrameworkCore;
using Trend.MudWeb.Models;

namespace Trend.MudWeb
{
    /// <summary>
    /// Repositori untuk mengelola operasi basis data aplikasi TRENDZ.
    /// Menangani logika pengambilan tren, rekomendasi konten, dan analisis kompetitor.
    /// </summary>
    public class TrendRepo
    {
        private readonly TrendContext _context;

        /// <summary>
        /// Inisialisasi instance baru dari <see cref="TrendRepo"/>.
        /// </summary>
        /// <param name="context">Context database untuk aplikasi Trend.</param>
        public TrendRepo(TrendContext context)
        {
            _context = context;
        }

        #region Dashboard & Trends

        /// <summary>
        /// Mengambil tren yang sedang viral (IsViral = true) untuk ditampilkan di widget Home
        /// </summary>
        /// <returns>Daftar 3 tren dengan skor pertumbuhan tertinggi</returns>
        public async Task<List<SocialTrend>> GetViralTrendsAsync()
        {
            return await _context.SocialTrends
                .Where(t => t.IsViral == true)
                .OrderByDescending(t => t.GrowthScore)
                .Take(3)
                .ToListAsync();
        }

        #endregion

        #region Content Recommendations

        /// <summary>
        /// Memberikan rekomendasi ide konten otomatis berdasarkan tren terbaru
        /// </summary>
        public async Task<List<ContentRecommendation>> GetLatestRecommendationsAsync(bool isPremiumUser)
        {
            var query = _context.ContentRecommendations
                .Include(r => r.Trend)
                .AsQueryable();

            if (!isPremiumUser)
            {
                query = query.Where(r => r.IsPremiumOnly == false);
            }

            return await query
                .OrderByDescending(r => r.RecommendationId)
                .ToListAsync();
        }

        #endregion

        #region Competitor Tracking

        public async Task<List<CompetitorPost>> GetTopCompetitorPostsAsync(int userId)
        {
            return await _context.CompetitorPosts
                .Include(p => p.Competitor)
                .Where(p => p.Competitor.UserId == userId)
                .OrderByDescending(p => p.EngagementRate)
                .Take(10)
                .ToListAsync();
        }

        #endregion

        #region Notifications & Users

        public async Task<List<Notifications>> GetUserNotificationsAsync(int userId)
        {
            return await _context.Notifications
                .Where(n => n.UserId == userId && n.IsRead == false)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();
        }

        public async Task<User> GetUserByEmailAsync(string email)
        {
            return await _context.Users
                .Include(u => u.Niche)
                .FirstOrDefaultAsync(u => u.Email == email);
        }

        public async Task AddUserAsync(User user)
        {
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
        }

        public async Task<List<Nich>> GetAllNichesAsync()
        {
            return await _context.Niches.OrderBy(n => n.NicheName).ToListAsync();
        }

        // --- TAMBAHKAN INI: Metode yang hilang untuk UpgradePremium.razor ---
        public async Task AddNotificationAsync(int userId, string title, string message)
        {
            var notification = new Notifications
            {
                UserId = userId,
                Message = $"[{title}] {message}",
                CreatedAt = DateTime.Now,
                IsRead = false
            };
            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
        }

        #endregion

        #region Simpan & Ambil Daftar Trend

        public async Task<List<SocialTrend>> GetTrendsAsync()
        {
            return await _context.SocialTrends.ToListAsync();
        }

        public async Task SaveTrendRangeAsync(List<TrendData> trends, int nicheId)
        {
            try
            {
                Console.WriteLine($"[DEBUG] Memproses {trends.Count} data untuk Niche ID: {nicheId}"); // LOG 1

                foreach (var item in trends)
                {
                    var exists = await _context.SocialTrends
                        .AnyAsync(t => t.TrendName.ToLower() == item.Keyword.ToLower()
                                        && t.Platform == item.Platform
                                        && t.NicheId == nicheId);

                    if (!exists)
                    {
                        Console.WriteLine($"[DEBUG] Menyimpan Trend Baru: {item.Keyword} dengan Skor: {item.EngagementScore}"); // LOG 2
                        var newTrend = new SocialTrend
                        {
                            TrendName = item.Keyword.Length > 200 ? item.Keyword.Substring(0, 197) + "..." : item.Keyword,
                            Platform = item.Platform,
                            GrowthScore = item.EngagementScore,
                            DiscoveredAt = DateTime.Now,
                            IsViral = item.EngagementScore > 80,
                            NicheId = nicheId,
                            TrendType = "Hashtag"
                        };
                        _context.SocialTrends.Add(newTrend);
                    }
                }
                await _context.SaveChangesAsync();
                Console.WriteLine("[DEBUG] Simpan ke Database SELESAI."); // LOG 3
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] ERROR DATABASE: {ex.Message}");
                throw;
            }
        }

        // --- TAMBAHKAN INI: Metode yang hilang untuk TrendExplorer.razor ---
        public async Task<List<SocialTrend>> SearchTrendsAsync(string searchTerm)
        {
            var query = _context.SocialTrends
                .Include(t => t.Niche)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                string search = searchTerm.ToLower();
                query = query.Where(t =>
                    (t.TrendName != null && t.TrendName.ToLower().Contains(search)) ||
                    (t.Platform != null && t.Platform.ToLower().Contains(search)));
            }

            return await query.OrderByDescending(t => t.DiscoveredAt).ToListAsync();
        }

        #endregion

        #region Upgrade User Subscription

        public async Task UpdateUserSubscriptionAsync(string email, string newStatus)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user != null)
            {
                user.SubscriptionStatus = newStatus;
                await _context.SaveChangesAsync();
            }
        }

        #endregion

        #region Competitor Watchlist Logic

        public async Task<List<Competitor>> GetUserCompetitorsAsync(int userId)
        {
            return await _context.Competitors
                .Where(c => c.UserId == userId)
                .ToListAsync();
        }

        public async Task<List<CompetitorPost>> GetCompetitorPostsAsync(int userId)
        {
            return await _context.CompetitorPosts
                .Include(p => p.Competitor)
                .Where(p => p.Competitor.UserId == userId)
                .OrderByDescending(p => p.EngagementRate)
                .ToListAsync();
        }

        public async Task AddCompetitorAsync(Competitor comp)
        {
            _context.Competitors.Add(comp);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> DeleteCompetitorAsync(int competitorId)
        {
            var comp = await _context.Competitors.FindAsync(competitorId);
            if (comp == null) return false;

            // Hapus dulu semua postingan terkait kompetitor ini
            var posts = _context.CompetitorPosts.Where(p => p.CompetitorId == competitorId);
            _context.CompetitorPosts.RemoveRange(posts);

            // Hapus kompetitornya
            _context.Competitors.Remove(comp);

            await _context.SaveChangesAsync();
            return true;
        }

        #endregion

        #region The Hot List

        public async Task<List<SocialTrend>> GetHotListTrendsAsync()
        {
            return await _context.SocialTrends
                .Where(t => t.IsViral == true || t.GrowthScore > 80)
                .OrderByDescending(t => t.GrowthScore)
                .ToListAsync();
        }

        #endregion

        #region Market Report

        public async Task<Dictionary<string, int>> GetMarketReportStatsAsync()
        {
            var totalTrends = await _context.SocialTrends.CountAsync();
            var viralTrends = await _context.SocialTrends.CountAsync(t => t.IsViral == true);
            var tiktokCount = await _context.SocialTrends.CountAsync(t => t.Platform == "TikTok");
            var igCount = await _context.SocialTrends.CountAsync(t => t.Platform == "Instagram");

            return new Dictionary<string, int>
            {
                { "Total", totalTrends },
                { "Viral", viralTrends },
                { "TikTok", tiktokCount },
                { "Instagram", igCount }
            };
        }

        #endregion

        #region System Settings

        public async Task<string?> GetSettingAsync(string key)
        {
            var setting = await _context.SystemSettings
                .FirstOrDefaultAsync(s => s.SettingKey == key);

            return setting?.SettingValue;
        }

        public async Task UpdateSettingAsync(string key, string value)
        {
            var existingSetting = await _context.SystemSettings
                .FirstOrDefaultAsync(s => s.SettingKey == key);

            if (existingSetting != null)
            {
                existingSetting.SettingValue = value;
            }
            else
            {
                var newSetting = new SystemSetting
                {
                    SettingKey = key,
                    SettingValue = value
                };
                _context.SystemSettings.Add(newSetting);
            }

            await _context.SaveChangesAsync();
        }

        #endregion

        #region Simpan Link Video Kompetitor

        public async Task SaveCompetitorPostAsync(int competitorId, string videoUrl)
        {
            var exists = await _context.CompetitorPosts
                .AnyAsync(p => p.PostUrl == videoUrl);

            if (!exists)
            {
                var newPost = new CompetitorPost
                {
                    CompetitorId = competitorId,
                    PostUrl = videoUrl,
                    EngagementRate = new Random().NextDouble() * 10,
                    PostedAt = DateTime.Now
                };
                _context.CompetitorPosts.Add(newPost);
                await _context.SaveChangesAsync();
            }
        }

        #endregion

        #region Niche & Trend Management

        public async Task<List<SocialTrend>> GetTrendsByNicheAsync(int nicheId)
        {
            return await _context.SocialTrends
                .Where(t => t.NicheId == nicheId)
                .OrderByDescending(t => t.DiscoveredAt)
                .ToListAsync();
        }

        public async Task<List<Nich>> GetNichesAsync()
        {
            return await _context.Niches.OrderBy(n => n.NicheName).ToListAsync();
        }

        public async Task<User?> GetUserByIdAsync(int userId)
        {
            return await _context.Users
                .Include(u => u.Niche)
                .FirstOrDefaultAsync(u => u.UserId == userId);
        }

        public async Task<Dictionary<string, int>> GetMarketReportStatsByNicheAsync(int nicheId)
        {
            var stats = new Dictionary<string, int>();

            // 1. Hitung jumlah tren TikTok di niche ini
            stats["TikTok"] = await _context.SocialTrends
                .CountAsync(t => t.NicheId == nicheId && t.Platform == "TikTok");

            // 2. Hitung jumlah tren Instagram di niche ini
            stats["Instagram"] = await _context.SocialTrends
                .CountAsync(t => t.NicheId == nicheId && t.Platform == "Instagram");

            // 3. Perbaikan Error: Hitung kompetitor yang dimiliki oleh user-user di niche ini
            // Kita filter User dulu yang nichenya sesuai, baru hitung kompetitor mereka
            stats["Competitors"] = await _context.Competitors
                .CountAsync(c => c.User.NicheId == nicheId);

            // 4. Tambahkan total konten yang sudah terkumpul di niche ini
            stats["Total Posts"] = await _context.CompetitorPosts
                .CountAsync(p => p.Competitor.User.NicheId == nicheId);

            return stats;
        }

        #endregion
    }
}