using System;

namespace MyProject.Models
{
    public class Alert
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string Message { get; set; } = string.Empty;
        public int PeakPressure { get; set; }
        public int Threshold { get; set; }
        public string Source { get; set; } = "Dashboard";
    }
}