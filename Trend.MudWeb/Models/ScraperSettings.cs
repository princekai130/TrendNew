namespace Trend.MudWeb.Models
{
    public class ScraperSettings
    {
        // Nama properti harus sama dengan yang ada di appsettings.json
        public string ApiKey { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = string.Empty;
    }
}
