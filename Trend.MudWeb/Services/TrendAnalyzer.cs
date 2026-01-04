using Trend.MudWeb.Models;

namespace Trend.MudWeb.Services
{
    public class TrendAnalyzer
    {
        // Atribut sesuai laporan Gambar 5.2
        public string analysisModel { get; set; } = "Trendz-ML-v1";
        public string analysisResult { get; set; }

        // Method sesuai laporan
        public string AnalyzeTrend(TrendData data)
        {
            // Proteksi jika data null
            if (data == null) return "Data tidak tersedia untuk dianalisis.";

            // Logika analisis tren berdasarkan GrowthScore (Simulasi ML)
            if (data.GrowthScore > 80)
                analysisResult = $"Analisis {analysisModel}: Keyword '{data.Keyword}' memiliki Potensi Viral Tinggi di {data.Platform}!";
            else
                analysisResult = $"Analisis {analysisModel}: Keyword '{data.Keyword}' menunjukkan pertumbuhan stabil.";

            return analysisResult;
        }

        public string AnalyzeCompetitor(CompetitorData data)
        {
            // Proteksi jika data null
            if (data == null) return "Data kompetitor tidak ditemukan.";

            // Logika analisis kompetitor sesuai Activity Diagram
            analysisResult = $"Rekomendasi Strategi untuk @{data.Username}: Fokus pada replikasi hook konten yang sedang ramai di niche {data.Platform}.";

            return analysisResult;
        }
    }
}
