using System;

namespace QueueCure.Models
{
    public class QueueImpact
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string EventType { get; set; } = string.Empty;
        public string EventDetail { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public Guid PatientId { get; set; }
        public Patient? Patient { get; set; }
        public string TokenNumber { get; set; } = string.Empty;
        public double WaitTimeBefore { get; set; }
        public double WaitTimeAfter { get; set; }
        public double ImpactMinutes { get; set; }
    }
}
