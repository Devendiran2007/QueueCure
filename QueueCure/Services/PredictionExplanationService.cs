using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using QueueCure.Models;
using QueueCure.Repositories;

namespace QueueCure.Services
{
    public class PredictionExplanationService : IPredictionExplanationService
    {
        private readonly IQueueRepository _queueRepository;
        private readonly IHistoricalDataRepository _historicalRepository;
        private readonly IMLPredictionService _mlPredictionService;

        public PredictionExplanationService(
            IQueueRepository queueRepository,
            IHistoricalDataRepository historicalRepository,
            IMLPredictionService mlPredictionService)
        {
            _queueRepository = queueRepository;
            _historicalRepository = historicalRepository;
            _mlPredictionService = mlPredictionService;
        }

        public async Task<PredictionExplanationDto> GetWaitExplanationAsync(Guid patientId)
        {
            var patient = await _queueRepository.GetPatientByIdAsync(patientId);
            if (patient == null)
            {
                throw new ArgumentException("Patient not found.");
            }

            var activeQueue = (await _queueRepository.GetActivePatientsForDoctorAsync(patient.DoctorId)).ToList();
            var prediction = await _mlPredictionService.PredictWaitTimeAsync(patientId);
            var explanations = new List<string>();

            // 1. Queue Position & Tokens Ahead
            int tokensAhead = 0;
            var waitingPatients = activeQueue.Where(p => p.Status == PatientStatus.Waiting).ToList();
            var patientIndex = waitingPatients.FindIndex(p => p.Id == patientId);

            if (patient.Status == PatientStatus.InConsultation)
            {
                explanations.Add("You are currently inside the consultation room being served.");
            }
            else if (patientIndex >= 0)
            {
                tokensAhead = patientIndex;
                if (tokensAhead == 0)
                {
                    explanations.Add("You are the next patient to be called. Please stand by.");
                }
                else
                {
                    explanations.Add($"{tokensAhead} patient{(tokensAhead > 1 ? "s" : "")} ahead of you in the queue.");
                }
            }
            else
            {
                explanations.Add("Your token is currently not in the active waiting queue.");
            }

            // 2. Doctor Average Consultation Time
            double categoryAvg = await _queueRepository.GetAverageConsultationDurationAsync(patient.Category);
            string categoryDisplay = patient.Category.ToString().Replace("Consultation", " Consultation").Replace("Review", " Review").Trim();
            explanations.Add($"Doctor average consultation time: {Math.Round(categoryAvg, 1)} mins for {categoryDisplay}.");

            // 3. Emergencies or Restorations Ahead
            if (patientIndex > 0)
            {
                var aheadList = waitingPatients.Take(patientIndex).ToList();
                int emergencyAheadCount = aheadList.Count(p => p.IsEmergency);
                int restoredAheadCount = aheadList.Count(p => p.IsRestored);

                if (emergencyAheadCount > 0)
                {
                    explanations.Add($"{emergencyAheadCount} emergency patient{(emergencyAheadCount > 1 ? "s" : "")} ahead in queue (prioritized).");
                }
                if (restoredAheadCount > 0)
                {
                    explanations.Add($"{restoredAheadCount} skipped patient{(restoredAheadCount > 1 ? "s" : "")} restored ahead of you.");
                }
            }

            // 4. Smart Delay: Is doctor currently running behind?
            var ongoingPatient = activeQueue.FirstOrDefault(p => p.Status == PatientStatus.InConsultation);
            if (ongoingPatient != null)
            {
                double ongoingEstDuration = await _queueRepository.GetAverageConsultationDurationAsync(ongoingPatient.Category);
                var startEvent = (await _queueRepository.GetEventsByPatientIdAsync(ongoingPatient.Id))
                    .Where(e => e.EventType == "Started" || e.EventType == "Called")
                    .OrderByDescending(e => e.Timestamp)
                    .FirstOrDefault();

                if (startEvent != null)
                {
                    double elapsed = (DateTime.UtcNow - startEvent.Timestamp).TotalMinutes;
                    if (elapsed > ongoingEstDuration)
                    {
                        double delay = Math.Round(elapsed - ongoingEstDuration);
                        if (delay >= 2.0)
                        {
                            explanations.Add($"Current doctor is running behind schedule by +{delay} minutes on active patient.");
                        }
                    }
                }
            }

            // 5. Congestion Factors (Day of week / Hour of day)
            var historyList = (await _historicalRepository.GetAllHistoryAsync()).ToList();
            if (historyList.Any())
            {
                // Day of Week multiplier
                var dayRecords = historyList.Where(h => h.DayOfWeek == patient.CheckInTime.DayOfWeek).ToList();
                double overallAvgWait = historyList.Average(h => h.ActualWaitTime);
                if (dayRecords.Any() && overallAvgWait > 0)
                {
                    double dayAvgWait = dayRecords.Average(h => h.ActualWaitTime);
                    double dayMultiplier = dayAvgWait / overallAvgWait;
                    if (dayMultiplier > 1.1)
                    {
                        explanations.Add($"Historical {patient.CheckInTime.DayOfWeek} traffic increase (+{(int)Math.Round((dayMultiplier - 1.0) * 100)}% congestion).");
                    }
                }

                // Hour of Day multiplier
                var hourRecords = historyList.Where(h => h.HourOfDay == patient.CheckInTime.Hour).ToList();
                if (hourRecords.Any() && overallAvgWait > 0)
                {
                    double hourAvgWait = hourRecords.Average(h => h.ActualWaitTime);
                    double hourMultiplier = hourAvgWait / overallAvgWait;
                    if (hourMultiplier > 1.1)
                    {
                        explanations.Add($"Peak hour traffic congestion (+{(int)Math.Round((hourMultiplier - 1.0) * 100)}% wait adjustment).");
                    }
                }
            }

            return new PredictionExplanationDto
            {
                EstimatedWaitMinutes = prediction.PredictedWaitMinutes,
                Explanations = explanations
            };
        }
    }
}
