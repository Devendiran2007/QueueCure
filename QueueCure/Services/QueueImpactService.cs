using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using QueueCure.Data;
using QueueCure.Models;
using QueueCure.Repositories;

namespace QueueCure.Services
{
    public class QueueImpactService : IQueueImpactService
    {
        private readonly QueueCureDbContext _context;
        private readonly IQueueRepository _queueRepository;

        public QueueImpactService(QueueCureDbContext context, IQueueRepository queueRepository)
        {
            _context = context;
            _queueRepository = queueRepository;
        }

        public async Task<Dictionary<Guid, double>> GetCurrentWaitTimesSnapshotAsync(Guid doctorId)
        {
            var activePatients = (await _queueRepository.GetActivePatientsForDoctorAsync(doctorId)).ToList();
            var snapshot = new Dictionary<Guid, double>();

            for (int i = 0; i < activePatients.Count; i++)
            {
                var patient = activePatients[i];
                if (patient.Status == PatientStatus.Waiting)
                {
                    double waitTime = await CalculateWaitTimeForPatientAsync(patient, activePatients, i);
                    snapshot[patient.Id] = waitTime;
                }
            }

            return snapshot;
        }

        public async Task RecordImpactAsync(string eventType, string eventDetail, Guid doctorId, Dictionary<Guid, double> preWaitTimes)
        {
            var activePatients = (await _queueRepository.GetActivePatientsForDoctorAsync(doctorId)).ToList();
            var timestamp = DateTime.UtcNow;

            for (int i = 0; i < activePatients.Count; i++)
            {
                var patient = activePatients[i];
                if (patient.Status == PatientStatus.Waiting)
                {
                    double waitTimeAfter = await CalculateWaitTimeForPatientAsync(patient, activePatients, i);
                    preWaitTimes.TryGetValue(patient.Id, out double waitTimeBefore);

                    double impact = waitTimeAfter - waitTimeBefore;

                    // Log impact in db (allow both positive and negative shifts, but only if they are not practically zero)
                    if (Math.Abs(impact) >= 0.1)
                    {
                        var record = new QueueImpact
                        {
                            Id = Guid.NewGuid(),
                            EventType = eventType,
                            EventDetail = eventDetail,
                            Timestamp = timestamp,
                            PatientId = patient.Id,
                            TokenNumber = patient.TokenNumber,
                            WaitTimeBefore = Math.Round(waitTimeBefore, 1),
                            WaitTimeAfter = Math.Round(waitTimeAfter, 1),
                            ImpactMinutes = Math.Round(impact, 1)
                        };

                        await _context.QueueImpacts.AddAsync(record);
                    }
                }
            }

            await _context.SaveChangesAsync();
        }

        private async Task<double> CalculateWaitTimeForPatientAsync(Patient patient, List<Patient> activePatients, int index)
        {
            if (index < 0) return 0;

            // 1. Remaining time of current consultation
            double remainingTime = 0;
            var ongoingPatient = activePatients.FirstOrDefault(p => p.Status == PatientStatus.InConsultation);
            if (ongoingPatient != null)
            {
                double ongoingEstDuration = await _queueRepository.GetAverageConsultationDurationAsync(ongoingPatient.Category);
                
                // Get the start of consultation event
                var startEvent = (await _queueRepository.GetEventsByPatientIdAsync(ongoingPatient.Id))
                    .Where(e => e.EventType == "Started" || e.EventType == "Called")
                    .OrderByDescending(e => e.Timestamp)
                    .FirstOrDefault();

                if (startEvent != null)
                {
                    double elapsed = (DateTime.UtcNow - startEvent.Timestamp).TotalMinutes;
                    
                    // Smart delay detection adjusts remaining time: 
                    // If running behind schedule, we estimate remaining time is at least 3 minutes
                    if (elapsed > ongoingEstDuration)
                    {
                        remainingTime = 3.0; 
                    }
                    else
                    {
                        remainingTime = ongoingEstDuration - elapsed;
                    }
                }
                else
                {
                    remainingTime = ongoingEstDuration;
                }
            }

            // 2. Sum of estimated durations for all waiting patients checked in before this patient in priority order
            double waitingAheadDuration = 0;
            var waitingPatientsAhead = activePatients.Take(index).Where(p => p.Status == PatientStatus.Waiting).ToList();

            foreach (var p in waitingPatientsAhead)
            {
                waitingAheadDuration += await _queueRepository.GetAverageConsultationDurationAsync(p.Category);
            }

            return Math.Round(remainingTime + waitingAheadDuration, 1);
        }
    }
}
