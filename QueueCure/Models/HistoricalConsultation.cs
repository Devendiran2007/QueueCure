using System;

namespace QueueCure.Models
{
    public class HistoricalConsultation
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid PatientId { get; set; }
        public Guid DoctorId { get; set; }
        public VisitCategory PatientCategory { get; set; }
        public int QueuePositionWhenAdded { get; set; }
        public DateTime CheckInTime { get; set; }
        public DateTime ConsultationStartTime { get; set; }
        public DateTime ConsultationEndTime { get; set; }
        public double ActualWaitTime { get; set; } // in minutes
        public double ConsultationDuration { get; set; } // in minutes
        public DayOfWeek DayOfWeek { get; set; }
        public int HourOfDay { get; set; } // 0 to 23

        // Navigation properties
        public Doctor? Doctor { get; set; }
    }
}
