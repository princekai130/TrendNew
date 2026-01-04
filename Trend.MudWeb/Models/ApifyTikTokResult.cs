namespace Trend.MudWeb.Models
{
    public class ApifyTikTokResult
    {
        public string? text { get; set; } // Tambahkan ? agar boleh null
        public TikTokStats? stats { get; set; }
        public List<TikTokHashtag>? hashtags { get; set; } = new();
    }

    public class TikTokStats
    {
        public int diggCount { get; set; } // Likes di TikTok Scraper
        public int playCount { get; set; }
    }

    public class TikTokHashtag
    {
        public string name { get; set; }
    }
}