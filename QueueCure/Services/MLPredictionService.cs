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
    public class MLPredictionService : IMLPredictionService
    {
        private readonly IHistoricalDataRepository _historicalRepository;
        private readonly IQueueRepository _queueRepository;
        private readonly IPredictionModel _predictionModel;
        private readonly IHubContext<QueueHub> _hubContext;

        public MLPredictionService(
            IHistoricalDataRepository historicalRepository,
            IQueueRepository queueRepository,
            IPredictionModel predictionModel,
            IHubContext<QueueHub> hubContext)
        {
            _historicalRepository = historicalRepository;
            _queueRepository = queueRepository;
            _predictionModel = predictionModel;
            _hubContext = hubContext;
        }

        public async Task<PredictionResultDto> PredictWaitTimeAsync(Guid patientId)
        {
            var patient = await _queueRepository.GetPatientByIdAsync(patientId);
            if (patient == null)
            {
                throw new ArgumentException("Patient not found.");
            }

            var activePatients = (await _queueRepository.GetActivePatientsForDoctorAsync(patient.DoctorId)).ToList();
            
            // Calculate Queue Length and Tokens Ahead
            int queueLength = activePatients.Count(p => p.Status == PatientStatus.Waiting);
            
            int tokensAhead = 0;
            var waitingPatients = activePatients.Where(p => p.Status == PatientStatus.Waiting).ToList();
            var patientIndex = waitingPatients.FindIndex(p => p.Id == patientId);
            if (patientIndex >= 0)
            {
                tokensAhead = patientIndex;
            }
            else if (patient.Status == PatientStatus.InConsultation)
            {
                tokensAhead = 0;
            }

            var input = new PredictionInput
            {
                DoctorId = patient.DoctorId,
                Category = patient.Category,
                QueueLength = queueLength,
                TokensAhead = tokensAhead,
                HourOfDay = patient.CheckInTime.Hour,
                DayOfWeek = patient.CheckInTime.DayOfWeek
            };

            var historicalData = await _historicalRepository.GetAllHistoryAsync();
            var historyList = historicalData.ToList();

            // Run prediction model
            double predictedMinutes = await _predictionModel.PredictWaitTimeAsync(input, historyList);

            // Calculate Confidence based on N records for doctor + category
            int categoryRecordsCount = historyList.Count(h => h.DoctorId == patient.DoctorId && h.PatientCategory == patient.Category);
            
            // Confidence asymptotic growth curve: starts at 5% (no records), grows to ~50% (10 records), ~90% (35 records)
            int confidence = (int)Math.Round(95.0 * (1.0 - Math.Exp(-categoryRecordsCount / 15.0)) + 5.0);
            confidence = Math.Clamp(confidence, 5, 99);

            // Compute Arrival Window (e.g. Estimated Start Time +/- 5 minutes)
            DateTime estStartTime = DateTime.UtcNow.AddMinutes(predictedMinutes);
            DateTime windowStart = estStartTime.AddMinutes(-5);
            DateTime windowEnd = estStartTime.AddMinutes(5);

            return new PredictionResultDto
            {
                PredictedWaitMinutes = Math.Round(predictedMinutes, 1),
                Confidence = confidence,
                EstimatedStartTime = estStartTime,
                ArrivalWindowStart = windowStart,
                ArrivalWindowEnd = windowEnd
            };
        }

        public async Task RecordCompletedConsultationAsync(Guid patientId)
        {
            var patient = await _queueRepository.GetPatientByIdAsync(patientId);
            if (patient == null) return;

            var events = (await _queueRepository.GetEventsByPatientIdAsync(patientId)).ToList();

            // Determine Start and End consultation timestamps from events or patient model fallback
            DateTime checkInTime = patient.CheckInTime;
            DateTime startTime = patient.ConsultationStartTime 
                               ?? events.FirstOrDefault(e => e.EventType == "Started" || e.EventType == "Called")?.Timestamp 
                               ?? DateTime.UtcNow;
            
            DateTime endTime = patient.ConsultationEndTime 
                             ?? events.FirstOrDefault(e => e.EventType == "Completed")?.Timestamp 
                             ?? DateTime.UtcNow;

            double actualWait = (startTime - checkInTime).TotalMinutes;
            double actualDuration = (endTime - startTime).TotalMinutes;

            var record = new HistoricalConsultation
            {
                Id = Guid.NewGuid(),
                PatientId = patient.Id,
                DoctorId = patient.DoctorId,
                PatientCategory = patient.Category,
                QueuePositionWhenAdded = patient.QueuePositionWhenAdded,
                CheckInTime = checkInTime,
                ConsultationStartTime = startTime,
                ConsultationEndTime = endTime,
                ActualWaitTime = Math.Max(0.0, Math.Round(actualWait, 1)),
                ConsultationDuration = Math.Max(1.0, Math.Round(actualDuration, 1)),
                DayOfWeek = checkInTime.DayOfWeek,
                HourOfDay = checkInTime.Hour
            };

            await _historicalRepository.AddHistoricalRecordAsync(record);

            // Broadcast SignalR update to notify receptionist & analytics dashboards
            await _hubContext.Clients.All.SendAsync("QueueUpdated");
            await _hubContext.Clients.Group(patient.DoctorId.ToString()).SendAsync("DoctorQueueUpdated", patient.DoctorId);
        }

        public async Task<DoctorLearningProfileDto> GetDoctorProfileAsync(Guid doctorId)
        {
            var doctor = await _queueRepository.GetDoctorByIdAsync(doctorId);
            if (doctor == null)
            {
                throw new ArgumentException("Doctor not found.");
            }

            var doctorHistory = (await _historicalRepository.GetHistoryByDoctorAsync(doctorId)).ToList();
            int patientsSeen = doctorHistory.Count;

            double avgDuration = patientsSeen > 0 
                ? doctorHistory.Average(h => h.ConsultationDuration) 
                : doctor.AverageConsultationTime;

            double fastest = patientsSeen > 0 ? doctorHistory.Min(h => h.ConsultationDuration) : 0.0;
            double slowest = patientsSeen > 0 ? doctorHistory.Max(h => h.ConsultationDuration) : 0.0;

            // Calculate Historical Accuracy for this doctor
            // Compare actual wait times to simulated predicted wait times
            double accuracySum = 0;
            int accuracyCount = 0;

            foreach (var h in doctorHistory)
            {
                // Simple simulated baseline prediction: position when added * doctor baseline speed
                double simulatedPrediction = h.QueuePositionWhenAdded * avgDuration;
                double actualWait = h.ActualWaitTime;
                
                double absoluteError = Math.Abs(actualWait - simulatedPrediction);
                double accuracy = 100.0;
                
                if (actualWait > 0)
                {
                    accuracy = Math.Max(0.0, 100.0 - (absoluteError / actualWait) * 100.0);
                }
                else if (simulatedPrediction > 0)
                {
                    accuracy = 0.0; // was predicted to wait but wait was 0
                }

                accuracySum += accuracy;
                accuracyCount++;
            }

            double historicalAccuracy = accuracyCount > 0 ? accuracySum / accuracyCount : 100.0;

            return new DoctorLearningProfileDto
            {
                DoctorId = doctor.Id,
                DoctorName = doctor.Name,
                Specialty = doctor.Specialty,
                AverageConsultationDuration = Math.Round(avgDuration, 1),
                FastestConsultation = Math.Round(fastest, 1),
                SlowestConsultation = Math.Round(slowest, 1),
                PatientsSeen = patientsSeen,
                HistoricalAccuracy = Math.Round(historicalAccuracy, 1)
            };
        }
    }
}
