using System;

namespace QueueCure.Models
{
    public class PredictionInput
    {
        public Guid DoctorId { get; set; }
        public VisitCategory Category { get; set; }
        public int QueueLength { get; set; }
        public int TokensAhead { get; set; }
        public int HourOfDay { get; set; }
        public DayOfWeek DayOfWeek { get; set; }
    }
}
