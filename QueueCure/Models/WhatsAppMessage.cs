using System;

namespace QueueCure.Models
{
    public class WhatsAppMessage
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string PhoneNumber { get; set; } = string.Empty;
        public string MessageText { get; set; } = string.Empty;
        public DateTime SentAt { get; set; } = DateTime.UtcNow;
        public string Status { get; set; } = "Sent"; // Sent, Delivered, Read
    }
}
