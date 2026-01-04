namespace Trend.MudWeb.Services
{
    public class Notifications
    {
        // Atribut sesuai laporan
        public int notificationId { get; set; }
        public string message { get; set; }
        public DateTime sentAt { get; set; }
        public string status { get; set; }

        // Method sesuai laporan
        public void SendNotification(string userEmail, string msg)
        {
            this.message = msg;
            this.sentAt = DateTime.Now;
            this.status = "Sent";
            // Logika pengiriman (Email/WA/Push)
        }
    }
}
