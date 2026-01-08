using Microsoft.EntityFrameworkCore;
using Trend.MudWeb.Models;

namespace Trend.MudWeb
{
    /// <summary>
    /// Repositori untuk mengelola operasi basis data aplikasi TRENDZ.
    /// Menangani logika pengambilan tren, rekomendasi konten, dan analisis kompetitor[cite: 2, 40].
                /// </summary>
    public class TrendRepo
    {
        private readonly TrendContext _context;

        /// <summary>
        /// Inisialisasi instance baru dari <see cref="TrendRepo"/>.
        /// </summary>
        /// <param name="context">Context database untuk aplikasi Trend[cite: 40].</param>
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

        /// <summary>
        /// Mengambil tren berdasarkan kategori niche tertentu.
        /// Fitur ini mendukung kebutuhan 74,1% responden yang ingin mengetahui tren di niche mereka
                    /// </summary>
        /// <param name="nicheId">ID unik untuk kategori niche</param>
        /// <returns>Daftar tren yang difilter berdasarkan niche.</returns>
        public async Task<List<SocialTrend>> GetTrendsByNicheAsync(int nicheId)
        {
            return await _context.SocialTrends
                .Where(t => t.NicheId == nicheId)
                .OrderByDescending(t => t.DiscoveredAt)
                .ToListAsync();
        }

        #endregion

        #region Content Recommendations

