using System.ComponentModel.DataAnnotations;

namespace Trend.MudWeb.Models
{
    public class SystemSetting
    {
        [Key]
        public string SettingKey { get; set; } = string.Empty;
        public string SettingValue { get; set; } = string.Empty;
    }
}