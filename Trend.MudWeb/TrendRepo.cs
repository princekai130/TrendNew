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
                // Pastikan kita menangani null pada GrowthScore sebelum sorting
                .OrderByDescending(t => t.GrowthScore ?? 0)
                .Take(3)
                .AsNoTracking() // Tambahkan ini untuk performa dashboard yang lebih cepat
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

        public async Task<List<UserNotification>> GetUserNotificationsAsync(int userId)
        {
            // Panggil _context.Notifications (bukan _context.UserNotification)
            return await _context.Notifications
                .Where(n => n.UserId == userId)
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
            var notification = new UserNotification // Ini nama CLASS-nya
            {
                UserId = userId,
                Message = $"[{title}] {message}",
                CreatedAt = DateTime.Now,
                IsRead = false
            };

            // PERBAIKAN: Gunakan .Notifications (ini nama DbSet di TrendContext)
            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
        }
        public async Task MarkNotificationAsReadAsync(int notificationId)
        {
            // PERBAIKAN: Gunakan .Notifications (nama DbSet), bukan .UserNotification
            var notif = await _context.Notifications.FindAsync(notificationId);
            if (notif != null)
            {
                notif.IsRead = true;
                await _context.SaveChangesAsync();
            }
        }

        #endregion

        #region Simpan & Ambil Daftar Trend

        public async Task<List<SocialTrend>> GetTrendsAsync()
        {
            return await _context.SocialTrends.ToListAsync();
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
            var query = _context.SocialTrends
                .Include(t => t.Niche) // Penting agar NicheName tidak null
                .AsQueryable();

            if (nicheId != 0)
            {
                query = query.Where(t => t.NicheId == nicheId);
            }

            return await query
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

        // Ambil semua user beserta data Niche-nya untuk ditampilkan di tabel Admin
        public async Task<List<User>> GetAllUsersAsync()
        {
            return await _context.Users
                .Include(u => u.Niche)
                .OrderByDescending(u => u.UserId)
                .ToListAsync();
        }

        // Simpan perubahan data user (seperti perubahan Role ke Premium)
        public async Task UpdateUserAsync(User user)
        {
            var existingUser = await _context.Users.FindAsync(user.UserId);
            if (existingUser != null)
            {
                existingUser.Role = user.Role;
                existingUser.Email = user.Email;
                // Tambahkan field lain jika diperlukan

                _context.Users.Update(existingUser);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<List<SocialTrend>> SearchTrendsAsync(string searchTerm)
        {
            var query = _context.SocialTrends
                .Include(t => t.Niche)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                string search = searchTerm.ToLower();

                // Mencari di nama tren atau nama sound (jika ada)
                query = query.Where(t =>
                    (t.TrendName != null && t.TrendName.ToLower().Contains(search)) ||
                    (t.SoundName != null && t.SoundName.ToLower().Contains(search)) ||
                    (t.Platform != null && t.Platform.ToLower().Contains(search)));
            }

            return await query
                .OrderByDescending(t => t.DiscoveredAt)
                .ToListAsync();
        }

        public async Task SaveTrendRangeAsync(List<TrendData> trends, int nicheId)
        {
            foreach (var item in trends)
            {
                var exists = await _context.SocialTrends
                    .AnyAsync(t => t.TrendName.ToLower() == item.Keyword.ToLower()
                                    && t.Platform == item.Platform
                                    && t.NicheId == nicheId);

                if (!exists)
                {
                    var newTrend = new SocialTrend
                    {
                        TrendName = item.Keyword,
                        Platform = item.Platform,
                        // Gunakan GrowthScore jika sudah ada, jika tidak gunakan EngagementScore
                        GrowthScore = item.GrowthScore > 0 ? item.GrowthScore : item.EngagementScore,
                        DiscoveredAt = DateTime.Now,
                        IsViral = (item.GrowthScore > 80 || item.EngagementScore > 80),
                        NicheId = nicheId,
                        TrendType = "Hashtag",
                        SoundName = item.SoundName,
                        SoundUrl = item.SoundUrl
                    };
                    _context.SocialTrends.Add(newTrend);
                }
            }
            await _context.SaveChangesAsync();
        }

        #endregion

        #region Cleanup Old Data
        public async Task CleanupOldPostsAsync(int days)
        {
            var cutOffDate = DateTime.Now.AddDays(-days);
            var oldPosts = _context.CompetitorPosts.Where(p => p.PostedAt < cutOffDate);

            _context.CompetitorPosts.RemoveRange(oldPosts);
            await _context.SaveChangesAsync();
        }
        #endregion
    }
}