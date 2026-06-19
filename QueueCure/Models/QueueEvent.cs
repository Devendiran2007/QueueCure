using System;

namespace QueueCure.Models
{
    public class QueueEvent
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        
        public Guid PatientId { get; set; }
        public Patient? Patient { get; set; }

        public string EventType { get; set; } = string.Empty; // e.g. "CheckIn", "Called", "Started", "Completed", "Skipped"
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
