using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Trend.MudWeb;
using Trend.MudWeb.Models;

namespace Trend.MudWebDbTestConsole
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // 1. Konfigurasi AppSettings
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            // 2. Setup Dependency Injection
            var serviceProvider = new ServiceCollection()
                .AddDbContext<TrendContext>(options =>
                    options.UseSqlite(configuration.GetConnectionString("DefaultConnection")))
                .AddScoped<TrendRepo>()
                .BuildServiceProvider();

            // 3. Menjalankan Test
            using (var scope = serviceProvider.CreateScope())
            {
                var repo = scope.ServiceProvider.GetRequiredService<TrendRepo>();
                Console.WriteLine("=== Memulai Test Repositori TRENDZ ===\n");

                await TestViralTrends(repo);
                await TestNicheTrends(repo);
                await TestContentRecommendations(repo);
            }

            Console.WriteLine("\n=== Test Selesai. Tekan tombol apa saja untuk keluar. ===");
            Console.ReadKey();
        }

        /// <summary>
        /// Menguji pengambilan tren viral (Fitur utama untuk real-time monitoring).
        /// </summary>
        static async Task TestViralTrends(TrendRepo repo)
        {
            Console.WriteLine("[Test 1] Mengambil Tren Viral...");
            var trends = await repo.GetViralTrendsAsync();

            if (trends.Count > 0)
            {
                foreach (var t in trends)
                {
                    Console.WriteLine($"- {t.TrendName} (Score: {t.GrowthScore}) pada platform {t.Platform}");
                }
            }
            else
            {
                Console.WriteLine("- Tidak ada tren viral ditemukan. Pastikan dummy data sudah masuk.");
            }
        }

        /// <summary>
        /// Menguji filter tren berdasarkan niche (Kebutuhan 74.1% responden).
        /// </summary>
        static async Task TestNicheTrends(TrendRepo repo)
        {
            Console.WriteLine("\n[Test 2] Mengambil Tren Niche ID: 1...");
            var nicheTrends = await repo.GetTrendsByNicheAsync(1);
            Console.WriteLine($"- Ditemukan {nicheTrends.Count} tren untuk niche ini.");
        }

        /// <summary>
        /// Menguji sistem rekomendasi konten (Fitur paling diinginkan responden).
        /// </summary>
        static async Task TestContentRecommendations(TrendRepo repo)
        {
            Console.WriteLine("\n[Test 3] Mengambil Rekomendasi (User Premium)...");
            var recommendations = await repo.GetLatestRecommendationsAsync(isPremiumUser: true);

            foreach (var rec in recommendations)
            {
                string label = (rec.IsPremiumOnly ?? false) ? "[PREMIUM]" : "[FREE]";
                Console.WriteLine($"{label} Hook: {rec.SuggestedHook}");
            }
        }
    }
}