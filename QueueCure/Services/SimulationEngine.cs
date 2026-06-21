using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QueueCure.Data;
using QueueCure.Models;
using QueueCure.Repositories;

namespace QueueCure.Services
{
    public class SimulationEngine : ISimulationEngine
    {
        private readonly QueueCureDbContext _context;
        private readonly IQueueRepository _queueRepository;

        public SimulationEngine(QueueCureDbContext context, IQueueRepository queueRepository)
        {
            _context = context;
            _queueRepository = queueRepository;
        }

        public async Task<SimulationResultDto> RunSimulationAsync(SimulationRequestDto request)
        {
            // Fetch real status
            var doctors = (await _queueRepository.GetAllDoctorsAsync()).ToList();
            var patientsToday = await _queueRepository.GetAllPatientsTodayAsync();
            var activePatients = patientsToday.Where(p => p.Status == PatientStatus.Waiting || p.Status == PatientStatus.InConsultation).ToList();

            // Real calculations
            var (realAvgWait, realQueueLength, realDelays, realUtil) = await CalculateMetricsAsync(doctors, activePatients);

            // Simulated calculations variables
            double simAvgWait = realAvgWait;
            int simQueueLength = realQueueLength;
            int simDelays = realDelays;
            double simUtil = realUtil;

            if (request.Scenario == "A")
            {
                // Scenario A: Add one more doctor
                var simDoctors = new List<Doctor>(doctors);
                var newDoctor = new Doctor
                {
                    Id = Guid.NewGuid(),
                    Name = "Dr. Simulated Intern",
                    Specialization = "General Medicine",
                    RoomNumber = "103",
                    AverageConsultationTime = 10,
                    IsAvailable = true
                };
                simDoctors.Add(newDoctor);

                // Redistribute waiting patients across all doctors to simulate optimal load balancing
                var waitingPatients = activePatients.Where(p => p.Status == PatientStatus.Waiting).OrderBy(p => p.CheckInTime).ToList();
                var ongoingPatients = activePatients.Where(p => p.Status == PatientStatus.InConsultation).ToList();

                var simActivePatients = new List<Patient>();
                simActivePatients.AddRange(ongoingPatients); // Ongoing remain as is

                // Round-robin assign waiting patients to all doctors
                int docIndex = 0;
                foreach (var p in waitingPatients)
                {
                    var assignedDoc = simDoctors[docIndex % simDoctors.Count];
                    var copyPatient = ClonePatient(p);
                    copyPatient.DoctorId = assignedDoc.Id;
                    simActivePatients.Add(copyPatient);
                    docIndex++;
                }

                var (avgW, qL, del, ut) = await CalculateMetricsAsync(simDoctors, simActivePatients);
                simAvgWait = avgW;
                simQueueLength = qL;
                simDelays = del;
                // Utilization drops since workload is shared with a new doctor
                simUtil = Math.Round(realUtil * (double)doctors.Count / simDoctors.Count, 1);
            }
            else if (request.Scenario == "B")
            {
                // Scenario B: Two emergency patients arrive
                var simActivePatients = new List<Patient>();
                // Clone existing
                foreach (var p in activePatients)
                {
                    simActivePatients.Add(ClonePatient(p));
                }

                // Add 2 emergency patients to the focus doctor or first doctor
                var targetDoctorId = request.DoctorId ?? doctors.FirstOrDefault()?.Id ?? Guid.Empty;
                if (targetDoctorId != Guid.Empty)
                {
                    for (int i = 1; i <= 2; i++)
                    {
                        var emergencyPatient = new Patient
                        {
                            Id = Guid.NewGuid(),
                            Name = $"Simulated Emergency Patient {i}",
                            TokenNumber = $"EMERG-{i}",
                            CheckInTime = DateTime.UtcNow,
                            Status = PatientStatus.Waiting,
                            IsEmergency = true,
                            DoctorId = targetDoctorId,
                            Category = VisitCategory.GeneralCheckup
                        };
                        // Insert emergency at the beginning of waiting queue
                        simActivePatients.Add(emergencyPatient);
                    }
                }

                var (avgW, qL, del, ut) = await CalculateMetricsAsync(doctors, simActivePatients);
                simAvgWait = avgW;
                simQueueLength = qL;
                simDelays = del;
                simUtil = Math.Clamp(realUtil + 15.0, 0, 100);
            }
            else if (request.Scenario == "C")
            {
                // Scenario C: Doctor consultation speed improves by 20%
                var simActivePatients = new List<Patient>();
                foreach (var p in activePatients)
                {
                    simActivePatients.Add(ClonePatient(p));
                }

                // Recalculate using Speed multiplier 0.8
                var (avgW, qL, del, ut) = await CalculateMetricsAsync(doctors, simActivePatients, speedMultiplier: 0.8);
                simAvgWait = avgW;
                simQueueLength = qL;
                simDelays = Math.Max(0, realDelays - 1); // Delays decrease since consultations are faster
                simUtil = Math.Clamp(realUtil - 8.0, 5.0, 100); // Doctor idle time increases slightly
            }
            else if (request.Scenario == "D")
            {
                // Scenario D: Expected patient volume increases by 30%
                var simActivePatients = new List<Patient>();
                foreach (var p in activePatients)
                {
                    simActivePatients.Add(ClonePatient(p));
                }

                int extraPatientsCount = (int)Math.Max(1, Math.Round(realQueueLength * 0.3));
                var random = new Random();

                for (int i = 1; i <= extraPatientsCount; i++)
                {
                    var doc = doctors[random.Next(doctors.Count)];
                    var extraPatient = new Patient
                    {
                        Id = Guid.NewGuid(),
                        Name = $"Simulated Volume Patient {i}",
                        TokenNumber = $"VOL-{i}",
                        CheckInTime = DateTime.UtcNow.AddMinutes(5 * i),
                        Status = PatientStatus.Waiting,
                        DoctorId = doc.Id,
                        Category = (VisitCategory)random.Next(Enum.GetValues(typeof(VisitCategory)).Length)
                    };
                    simActivePatients.Add(extraPatient);
                }

                var (avgW, qL, del, ut) = await CalculateMetricsAsync(doctors, simActivePatients);
                simAvgWait = avgW;
                simQueueLength = qL;
                simDelays = del;
                simUtil = Math.Clamp(realUtil + 12.0, 0, 100);
            }

            return new SimulationResultDto
            {
                RealAvgWaitTime = Math.Round(realAvgWait, 1),
                RealQueueLength = realQueueLength,
                RealPredictedDelaysCount = realDelays,
                RealAvgDoctorUtilization = Math.Round(realUtil, 1),

                SimulatedAvgWaitTime = Math.Round(simAvgWait, 1),
                SimulatedQueueLength = simQueueLength,
                SimulatedPredictedDelaysCount = simDelays,
                SimulatedAvgDoctorUtilization = Math.Round(simUtil, 1)
            };
        }

