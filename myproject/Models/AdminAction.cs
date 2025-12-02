using System;

namespace MyProject.Models
{
    public class AdminAction
    {
        public Guid ActionId { get; set; }
        public Guid? AdminId { get; set; }
        public Guid? TargetUserId { get; set; }
        public string ActionType { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}