using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using QueueCure.Hubs;
using QueueCure.Models;
using QueueCure.Repositories;

namespace QueueCure.Services
{
    public class QueueService : IQueueService
    {
        private readonly IQueueRepository _queueRepository;
        private readonly IHubContext<QueueHub> _hubContext;

        public QueueService(IQueueRepository queueRepository, IHubContext<QueueHub> hubContext)
        {
            _queueRepository = queueRepository;
            _hubContext = hubContext;
        }

        public async Task<Patient> GenerateTokenAsync(string patientName, string patientPhone, Guid doctorId)
        {
            var doctor = await _queueRepository.GetDoctorByIdAsync(doctorId);
            if (doctor == null)
            {
                throw new ArgumentException("Doctor not found.");
            }

            var nextSeq = await _queueRepository.GetNextSequenceNumberAsync(doctorId);
            
            // Format token number: e.g. 101-005 (Room Prefix + Sequence)
            string prefix = !string.IsNullOrWhiteSpace(doctor.RoomNumber) ? doctor.RoomNumber.Trim() : "Q";
            string tokenNumber = $"{prefix}-{nextSeq:D3}";

            var patient = new Patient
            {
                Name = patientName,
                PhoneNumber = patientPhone,
                TokenNumber = tokenNumber,
                CheckInTime = DateTime.UtcNow,
                Status = PatientStatus.Waiting,
                DoctorId = doctorId
            };

            await _queueRepository.AddPatientAsync(patient);

            // Log Check-In Event
            var checkInEvent = new QueueEvent
            {
                PatientId = patient.Id,
                EventType = "CheckIn",
                Timestamp = DateTime.UtcNow
            };
            await _queueRepository.AddQueueEventAsync(checkInEvent);

            // Update global settings
            var settings = await _queueRepository.GetSettingsAsync();
            settings.LastTokenNumber = tokenNumber;
            await _queueRepository.UpdateSettingsAsync(settings);

            // Notify clients real-time
            await _hubContext.Clients.All.SendAsync("QueueUpdated");
            await _hubContext.Clients.Group(doctorId.ToString()).SendAsync("DoctorQueueUpdated", doctorId);

            return patient;
        }

        public async Task<Patient?> CallNextTokenAsync(Guid doctorId)
        {
            var doctor = await _queueRepository.GetDoctorByIdAsync(doctorId);
            if (doctor == null) return null;

            var activePatients = await _queueRepository.GetActivePatientsForDoctorAsync(doctorId);

            // If there's an ongoing consultation, doctor must complete or skip it first.
            var ongoing = activePatients.FirstOrDefault(p => p.Status == PatientStatus.InConsultation);
            if (ongoing != null)
            {
                throw new InvalidOperationException("Please complete or skip the current patient first.");
            }

            // Find next waiting patient
            var nextWaiting = activePatients.FirstOrDefault(p => p.Status == PatientStatus.Waiting);
            if (nextWaiting == null) return null;

            // Transition status to InConsultation (called status in this model)
            nextWaiting.Status = PatientStatus.InConsultation;
            await _queueRepository.UpdatePatientAsync(nextWaiting);

            // Log Called Event
            var callEvent = new QueueEvent
            {
                PatientId = nextWaiting.Id,
                EventType = "Called",
                Timestamp = DateTime.UtcNow
            };
            await _queueRepository.AddQueueEventAsync(callEvent);

            // Create notification alert for patient
            var notification = new Notification
            {
                PatientId = nextWaiting.Id,
                Message = $"Your token {nextWaiting.TokenNumber} is now called! Please proceed to Room {doctor.RoomNumber}.",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };
            await _queueRepository.AddNotificationAsync(notification);

            // Broadcast real-time call event (triggers TV sound and displays popups)
            await _hubContext.Clients.All.SendAsync("TokenCalled", new
            {
                tokenNumber = nextWaiting.TokenNumber,
                patientName = nextWaiting.Name,
                roomNumber = doctor.RoomNumber,
                doctorName = doctor.Name
            });
            await _hubContext.Clients.All.SendAsync("QueueUpdated");
            await _hubContext.Clients.Group(doctorId.ToString()).SendAsync("DoctorQueueUpdated", doctorId);

            return nextWaiting;
        }

        public async Task<Patient?> StartConsultationAsync(Guid patientId)
        {
            var patient = await _queueRepository.GetPatientByIdAsync(patientId);
            if (patient == null || patient.Status != PatientStatus.InConsultation) return null;

            // Log started event (marks the transition from Called to examination room)
            var startEvent = new QueueEvent
            {
                PatientId = patient.Id,
                EventType = "Started",
                Timestamp = DateTime.UtcNow
            };
            await _queueRepository.AddQueueEventAsync(startEvent);

            // Notify
            await _hubContext.Clients.All.SendAsync("QueueUpdated");
            await _hubContext.Clients.Group(patient.DoctorId.ToString()).SendAsync("DoctorQueueUpdated", patient.DoctorId);

            return patient;
        }