        private Patient ClonePatient(Patient p)
        {
            return new Patient
            {
                Id = p.Id,
                Name = p.Name,
                PhoneNumber = p.PhoneNumber,
                TokenNumber = p.TokenNumber,
                CheckInTime = p.CheckInTime,
                Status = p.Status,
                Category = p.Category,
                IsEmergency = p.IsEmergency,
                IsRestored = p.IsRestored,
                RestoredTime = p.RestoredTime,
                QueuePositionWhenAdded = p.QueuePositionWhenAdded,
                ConsultationStartTime = p.ConsultationStartTime,
                ConsultationEndTime = p.ConsultationEndTime,
                DoctorId = p.DoctorId
            };
        }

        private async Task<(double AvgWait, int QueueLength, int Delays, double Utilization)> CalculateMetricsAsync(
            List<Doctor> docs, List<Patient> patients, double speedMultiplier = 1.0)
        {
            var waitingPatients = patients.Where(p => p.Status == PatientStatus.Waiting).ToList();
            int queueLength = waitingPatients.Count;
            int delaysCount = 0;
            var waitTimes = new List<double>();

            // Group by doctor
            foreach (var doc in docs)
            {
                var docPatients = patients.Where(p => p.DoctorId == doc.Id).ToList();
                
                // Sort order for wait calculations
                var ongoing = docPatients.Where(p => p.Status == PatientStatus.InConsultation).ToList();
                var waiting = docPatients.Where(p => p.Status == PatientStatus.Waiting).ToList();
                var emergencies = waiting.Where(p => p.IsEmergency).OrderBy(p => p.CheckInTime).ToList();
                var standards = waiting.Where(p => !p.IsEmergency && !p.IsRestored).OrderBy(p => p.CheckInTime).ToList();
                var restored = waiting.Where(p => !p.IsEmergency && p.IsRestored).OrderBy(p => p.RestoredTime ?? p.CheckInTime).ToList();

                var sorted = new List<Patient>();
                sorted.AddRange(ongoing);
                sorted.AddRange(emergencies);
                if (standards.Any())
                {
                    sorted.Add(standards.First());
                    sorted.AddRange(restored);
                    sorted.AddRange(standards.Skip(1));
                }
                else
                {
                    sorted.AddRange(restored);
                }

                // Ongoing patient delay check
                double remainingTime = 0;
                var ongoingPatient = ongoing.FirstOrDefault();
                if (ongoingPatient != null)
                {
                    double ongoingEstDuration = (await _queueRepository.GetAverageConsultationDurationAsync(ongoingPatient.Category)) * speedMultiplier;
                    var startTime = ongoingPatient.ConsultationStartTime ?? DateTime.UtcNow.AddMinutes(-5);
                    double elapsed = (DateTime.UtcNow - startTime).TotalMinutes;

                    if (elapsed > ongoingEstDuration)
                    {
                        delaysCount++;
                        remainingTime = 3.0; // Assume 3 minutes buffer
                    }
                    else
                    {
                        remainingTime = ongoingEstDuration - elapsed;
                    }
                }

                // Calculate waiting wait-times
                double waitingAheadDuration = 0;
                var docWaiting = sorted.Where(p => p.Status == PatientStatus.Waiting).ToList();

                for (int i = 0; i < docWaiting.Count; i++)
                {
                    var p = docWaiting[i];
                    double wait = remainingTime + waitingAheadDuration;
                    waitTimes.Add(wait);

                    // Accumulate expected time for next
                    waitingAheadDuration += (await _queueRepository.GetAverageConsultationDurationAsync(p.Category)) * speedMultiplier;
                }
            }

            double avgWait = waitTimes.Any() ? waitTimes.Average() : 0.0;

            // Simple utilization score based on workload
            double totalCapacity = docs.Count * 480.0; // 8 hours in minutes
            double totalWorkload = (queueLength * 12.0 + (patients.Count(p => p.Status == PatientStatus.InConsultation) * 15.0)) * speedMultiplier;
            double utilization = totalCapacity > 0 ? (totalWorkload / totalCapacity) * 100.0 : 0.0;
            utilization = Math.Clamp(utilization + 30.0, 5.0, 95.0); // Baseline active shift presence

            return (avgWait, queueLength, delaysCount, utilization);
        }
    }
}
