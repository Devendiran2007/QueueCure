using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QueueCure.Data;
using QueueCure.Repositories;

namespace QueueCure.Services
{
    public class QueueReliabilityService : IQueueReliabilityService
    {
        private readonly QueueCureDbContext _context;
        private readonly IQueueRepository _queueRepository;
        private readonly IMLPredictionService _mlPredictionService;

        public QueueReliabilityService(
            QueueCureDbContext context,
            IQueueRepository queueRepository,
            IMLPredictionService mlPredictionService)
        {
            _context = context;
            _queueRepository = queueRepository;
            _mlPredictionService = mlPredictionService;
        }

        public async Task<int> CalculateReliabilityScoreAsync(Guid doctorId)
        {
            // 1. Historical prediction accuracy (0-100)
            double historicalAccuracy = 100.0;
            try
            {
                var profile = await _mlPredictionService.GetDoctorProfileAsync(doctorId);
                historicalAccuracy = profile.HistoricalAccuracy;
            }
            catch
            {
                // Fallback if profile not found
            }

            double score = historicalAccuracy;

            // Get active queue details
            var activeQueue = (await _queueRepository.GetActivePatientsForDoctorAsync(doctorId)).ToList();
            int waitingCount = activeQueue.Count(p => p.Status == Models.PatientStatus.Waiting);
            int emergencyCount = activeQueue.Count(p => p.Status == Models.PatientStatus.Waiting && p.IsEmergency);

            // 2. Emergency interruptions penalty
            score -= (emergencyCount * 15.0);

            // 3. Queue congestion penalty
            if (waitingCount > 4)
            {
                score -= ((waitingCount - 4) * 3.0);
            }

            // 4. Number of active patients penalty
            if (activeQueue.Count > 6)
            {
                score -= ((activeQueue.Count - 6) * 1.5);
            }

            // 5. Recent delay events penalty (last 2 hours)
            var twoHoursAgo = DateTime.UtcNow.AddHours(-2);
            int recentDelays = await _context.DelayEvents
                .CountAsync(d => d.DoctorId == doctorId && d.Timestamp >= twoHoursAgo);
            
            score -= (recentDelays * 10.0);

            // Clamp score between 0 and 100
            int finalScore = (int)Math.Clamp(Math.Round(score), 0, 100);
            return finalScore;
        }

        public string GetReliabilityLabel(int score)
        {
            if (score >= 90) return "Excellent";
            if (score >= 75) return "Good";
            if (score >= 50) return "Moderate";
            return "Unstable";
        }
    }
}
