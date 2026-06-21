using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QueueCure.Repositories;
using QueueCure.Services;
using QueueCure.Models;

using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using QueueCure.Data;

namespace QueueCure.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DoctorController : ControllerBase
    {
        private readonly IQueueService _queueService;
        private readonly IQueueRepository _queueRepository;
        private readonly QueueCureDbContext _context;
        private readonly IQueueReliabilityService _reliabilityService;

        public DoctorController(
            IQueueService queueService, 
            IQueueRepository queueRepository, 
            QueueCureDbContext context,
            IQueueReliabilityService reliabilityService)
        {
            _queueService = queueService;
            _queueRepository = queueRepository;
            _context = context;
            _reliabilityService = reliabilityService;
        }

        [HttpGet("list")]
        public async Task<IActionResult> GetDoctors()
        {
            var doctors = await _queueService.GetDoctorsStatusAsync();
            var patientsToday = await _queueRepository.GetAllPatientsTodayAsync();
            
            // Get historical consult durations grouped by doctor to compute dynamic wait times
            var history = await _context.HistoricalConsultations.ToListAsync();
            var avgDurations = history
                .GroupBy(h => h.DoctorId)
                .ToDictionary(g => g.Key, g => g.Average(h => h.ConsultationDuration));

            var result = new List<object>();
            foreach (var d in doctors)
            {
                var waitingCount = patientsToday.Count(p => p.DoctorId == d.Id && p.Status == PatientStatus.Waiting);
                
                // Get average consult duration
                double avgDuration = avgDurations.TryGetValue(d.Id, out double val) ? val : d.AverageConsultationTime;

                // Calculate remaining consult time of active patient if busy
                double remainingTime = 0;
                var activePatient = patientsToday.FirstOrDefault(p => p.DoctorId == d.Id && p.Status == PatientStatus.InConsultation);
                if (activePatient != null)
                {
                    // Find when the consultation started
                    var startEvent = await _context.QueueEvents
                        .Where(e => e.PatientId == activePatient.Id && (e.EventType == "Started" || e.EventType == "Called"))
                        .OrderByDescending(e => e.Timestamp)
                        .FirstOrDefaultAsync();
                    
                    if (startEvent != null)
                    {
                        double elapsed = (DateTime.UtcNow - startEvent.Timestamp).TotalMinutes;
                        remainingTime = Math.Max(0.0, avgDuration - elapsed);
                    }
                    else
                    {
                        remainingTime = avgDuration;
                    }
                }

                double estimatedWait = (waitingCount * avgDuration) + remainingTime;
                var reliabilityScore = await _reliabilityService.CalculateReliabilityScoreAsync(d.Id);
                var reliabilityLabel = _reliabilityService.GetReliabilityLabel(reliabilityScore);

                result.Add(new
                {
                    d.Id,
                    d.Specialty,
                    d.RoomNumber,
                    d.IsAvailable,
                    CurrentTokenNumber = activePatient?.TokenNumber,
                    DoctorName = d.Name,
                    d.AverageConsultationTime,
                    WaitingCount = waitingCount,
                    EstimatedWaitMinutes = Math.Max(0.0, Math.Round(estimatedWait, 1)),
                    ReliabilityScore = reliabilityScore,
                    ReliabilityLabel = reliabilityLabel
                });
            }

            return Ok(result);
        }

        [HttpGet("my-profile")]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> GetMyProfile()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            {
                return Unauthorized(new { message = "Invalid user identification." });
            }

            var doctor = await _queueRepository.GetDoctorByUserIdAsync(userId);
            if (doctor == null)
            {
                return NotFound(new { message = "Doctor profile not found." });
            }

            var activeQueue = await _queueRepository.GetActivePatientsForDoctorAsync(doctor.Id);
            var currentCalling = activeQueue.FirstOrDefault(p => p.Status == PatientStatus.InConsultation);

            return Ok(new
            {
                doctor.Id,
                doctor.Specialty,
                doctor.RoomNumber,
                DoctorName = doctor.Name,
                doctor.AverageConsultationTime,
                CurrentTokenNumber = currentCalling?.TokenNumber
            });
        }

        [HttpPost("{doctorId}/consultation-time")]
        [Authorize(Roles = "Receptionist")]
        public async Task<IActionResult> UpdateConsultationTime(Guid doctorId, [FromQuery] int minutes)
        {
            if (minutes <= 0) return BadRequest(new { message = "Minutes must be positive." });
            await _queueService.UpdateDoctorConsultationTimeAsync(doctorId, minutes);
            return Ok(new { message = "Doctor consultation time updated successfully.", doctorId, averageConsultationTime = minutes });
        }

        [HttpPost("toggle-availability")]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> ToggleAvailability()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            {
                return Unauthorized(new { message = "Invalid user identification." });
            }

            var doctor = await _queueRepository.GetDoctorByUserIdAsync(userId);
            if (doctor == null)
            {
                return NotFound(new { message = "Doctor profile not found." });
            }

            var updatedDoctor = await _queueService.ToggleDoctorAvailabilityAsync(doctor.Id);
            if (updatedDoctor == null)
            {
                return NotFound(new { message = "Could not update availability." });
            }

            return Ok(new { isAvailable = updatedDoctor.IsAvailable });
        }

        [HttpGet("analytics")]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> GetAnalytics()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            {
                return Unauthorized(new { message = "Invalid user identification." });
            }

            var doctor = await _queueRepository.GetDoctorByUserIdAsync(userId);
            if (doctor == null)
            {
                return NotFound(new { message = "Doctor profile not found." });
            }

            var doctorId = doctor.Id;
            var today = DateTime.UtcNow.Date;

            // Fetch all patients for this doctor
            var patients = await _context.Patients
                .Where(p => p.DoctorId == doctorId)
                .ToListAsync();

            // Today's patients
            var todayPatients = patients.Where(p => p.CheckInTime >= today).ToList();

            // 1. Current Patient
            var currentPatient = todayPatients.FirstOrDefault(p => p.Status == PatientStatus.InConsultation);

            // 2. Patients Waiting
            var waitingCount = todayPatients.Count(p => p.Status == PatientStatus.Waiting);

            // 3. Patients Served Today
            var servedCount = todayPatients.Count(p => p.Status == PatientStatus.Completed);

            // Fetch all events for today's patients of this doctor
            var todayPatientIds = todayPatients.Select(p => p.Id).ToList();
            var todayEvents = await _context.QueueEvents
                .Where(e => todayPatientIds.Contains(e.PatientId))
                .ToListAsync();

            // 4. Average Wait Time (for today's patients who finished waiting, i.e. status InConsultation, Completed, Skipped)
            var waitTimes = new List<double>();
            foreach (var p in todayPatients)
            {
                if (p.Status == PatientStatus.Waiting) continue;

                var events = todayEvents.Where(e => e.PatientId == p.Id).ToList();
                var endWaitEvent = events.FirstOrDefault(e => e.EventType == "Called") 
                                   ?? events.FirstOrDefault(e => e.EventType == "Started");

                if (endWaitEvent != null)
                {
                    var waitDuration = (endWaitEvent.Timestamp - p.CheckInTime).TotalMinutes;
                    waitTimes.Add(Math.Max(0, waitDuration));
                }
            }
            double avgWaitTime = waitTimes.Any() ? Math.Round(waitTimes.Average(), 1) : 0.0;

            // 5. Average Consultation Time (for today's completed patients)
            var consultTimes = new List<double>();
            foreach (var p in todayPatients.Where(p => p.Status == PatientStatus.Completed))
            {
                var events = todayEvents.Where(e => e.PatientId == p.Id).ToList();
                var started = events.FirstOrDefault(e => e.EventType == "Started") 
                              ?? events.FirstOrDefault(e => e.EventType == "Called");
                var completed = events.FirstOrDefault(e => e.EventType == "Completed");

                if (started != null && completed != null && completed.Timestamp > started.Timestamp)
                {
                    consultTimes.Add((completed.Timestamp - started.Timestamp).TotalMinutes);
                }
            }
            double avgConsultTime = consultTimes.Any() ? Math.Round(consultTimes.Average(), 1) : (double)doctor.AverageConsultationTime;

            // 6. Queue Health Score
            double healthScore = 100;
            healthScore -= (waitingCount * 5); // Deduct 5 points per waiting patient
            if (avgWaitTime > 0)
            {
                healthScore -= (avgWaitTime * 1.5); // Deduct 1.5 points per minute of avg wait time
            }
            int emergencyWaitingCount = todayPatients.Count(p => p.Status == PatientStatus.Waiting && p.IsEmergency);
            healthScore -= (emergencyWaitingCount * 15); // Deduct 15 points per waiting emergency patient

            int queueHealthScore = (int)Math.Max(0, Math.Min(100, Math.Round(healthScore)));

            // 7. Peak Hours (aggregate check-ins grouped by hour of check-in local time for all time)
            var peakHours = new int[24];
            foreach (var p in patients)
            {
                var localHour = p.CheckInTime.ToLocalTime().Hour;
                if (localHour >= 0 && localHour < 24)
                {
                    peakHours[localHour]++;
                }
            }

            // 8. Daily Patient Volume (last 7 days including today)
            var dailyVolume = new List<object>();
            for (int i = 6; i >= 0; i--)
            {
                var date = DateTime.Today.AddDays(-i);
                var count = patients.Count(p => p.CheckInTime.ToLocalTime().Date == date);
                dailyVolume.Add(new
                {
                    date = date.ToString("yyyy-MM-dd"),
                    dayOfWeek = date.ToString("ddd"),
                    count = count
                });
            }

            // 9. Consultation Trends
            // Daily averages (last 7 days)
            var dailyConsultationTrends = new List<object>();
            var allPatientIds = patients.Select(p => p.Id).ToList();
            var allEvents = await _context.QueueEvents
                .Where(e => allPatientIds.Contains(e.PatientId))
                .ToListAsync();

            for (int i = 6; i >= 0; i--)
            {
                var date = DateTime.Today.AddDays(-i);
                var completedOnDay = patients.Where(p => p.Status == PatientStatus.Completed && 
                    allEvents.Any(e => e.PatientId == p.Id && e.EventType == "Completed" && e.Timestamp.ToLocalTime().Date == date)).ToList();

                var dayDurations = new List<double>();
                foreach (var p in completedOnDay)
                {
                    var pevents = allEvents.Where(e => e.PatientId == p.Id).ToList();
                    var started = pevents.FirstOrDefault(e => e.EventType == "Started") 
                                  ?? pevents.FirstOrDefault(e => e.EventType == "Called");
                    var completed = pevents.FirstOrDefault(e => e.EventType == "Completed");

                    if (started != null && completed != null && completed.Timestamp > started.Timestamp)
                    {
                        dayDurations.Add((completed.Timestamp - started.Timestamp).TotalMinutes);
                    }
                }

                dailyConsultationTrends.Add(new
                {
                    date = date.ToString("yyyy-MM-dd"),
                    dayOfWeek = date.ToString("ddd"),
                    averageDuration = dayDurations.Any() ? Math.Round(dayDurations.Average(), 1) : 0.0
                });
            }

            // Category averages
            var categoryAverages = new Dictionary<string, double>();
            foreach (VisitCategory cat in Enum.GetValues(typeof(VisitCategory)))
            {
                var completedInCat = patients.Where(p => p.Category == cat && p.Status == PatientStatus.Completed).ToList();
                var catDurations = new List<double>();
                foreach (var p in completedInCat)
                {
                    var pevents = allEvents.Where(e => e.PatientId == p.Id).ToList();
                    var started = pevents.FirstOrDefault(e => e.EventType == "Started") 
                                  ?? pevents.FirstOrDefault(e => e.EventType == "Called");
                    var completed = pevents.FirstOrDefault(e => e.EventType == "Completed");

                    if (started != null && completed != null && completed.Timestamp > started.Timestamp)
                    {
                        catDurations.Add((completed.Timestamp - started.Timestamp).TotalMinutes);
                    }
                }

                categoryAverages[cat.ToString()] = catDurations.Any() ? Math.Round(catDurations.Average(), 1) : cat switch
                {
                    VisitCategory.Fever => 8.0,
                    VisitCategory.GeneralCheckup => 12.0,
                    VisitCategory.DiabetesReview => 15.0,
                    VisitCategory.CardiologyConsultation => 20.0,
                    VisitCategory.FollowUp => 5.0,
                    _ => 10.0
                };
            }

            // --- QUEUE FORECASTING LOGIC ---
            // 1. Predict Expected Patients Tomorrow
            var tomorrowDay = DateTime.Today.AddDays(1).DayOfWeek;
            var patientsGroupedByDate = patients
                .GroupBy(p => p.CheckInTime.ToLocalTime().Date)
                .Select(g => new { Date = g.Key, DayOfWeek = g.Key.DayOfWeek, Count = g.Count() })
                .ToList();

            double overallDailyAvg = patientsGroupedByDate.Any() 
                ? patientsGroupedByDate.Average(g => g.Count) 
                : 10.0;

            var sameDayOfWeekGroups = patientsGroupedByDate
                .Where(g => g.DayOfWeek == tomorrowDay)
                .ToList();

            double avgSameDayOfWeek = sameDayOfWeekGroups.Any()
                ? sameDayOfWeekGroups.Average(g => g.Count)
                : overallDailyAvg;

            int expectedPatientsTomorrow = (int)Math.Round((avgSameDayOfWeek * 0.7) + (overallDailyAvg * 0.3));
            if (expectedPatientsTomorrow <= 0)
            {
                expectedPatientsTomorrow = 10;
            }

            // 2. Predict Expected Peak Hour Range
            int peakHourIndex = 10;
            int maxHourCount = 0;
            for (int h = 0; h < 24; h++)
            {
                if (peakHours[h] > maxHourCount)
                {
                    maxHourCount = peakHours[h];
                    peakHourIndex = h;
                }
            }
            string ampmStart = peakHourIndex >= 12 ? "PM" : "AM";
            int displayHourStart = peakHourIndex % 12 == 0 ? 12 : peakHourIndex % 12;
            int nextHour = (peakHourIndex + 1) % 24;
            string ampmEnd = nextHour >= 12 ? "PM" : "AM";
            int displayHourEnd = nextHour % 12 == 0 ? 12 : nextHour % 12;
            string expectedPeakHour = $"{displayHourStart}:00 {ampmStart} - {displayHourEnd}:00 {ampmEnd}";

            // 3. Expected Waiting Time Tomorrow
            var allWaitTimes = new List<double>();
            foreach (var p in patients)
            {
                if (p.Status == PatientStatus.Waiting) continue;

                var pevents = allEvents.Where(e => e.PatientId == p.Id).ToList();
                var endWaitEvent = pevents.FirstOrDefault(e => e.EventType == "Called") 
                                   ?? pevents.FirstOrDefault(e => e.EventType == "Started");

                if (endWaitEvent != null)
                {
                    var waitDuration = (endWaitEvent.Timestamp - p.CheckInTime).TotalMinutes;
                    allWaitTimes.Add(Math.Max(0, waitDuration));
                }
            }
            double expectedWaitingTime = allWaitTimes.Any() 
                ? Math.Round(allWaitTimes.Average(), 1) 
                : 15.0;

            return Ok(new
            {
                doctorName = doctor.Name,
                specialization = doctor.Specialization,
                roomNumber = doctor.RoomNumber,
                currentPatient = currentPatient != null ? new { name = currentPatient.Name, tokenNumber = currentPatient.TokenNumber } : null,
                patientsWaiting = waitingCount,
                patientsServed = servedCount,
                averageWaitTime = avgWaitTime,
                averageConsultationTime = avgConsultTime,
                queueHealthScore,
                peakHours,
                dailyVolume,
                consultationTrends = new
                {
                    dailyAverages = dailyConsultationTrends,
                    categoryAverages
                },
                forecast = new
                {
                    expectedPatientsTomorrow,
                    expectedPeakHour,
                    expectedWaitingTime
                }
            });
        }
    }
}
