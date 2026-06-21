using System;

namespace QueueCure.Models
{
    public class DoctorLearningProfileDto
    {
        public Guid DoctorId { get; set; }
        public string DoctorName { get; set; } = string.Empty;
        public string Specialty { get; set; } = string.Empty;
        public double AverageConsultationDuration { get; set; }
        public double FastestConsultation { get; set; }
        public double SlowestConsultation { get; set; }
        public int PatientsSeen { get; set; }
        public double HistoricalAccuracy { get; set; } // in %
    }
}
