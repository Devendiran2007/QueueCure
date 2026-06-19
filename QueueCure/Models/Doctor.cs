using System;

namespace QueueCure.Models
{
    public class Doctor
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        
        public string Specialty { get; set; } = string.Empty;
        
        // Alias to match requested schema property Name
        public string Specialization 
        { 
            get => Specialty; 
            set => Specialty = value; 
        }

        public string RoomNumber { get; set; } = string.Empty;
        public bool IsAvailable { get; set; } = true;
        public int AverageConsultationTime { get; set; } = 10; // in minutes

        // Reference to security User account for dashboard authentication
        public Guid? UserId { get; set; }
        public User? User { get; set; }
    }
}
