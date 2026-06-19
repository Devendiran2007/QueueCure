using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QueueCure.Models;

namespace QueueCure.Services
{
    public interface IQueueService
    {
        Task<Patient> GenerateTokenAsync(string patientName, string patientPhone, Guid doctorId, VisitCategory category, bool isEmergency = false);
        Task<Patient?> CallNextTokenAsync(Guid doctorId);
        Task<Patient?> StartConsultationAsync(Guid patientId);
        Task<Patient?> CompleteConsultationAsync(Guid patientId);
        Task<Patient?> SkipPatientAsync(Guid patientId);
        Task<Patient?> MarkEmergencyAsync(Guid patientId);
        Task<Patient?> RestorePatientAsync(Guid patientId);
        
        Task<IEnumerable<Patient>> GetActiveQueueForDoctorAsync(Guid doctorId);
        Task<double> CalculateEstimatedWaitTimeAsync(Patient patient);
        Task<Patient?> GetPatientDetailsAsync(string tokenNumber);
        Task<IEnumerable<Doctor>> GetDoctorsStatusAsync();
        Task<object> GetTVDashboardDataAsync();

        // Settings updates
        Task UpdateGlobalSettingsAsync(int averageTime);
        Task UpdateDoctorConsultationTimeAsync(Guid doctorId, int averageTime);
    }
}
