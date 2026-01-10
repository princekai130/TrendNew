namespace Trend.MudWeb.Models
{
    public class TrendData
    {
        public string Keyword { get; set; } = "";
        public string Platform { get; set; } = "";

        // Gunakan nama ini agar sinkron dengan Dashboard & Analyzer
        public double GrowthScore { get; set; }

        // Tetap simpan ini jika ada bagian lain yang terlanjur pakai EngagementScore
        public double EngagementScore { get => GrowthScore; set => GrowthScore = value; }

        public DateTime CreatedAt { get; set; }
        public string? SoundName { get; set; }
        public string? SoundUrl { get; set; }
    }
}