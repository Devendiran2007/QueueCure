using System;

namespace QueueCure.Models
{
    public class Patient
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string TokenNumber { get; set; } = string.Empty;
        public DateTime CheckInTime { get; set; } = DateTime.UtcNow;
        public PatientStatus Status { get; set; } = PatientStatus.Waiting;
        public VisitCategory Category { get; set; } = VisitCategory.GeneralCheckup;

        // Foreign key to Doctor
        public Guid DoctorId { get; set; }
        public Doctor? Doctor { get; set; }
    }
}