        /// <summary>
        /// Memberikan rekomendasi ide konten otomatis berdasarkan tren terbaru
        /// Mendukung model monetisasi dengan memisahkan akses Free dan Premium
        /// </summary>
        /// <param name="isPremiumUser">Status langganan pengguna</param>
        /// <returns>Daftar rekomendasi ide konten.</returns>
        public async Task<List<ContentRecommendation>> GetLatestRecommendationsAsync(bool isPremiumUser)
        {
            var query = _context.ContentRecommendations
                .Include(r => r.Trend)
                .AsQueryable();

            // Memvalidasi hak akses konten berdasarkan status langganan.
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

        /// <summary>
        /// Mengambil performa konten kompetitor untuk membantu talent tetap unggul.
        /// Sangat diminati oleh 85,2% responden survei.
        /// </summary>
        /// <param name="userId">ID pengguna yang melakukan pelacakan.</param>
        /// <returns>Daftar postingan kompetitor dengan engagement terbaik</returns>
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

        /// <summary>
        /// Mengambil notifikasi tren real-time yang belum dibaca.
        /// Menjawab kebutuhan 92,6% responden akan notifikasi tren relevan.
        /// </summary>
        /// <param name="userId">ID pengguna target</param>
        /// <returns>Daftar notifikasi terbaru</returns>
        public async Task<List<Notifications>> GetUserNotificationsAsync(int userId)
        {
            return await _context.Notifications
                .Where(n => n.UserId == userId && n.IsRead == false)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();
        }


        /// <summary>
        /// Mengambil data user untuk mengecek status langganan secara real-time.
        /// </summary>
        public async Task<User> GetUserByEmailAsync(string email)
        {
            return await _context.Users
                .Include(u => u.Niche)
              .FirstOrDefaultAsync(u => u.Email == email);
        }

        /// <summary>
        /// Menambahkan user baru ke database.
        /// </summary>
        public async Task AddUserAsync(User user)
        {
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Mengambil semua daftar niche untuk pilihan saat registrasi.
        /// </summary>
        public async Task<List<Nich>> GetAllNichesAsync()
        {
            return await _context.Niches.OrderBy(n => n.NicheName).ToListAsync();
        }

        #endregion

        #region Simpan & Ambil Daftar Trend
        public async Task<List<SocialTrend>> GetTrendsAsync()
        {
            return await _context.SocialTrends.ToListAsync();
        }

        public async Task SaveTrendRangeAsync(List<TrendData> trends)
        {
            try
            {
                foreach (var item in trends)
                {
                    // Gunakan ToLower() untuk perbandingan yang lebih aman
                    var exists = await _context.SocialTrends
                        .AnyAsync(t => t.TrendName.ToLower() == item.Keyword.ToLower()
                                  && t.Platform == item.Platform);
                    if (!exists)
                    {
                        var newTrend = new SocialTrend
                        {
                            TrendName = item.Keyword.Length > 200 ? item.Keyword.Substring(0, 197) + "..." : item.Keyword,
                            Platform = item.Platform,
                            GrowthScore = Convert.ToDouble(item.EngagementScore),
                            DiscoveredAt = DateTime.Now,
                            IsViral = item.EngagementScore > 80,

                            // UBAH BARIS INI: Harus sesuai dengan CHECK constraint database
                            TrendType = "Hashtag"
                        };
                        _context.SocialTrends.Add(newTrend);
                    }
                }
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException dbEx)
            {
                // Ini akan menangkap detail error database yang sebenarnya
                var innerMessage = dbEx.InnerException?.Message ?? dbEx.Message;
                throw new Exception($"Database Error: {innerMessage}");
            }
        }

        // Tambahkan juga metode pencarian untuk Trend Explorer
        public async Task<List<SocialTrend>> SearchTrendsAsync(string searchTerm)
        {
            // Menggunakan queryable agar eksekusi terjadi di database
            var query = _context.SocialTrends.AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                string search = searchTerm.ToLower();
                query = query.Where(t =>
                    (t.TrendName != null && t.TrendName.ToLower().Contains(search)) ||
                    (t.Platform != null && t.Platform.ToLower().Contains(search)));
            }

            return await query.OrderByDescending(t => t.DiscoveredAt).ToListAsync();
        }

        public async Task AddNotificationAsync(int userId, string title, string message)
        {
            var notification = new Notifications
            {
                UserId = userId,
                // Kita gabungkan Title ke dalam Message karena kolom Title tidak ada di DB
                Message = $"[{title}] {message}",
                CreatedAt = DateTime.Now,
                IsRead = false
            };
            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
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

        // Mengambil daftar kompetitor yang dipantau oleh user
        public async Task<List<Competitor>> GetUserCompetitorsAsync(int userId)
        {
            return await _context.Competitors
                .Where(c => c.UserId == userId)
                .ToListAsync();
        }

        // Mengambil postingan dari kompetitor tertentu dengan engagement tertinggi
        public async Task<List<CompetitorPost>> GetCompetitorPostsAsync(int userId)
        {
            return await _context.CompetitorPosts
                .Include(p => p.Competitor) // Penting agar data Platform & AccountHandle terbaca
                .Where(p => p.Competitor.UserId == userId)
                .OrderByDescending(p => p.EngagementRate)
                .ToListAsync();
        }

        // Method untuk menambah kompetitor baru (untuk didemokan)
        public async Task AddCompetitorAsync(Competitor comp)
        {
            _context.Competitors.Add(comp);
            await _context.SaveChangesAsync();
        }

        #endregion

        #region The Hot List

        public async Task<List<SocialTrend>> GetHotListTrendsAsync()
        {
            // Mengambil tren yang IsViral true ATAU GrowthScore di atas 80
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

        /// <summary>
        /// Mengambil nilai konfigurasi sistem berdasarkan key.
        /// </summary>
        public async Task<string?> GetSettingAsync(string key)
        {
            // Mencari di tabel SystemSettings berdasarkan Primary Key (SettingKey)
            var setting = await _context.SystemSettings
                .FirstOrDefaultAsync(s => s.SettingKey == key);

            return setting?.SettingValue;
        }

        /// <summary>
        /// Memperbarui atau menambah konfigurasi sistem baru.
        /// </summary>
        public async Task UpdateSettingAsync(string key, string value)
        {
            var existingSetting = await _context.SystemSettings
                .FirstOrDefaultAsync(s => s.SettingKey == key);

            if (existingSetting != null)
            {
                // Jika sudah ada, update nilainya
                existingSetting.SettingValue = value;
            }
            else
            {
                // Jika belum ada, buat baru
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

    }
}