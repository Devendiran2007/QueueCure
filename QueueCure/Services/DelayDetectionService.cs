using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using QueueCure.Data;
using QueueCure.Hubs;
using QueueCure.Models;
using QueueCure.Repositories;

namespace QueueCure.Services
{
    public class DelayDetectionService : IDelayDetectionService
    {
        private readonly QueueCureDbContext _context;
        private readonly IQueueRepository _queueRepository;
        private readonly IQueueImpactService _queueImpactService;
        private readonly IHubContext<QueueHub> _hubContext;

        public DelayDetectionService(
            QueueCureDbContext context,
            IQueueRepository queueRepository,
            IQueueImpactService queueImpactService,
            IHubContext<QueueHub> hubContext)
        {
            _context = context;
            _queueRepository = queueRepository;
            _queueImpactService = queueImpactService;
            _hubContext = hubContext;
        }

        public async Task CheckActiveConsultationsAsync()
        {
            // Find all active doctors
            var doctors = await _queueRepository.GetAllDoctorsAsync();

            foreach (var doc in doctors)
            {
                var activePatients = (await _queueRepository.GetActivePatientsForDoctorAsync(doc.Id)).ToList();
                var ongoingPatient = activePatients.FirstOrDefault(p => p.Status == PatientStatus.InConsultation);

                if (ongoingPatient == null) continue;

                // Determine start of consultation
                var start = ongoingPatient.ConsultationStartTime;
                if (start == null)
                {
                    // Fallback to Called/Started event timestamp
                    var startEvent = (await _queueRepository.GetEventsByPatientIdAsync(ongoingPatient.Id))
                        .Where(e => e.EventType == "Started" || e.EventType == "Called")
                        .OrderByDescending(e => e.Timestamp)
                        .FirstOrDefault();
                    start = startEvent?.Timestamp;
                }

                if (start == null) continue;

                double elapsed = (DateTime.UtcNow - start.Value).TotalMinutes;
                double expected = await _queueRepository.GetAverageConsultationDurationAsync(ongoingPatient.Category);

                // Configurable threshold: 5 minutes
                double threshold = 5.0;

                if (elapsed > expected + threshold)
                {
                    double delayMinutes = elapsed - expected;

                    // Check if already logged for this patient
                    bool alreadyLogged = await _context.DelayEvents
                        .AnyAsync(d => d.PatientId == ongoingPatient.Id && !d.IsResolved);

                    if (!alreadyLogged)
                    {
                        // 1. Snapshot wait times before applying the delay
                        // Since standard estimation defaults to Math.Max(0, expected - elapsed), it would estimate 0.
                        var preWaitTimes = new Dictionary<Guid, double>();
                        for (int i = 0; i < activePatients.Count; i++)
                        {
                            var p = activePatients[i];
                            if (p.Status == PatientStatus.Waiting)
                            {
                                // Calculate wait time assuming remaining time for current patient is 0
                                double waitingAhead = 0;
                                var ahead = activePatients.Take(i).Where(x => x.Status == PatientStatus.Waiting).ToList();
                                foreach (var x in ahead)
                                {
                                    waitingAhead += await _queueRepository.GetAverageConsultationDurationAsync(x.Category);
                                }
                                preWaitTimes[p.Id] = Math.Round(waitingAhead, 1);
                            }
                        }

                        // 2. Log Delay Event
                        var delayEvent = new DelayEvent
                        {
                            Id = Guid.NewGuid(),
                            DoctorId = doc.Id,
                            PatientId = ongoingPatient.Id,
                            ExpectedDuration = Math.Round(expected, 1),
                            ActualDuration = Math.Round(elapsed, 1),
                            DelayMinutes = Math.Round(delayMinutes, 1),
                            Timestamp = DateTime.UtcNow,
                            IsResolved = false
                        };

                        await _context.DelayEvents.AddAsync(delayEvent);
                        await _context.SaveChangesAsync();

                        // 3. Record Impact for all waiting patients (adds the delay impact)
                        string detail = $"Doctor {doc.Name} running {Math.Round(delayMinutes)} mins behind on token {ongoingPatient.TokenNumber}";
                        await _queueImpactService.RecordImpactAsync("ConsultationDelay", detail, doc.Id, preWaitTimes);

                        // 4. Broadcast Real-Time SignalR Delay Alerts
                        await _hubContext.Clients.All.SendAsync("DelayDetected", new
                        {
                            doctorId = doc.Id,
                            doctorName = doc.Name,
                            tokenNumber = ongoingPatient.TokenNumber,
                            delayMinutes = Math.Round(delayMinutes, 1)
                        });

                        await _hubContext.Clients.All.SendAsync("QueueUpdated");
                        await _hubContext.Clients.Group(doc.Id.ToString()).SendAsync("DoctorQueueUpdated", doc.Id);
                    }
                }
            }
        }
    }
}
