using System;

namespace QueueCure.Models
{
    public class DelayEvent
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid DoctorId { get; set; }
        public Doctor? Doctor { get; set; }
        public Guid PatientId { get; set; }
        public Patient? Patient { get; set; }
        public double ExpectedDuration { get; set; }
        public double ActualDuration { get; set; }
        public double DelayMinutes { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public bool IsResolved { get; set; } = false;
    }
}
