using System;

namespace MyProject.Models
{
    public class SensorData
    {
        public string UserId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.MinValue;
        public int[,] Matrix { get; set; } = new int[32, 32];
    }
}