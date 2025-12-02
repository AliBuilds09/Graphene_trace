using System;

namespace MyProject.Models
{
    public class Comment
    {
        public string Author { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string? Reply { get; set; }
    }
}