        public async Task<Patient?> CompleteConsultationAsync(Guid patientId)
        {
            var patient = await _queueRepository.GetPatientByIdAsync(patientId);
            if (patient == null) return null;

            patient.Status = PatientStatus.Completed;
            await _queueRepository.UpdatePatientAsync(patient);

            // Log Completed Event
            var completeEvent = new QueueEvent
            {
                PatientId = patient.Id,
                EventType = "Completed",
                Timestamp = DateTime.UtcNow
            };
            await _queueRepository.AddQueueEventAsync(completeEvent);

            // Notify
            await _hubContext.Clients.All.SendAsync("QueueUpdated");
            await _hubContext.Clients.Group(patient.DoctorId.ToString()).SendAsync("DoctorQueueUpdated", patient.DoctorId);

            return patient;
        }

        public async Task<Patient?> SkipPatientAsync(Guid patientId)
        {
            var patient = await _queueRepository.GetPatientByIdAsync(patientId);
            if (patient == null) return null;

            patient.Status = PatientStatus.Skipped;
            await _queueRepository.UpdatePatientAsync(patient);

            // Log Skipped Event
            var skipEvent = new QueueEvent
            {
                PatientId = patient.Id,
                EventType = "Skipped",
                Timestamp = DateTime.UtcNow
            };
            await _queueRepository.AddQueueEventAsync(skipEvent);

            // Create notification alert for patient
            var notification = new Notification
            {
                PatientId = patient.Id,
                Message = $"Your token {patient.TokenNumber} has been marked as skipped due to no-show.",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };
            await _queueRepository.AddNotificationAsync(notification);

            // Notify
            await _hubContext.Clients.All.SendAsync("QueueUpdated");
            await _hubContext.Clients.Group(patient.DoctorId.ToString()).SendAsync("DoctorQueueUpdated", patient.DoctorId);

            return patient;
        }

        public async Task<IEnumerable<Patient>> GetActiveQueueForDoctorAsync(Guid doctorId)
        {
            return await _queueRepository.GetActivePatientsForDoctorAsync(doctorId);
        }

        public async Task<Patient?> GetPatientDetailsAsync(string tokenNumber)
        {
            return await _queueRepository.GetPatientByTokenNumberAsync(tokenNumber);
        }

        public async Task<IEnumerable<Doctor>> GetDoctorsStatusAsync()
        {
            return await _queueRepository.GetAllDoctorsAsync();
        }

        public async Task<object> GetTVDashboardDataAsync()
        {
            var patientsToday = await _queueRepository.GetAllPatientsTodayAsync();
            var doctors = await _queueRepository.GetAllDoctorsAsync();

            var servingPatients = patientsToday.Where(p => p.Status == PatientStatus.InConsultation).ToList();
            var waitingPatients = patientsToday.Where(p => p.Status == PatientStatus.Waiting).ToList();

            // Compute dynamic estimated wait times for waiting list
            var waitingList = new List<object>();
            foreach (var p in waitingPatients)
            {
                var doctor = doctors.FirstOrDefault(d => d.Id == p.DoctorId);
                var avgTime = doctor?.AverageConsultationTime ?? 10;
                var waitingCount = await _queueRepository.GetWaitingCountBeforePatientAsync(p.DoctorId, p.CheckInTime);
                
                waitingList.Add(new
                {
                    tokenNumber = p.TokenNumber,
                    doctorName = doctor?.Name ?? "Doctor",
                    estimatedWaitMinutes = waitingCount * avgTime
                });
            }

            return new
            {
                NowServing = servingPatients.Select(p => new {
                    tokenNumber = p.TokenNumber,
                    patientName = p.Name,
                    doctorName = p.Doctor?.Name ?? "Doctor",
                    roomNumber = p.Doctor?.RoomNumber ?? "N/A"
                }),
                WaitingList = waitingList,
                Doctors = doctors.Select(d => new {
                    doctorName = d.Name,
                    d.Specialty,
                    d.RoomNumber,
                    d.IsAvailable,
                    CurrentTokenNumber = servingPatients.FirstOrDefault(p => p.DoctorId == d.Id)?.TokenNumber
                })
            };
        }

        public async Task UpdateGlobalSettingsAsync(int averageTime)
        {
            var settings = await _queueRepository.GetSettingsAsync();
            settings.AverageConsultationTime = averageTime;
            await _queueRepository.UpdateSettingsAsync(settings);
            
            // Broadcast live updates
            await _hubContext.Clients.All.SendAsync("QueueUpdated");
        }

        public async Task UpdateDoctorConsultationTimeAsync(Guid doctorId, int averageTime)
        {
            var doctor = await _queueRepository.GetDoctorByIdAsync(doctorId);
            if (doctor != null)
            {
                doctor.AverageConsultationTime = averageTime;
                await _queueRepository.UpdateDoctorAsync(doctor);
                
                // Broadcast live updates
                await _hubContext.Clients.All.SendAsync("QueueUpdated");
                await _hubContext.Clients.Group(doctorId.ToString()).SendAsync("DoctorQueueUpdated", doctorId);
            }
        }
    }
}
