using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QueueCure.Models;

namespace QueueCure.Repositories
{
    public interface IQueueRepository
    {
        // Patient Queries
        Task<Patient?> GetPatientByIdAsync(Guid id);
        Task<Patient?> GetPatientByTokenNumberAsync(string tokenNumber);
        Task<IEnumerable<Patient>> GetActivePatientsForDoctorAsync(Guid doctorId);
        Task<IEnumerable<Patient>> GetPatientsByStatusAsync(PatientStatus status);
        Task<IEnumerable<Patient>> GetAllPatientsTodayAsync();
        Task<int> GetNextSequenceNumberAsync(Guid doctorId);
        Task<int> GetWaitingCountBeforePatientAsync(Patient patient);
        Task AddPatientAsync(Patient patient);
        Task UpdatePatientAsync(Patient patient);
        Task<double> GetAverageConsultationDurationAsync(VisitCategory category);

        // Doctor Queries
        Task<Doctor?> GetDoctorByIdAsync(Guid doctorId);
        Task<Doctor?> GetDoctorByUserIdAsync(Guid userId);
        Task<IEnumerable<Doctor>> GetAllDoctorsAsync();
        Task AddDoctorAsync(Doctor doctor);
        Task UpdateDoctorAsync(Doctor doctor);

        // QueueEvents Logs
        Task AddQueueEventAsync(QueueEvent queueEvent);
        Task<IEnumerable<QueueEvent>> GetEventsByPatientIdAsync(Guid patientId);

        // Notifications Logs
        Task AddNotificationAsync(Notification notification);
        Task<IEnumerable<Notification>> GetNotificationsByPatientIdAsync(Guid patientId);
        Task UpdateNotificationAsync(Notification notification);

        // Global Settings
        Task<QueueSettings> GetSettingsAsync();
        Task UpdateSettingsAsync(QueueSettings settings);
    }
}
