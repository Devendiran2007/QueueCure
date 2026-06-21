using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using QueueCure.Models;

namespace QueueCure.Services
{
    public class StatisticalPredictionModel : IPredictionModel
    {
        public Task<double> PredictWaitTimeAsync(PredictionInput input, IEnumerable<HistoricalConsultation> historicalData)
        {
            var historyList = historicalData.ToList();
            
            // 1. Calculate Doctor's Category Average Speed (or overall doctor speed/global category speed fallback)
            double avgConsultDuration = 10.0; // absolute fallback
            
            var doctorHistory = historyList.Where(h => h.DoctorId == input.DoctorId).ToList();
            if (doctorHistory.Any())
            {
                var catHistory = doctorHistory.Where(h => h.PatientCategory == input.Category).ToList();
                if (catHistory.Any())
                {
                    avgConsultDuration = catHistory.Average(h => h.ConsultationDuration);
                }
                else
                {
                    avgConsultDuration = doctorHistory.Average(h => h.ConsultationDuration);
                }
            }
            else
            {
                var globalCatHistory = historyList.Where(h => h.PatientCategory == input.Category).ToList();
                if (globalCatHistory.Any())
                {
                    avgConsultDuration = globalCatHistory.Average(h => h.ConsultationDuration);
                }
                else
                {
                    // Fallback to static category baselines if zero history
                    avgConsultDuration = input.Category switch
                    {
                        VisitCategory.Fever => 8.0,
                        VisitCategory.GeneralCheckup => 12.0,
                        VisitCategory.DiabetesReview => 15.0,
                        VisitCategory.CardiologyConsultation => 20.0,
                        VisitCategory.FollowUp => 5.0,
                        VisitCategory.OrthopedicConsultation => 18.0,
                        VisitCategory.PediatricConsultation => 15.0,
                        _ => 10.0
                    };
                }
            }

            // 2. Base wait time is (TokensAhead * expected consult duration)
            // If tokens ahead is 0 but there are patients waiting (QueueLength > 0), the current patient is InConsultation.
            // Let's assume the wait time is at least (TokensAhead + 0.5) * avgConsultDuration if the doctor is busy.
            double multiplier = input.TokensAhead;
            if (multiplier == 0 && input.QueueLength > 0)
            {
                multiplier = 0.5; // assume the current session is about halfway done
            }
            
            double baseWaitTime = multiplier * avgConsultDuration;

            // 3. Apply Day of Week Congestion Factor
            double dayMultiplier = 1.0;
            if (historyList.Any())
            {
                var dayRecords = historyList.Where(h => h.DayOfWeek == input.DayOfWeek).ToList();
                double overallAvgWait = historyList.Average(h => h.ActualWaitTime);
                
                if (dayRecords.Any() && overallAvgWait > 0)
                {
                    double dayAvgWait = dayRecords.Average(h => h.ActualWaitTime);
                    dayMultiplier = dayAvgWait / overallAvgWait;
                    // Cap multiplier to avoid wild swings
                    dayMultiplier = Math.Clamp(dayMultiplier, 0.5, 2.0);
                }
            }

            // 4. Apply Hour of Day Congestion Factor
            double hourMultiplier = 1.0;
            if (historyList.Any())
            {
                var hourRecords = historyList.Where(h => h.HourOfDay == input.HourOfDay).ToList();
                double overallAvgWait = historyList.Average(h => h.ActualWaitTime);
                
                if (hourRecords.Any() && overallAvgWait > 0)
                {
                    double hourAvgWait = hourRecords.Average(h => h.ActualWaitTime);
                    hourMultiplier = hourAvgWait / overallAvgWait;
                    // Cap multiplier to avoid wild swings
                    hourMultiplier = Math.Clamp(hourMultiplier, 0.5, 2.0);
                }
            }

            // Calculate final wait time
            double predictedWait = baseWaitTime * dayMultiplier * hourMultiplier;

            // Ensure wait time is non-negative and rounded to nearest minute (minimum 1 minute if there are tokens ahead)
            if (input.TokensAhead > 0 && predictedWait < 1.0)
            {
                predictedWait = 1.0;
            }
            
            return Task.FromResult(Math.Max(0.0, Math.Round(predictedWait, 1)));
        }
    }
}
