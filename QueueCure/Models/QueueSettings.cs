using System;

namespace QueueCure.Models
{
    public class QueueSettings
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public int AverageConsultationTime { get; set; } = 10; // in minutes
        public string LastTokenNumber { get; set; } = string.Empty;
    }
}
