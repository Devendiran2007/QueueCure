using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QueueCure.Data;
using QueueCure.Models;

namespace QueueCure.Repositories
{
    public class QueueRepository : IQueueRepository
    {
        private readonly QueueCureDbContext _context;

        public QueueRepository(QueueCureDbContext context)
        {
            _context = context;
        }

        // Patient Queries
        public async Task<Patient?> GetPatientByIdAsync(Guid id)
        {
            return await _context.Patients
                .Include(p => p.Doctor)
                .ThenInclude(d => d!.User)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<Patient?> GetPatientByTokenNumberAsync(string tokenNumber)
        {
            return await _context.Patients
                .Include(p => p.Doctor)
                .ThenInclude(d => d!.User)
                .FirstOrDefaultAsync(p => p.TokenNumber == tokenNumber);
        }

        public async Task<IEnumerable<Patient>> GetActivePatientsForDoctorAsync(Guid doctorId)
        {
            return await _context.Patients
                .Include(p => p.Doctor)
                .ThenInclude(d => d!.User)
                .Where(p => p.DoctorId == doctorId && 
                            (p.Status == PatientStatus.Waiting || 
                             p.Status == PatientStatus.InConsultation))
                .OrderBy(p => p.Status == PatientStatus.InConsultation ? 0 : 1)
                .ThenBy(p => p.IsEmergency ? 0 : 1)
                .ThenBy(p => p.CheckInTime)
                .ToListAsync();
        }

        public async Task<IEnumerable<Patient>> GetPatientsByStatusAsync(PatientStatus status)
        {
            return await _context.Patients
                .Include(p => p.Doctor)
                .ThenInclude(d => d!.User)
                .Where(p => p.Status == status)
                .OrderBy(p => p.CheckInTime)
                .ToListAsync();
        }

        public async Task<IEnumerable<Patient>> GetAllPatientsTodayAsync()
        {
            var today = DateTime.UtcNow.Date;
            return await _context.Patients
                .Include(p => p.Doctor)
                .ThenInclude(d => d!.User)
                .Where(p => p.CheckInTime >= today)
                .OrderBy(p => p.CheckInTime)
                .ToListAsync();
        }

        public async Task<int> GetNextSequenceNumberAsync(Guid doctorId)
        {
            var today = DateTime.UtcNow.Date;
            var count = await _context.Patients
                .CountAsync(p => p.DoctorId == doctorId && p.CheckInTime >= today);
            return count + 1;
        }

        public async Task<int> GetWaitingCountBeforePatientAsync(Patient patient)
        {
            var activePatients = await GetActivePatientsForDoctorAsync(patient.DoctorId);
            var list = activePatients.ToList();
            var index = list.FindIndex(p => p.Id == patient.Id);
            if (index <= 0) return 0;

            return list.Take(index).Count(p => p.Status == PatientStatus.Waiting);
        }

        public async Task AddPatientAsync(Patient patient)
        {
            await _context.Patients.AddAsync(patient);
            await _context.SaveChangesAsync();
        }

        public async Task UpdatePatientAsync(Patient patient)
        {
            _context.Patients.Update(patient);
            await _context.SaveChangesAsync();
        }

        // Doctor Queries
        public async Task<Doctor?> GetDoctorByIdAsync(Guid doctorId)
        {
            return await _context.Doctors
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.Id == doctorId);
        }

        public async Task<Doctor?> GetDoctorByUserIdAsync(Guid userId)
        {
            return await _context.Doctors
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.UserId == userId);
        }

        public async Task<IEnumerable<Doctor>> GetAllDoctorsAsync()
        {
            return await _context.Doctors
                .Include(d => d.User)
                .ToListAsync();
        }

        public async Task AddDoctorAsync(Doctor doctor)
        {
            await _context.Doctors.AddAsync(doctor);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateDoctorAsync(Doctor doctor)
        {
            _context.Doctors.Update(doctor);
            await _context.SaveChangesAsync();
        }

        // QueueEvents Logs
        public async Task AddQueueEventAsync(QueueEvent queueEvent)
        {
            await _context.QueueEvents.AddAsync(queueEvent);
            await _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<QueueEvent>> GetEventsByPatientIdAsync(Guid patientId)
        {
            return await _context.QueueEvents
                .Where(q => q.PatientId == patientId)
                .OrderBy(q => q.Timestamp)
                .ToListAsync();
        }

        // Notifications Logs
        public async Task AddNotificationAsync(Notification notification)
        {
            await _context.Notifications.AddAsync(notification);
            await _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<Notification>> GetNotificationsByPatientIdAsync(Guid patientId)
        {
            return await _context.Notifications
                .Where(n => n.PatientId == patientId)
                .OrderBy(n => n.CreatedAt)
                .ToListAsync();
        }

        public async Task UpdateNotificationAsync(Notification notification)
        {
            _context.Notifications.Update(notification);
            await _context.SaveChangesAsync();
        }

        // Global Settings
        public async Task<QueueSettings> GetSettingsAsync()
        {
            var settings = await _context.QueueSettings.FirstOrDefaultAsync();
            if (settings == null)
            {
                settings = new QueueSettings
                {
                    AverageConsultationTime = 10,
                    LastTokenNumber = "Q-000"
                };
                await _context.QueueSettings.AddAsync(settings);
                await _context.SaveChangesAsync();
            }
            return settings;
        }

        public async Task UpdateSettingsAsync(QueueSettings settings)
        {
            _context.QueueSettings.Update(settings);
            await _context.SaveChangesAsync();
        }

        public async Task<double> GetAverageConsultationDurationAsync(VisitCategory category)
        {
            var completedPatients = await _context.Patients
                .Where(p => p.Category == category && p.Status == PatientStatus.Completed)
                .Select(p => p.Id)
                .ToListAsync();

            if (!completedPatients.Any())
            {
                return category switch
                {
                    VisitCategory.Fever => 8.0,
                    VisitCategory.GeneralCheckup => 12.0,
                    VisitCategory.DiabetesReview => 15.0,
                    VisitCategory.CardiologyConsultation => 20.0,
                    VisitCategory.FollowUp => 5.0,
                    _ => 10.0
                };
            }

            var events = await _context.QueueEvents
                .Where(e => completedPatients.Contains(e.PatientId) && 
                            (e.EventType == "Completed" || e.EventType == "Started" || e.EventType == "Called"))
                .ToListAsync();

            var eventsByPatient = events.GroupBy(e => e.PatientId);

            var durations = new List<double>();
            foreach (var group in eventsByPatient)
            {
                var completedEvent = group.FirstOrDefault(e => e.EventType == "Completed");
                if (completedEvent == null) continue;

                var startEvent = group.FirstOrDefault(e => e.EventType == "Started") 
                                 ?? group.FirstOrDefault(e => e.EventType == "Called");

                if (startEvent != null && completedEvent.Timestamp > startEvent.Timestamp)
                {
                    durations.Add((completedEvent.Timestamp - startEvent.Timestamp).TotalMinutes);
                }
            }

            if (!durations.Any())
            {
                return category switch
                {
                    VisitCategory.Fever => 8.0,
                    VisitCategory.GeneralCheckup => 12.0,
                    VisitCategory.DiabetesReview => 15.0,
                    VisitCategory.CardiologyConsultation => 20.0,
                    VisitCategory.FollowUp => 5.0,
                    _ => 10.0
                };
            }

            return durations.Average();
        }
    }
}
