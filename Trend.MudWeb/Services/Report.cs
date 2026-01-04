namespace Trend.MudWeb.Services
{
    public class Report
    {
        public int reportId { get; set; }
        public DateTime generatedAt { get; set; }
        public string reportContent { get; set; }

        public void GenerateReport(string analysis)
        {
            this.reportId = new Random().Next(10000, 99999);
            this.generatedAt = DateTime.Now;
            this.reportContent = analysis;
        }

        // Method untuk simulasi ekspor PDF sesuai Class Diagram
        public byte[] ExportReport()
        {
            return System.Text.Encoding.UTF8.GetBytes(this.reportContent);
        }
    }
}