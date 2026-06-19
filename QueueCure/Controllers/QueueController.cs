using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QueueCure.Repositories;
using QueueCure.Services;
using QueueCure.Models;

namespace QueueCure.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class QueueController : ControllerBase
    {
        private readonly IQueueService _queueService;
        private readonly IQueueRepository _queueRepository;

        public QueueController(IQueueService queueService, IQueueRepository queueRepository)
        {
            _queueService = queueService;
            _queueRepository = queueRepository;
        }

        [HttpPost("generate")]
        [Authorize(Roles = "Receptionist")]
        public async Task<IActionResult> GenerateToken([FromBody] GenerateTokenDto model)
        {
            try
            {
                var patient = await _queueService.GenerateTokenAsync(model.PatientName, model.PatientPhone, model.DoctorId, model.Category, model.IsEmergency);
                var waitingCount = await _queueRepository.GetWaitingCountBeforePatientAsync(patient);
                var waitMinutes = await _queueService.CalculateEstimatedWaitTimeAsync(patient);
                var estStart = DateTime.UtcNow.AddMinutes(waitMinutes);

                return Ok(new
                {
                    patient.Id,
                    patient.TokenNumber,
                    name = patient.Name,
                    patient.PhoneNumber,
                    patient.CheckInTime,
                    patient.Status,
                    category = patient.Category.ToString(),
                    isEmergency = patient.IsEmergency,
                    estimatedWaitMinutes = waitMinutes,
                    estimatedStartTime = estStart,
                    arrivalWindowStart = estStart.AddMinutes(-5),
                    arrivalWindowEnd = estStart.AddMinutes(5)
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred", details = ex.Message });
            }
        }

        [HttpPost("call-next/{doctorId}")]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> CallNext(Guid doctorId)
        {
            try
            {
                var patient = await _queueService.CallNextTokenAsync(doctorId);
                if (patient == null)
                {
                    return NotFound(new { message = "No waiting patients in queue." });
                }
                return Ok(patient);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred", details = ex.Message });
            }
        }

        [HttpPost("start/{tokenId}")]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> StartConsultation(Guid tokenId)
        {
            var patient = await _queueService.StartConsultationAsync(tokenId);
            if (patient == null)
            {
                return BadRequest(new { message = "Consultation could not be started. Check if patient is currently called." });
            }
            return Ok(patient);
        }

        [HttpPost("complete/{tokenId}")]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> CompleteConsultation(Guid tokenId)
        {
            var patient = await _queueService.CompleteConsultationAsync(tokenId);
            if (patient == null)
            {
                return BadRequest(new { message = "Consultation could not be completed." });
            }
            return Ok(patient);
        }

        [HttpPost("noshow/{tokenId}")]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> MarkNoShow(Guid tokenId)
        {
            var patient = await _queueService.SkipPatientAsync(tokenId);
            if (patient == null)
            {
                return BadRequest(new { message = "Patient could not be marked as skipped." });
            }
            return Ok(patient);
        }

        [HttpPost("cancel/{tokenId}")]
        [Authorize(Roles = "Receptionist,Doctor")]
        public async Task<IActionResult> CancelToken(Guid tokenId)
        {
            var patient = await _queueService.SkipPatientAsync(tokenId);
            if (patient == null)
            {
                return BadRequest(new { message = "Token could not be cancelled." });
            }
            return Ok(patient);
        }

        [HttpGet("doctor/{doctorId}")]
        [Authorize]
        public async Task<IActionResult> GetDoctorQueue(Guid doctorId)
        {
            var queue = await _queueService.GetActiveQueueForDoctorAsync(doctorId);
            return Ok(queue);
        }

        [HttpGet("status/{tokenNumber}")]
        public async Task<IActionResult> GetTokenStatus(string tokenNumber)
        {
            var patient = await _queueService.GetPatientDetailsAsync(tokenNumber);
            if (patient == null)
            {
                return NotFound(new { message = "Token not found." });
            }

            var doctor = await _queueRepository.GetDoctorByIdAsync(patient.DoctorId);
            var waitingCount = await _queueRepository.GetWaitingCountBeforePatientAsync(patient);

            // Fetch the token currently being served by this doctor
            var patientsToday = await _queueRepository.GetAllPatientsTodayAsync();
            var currentServing = patientsToday
                .FirstOrDefault(p => p.DoctorId == patient.DoctorId && p.Status == PatientStatus.InConsultation)
                ?.TokenNumber;

            var waitMinutes = await _queueService.CalculateEstimatedWaitTimeAsync(patient);
            var estStart = DateTime.UtcNow.AddMinutes(waitMinutes);

            return Ok(new
            {
                id = patient.Id,
                patient.TokenNumber,
                patientName = patient.Name,
                status = (int)patient.Status,
                statusText = patient.Status.ToString(),
                category = patient.Category.ToString(),
                isEmergency = patient.IsEmergency,
                doctorName = doctor?.Name ?? "Doctor",
                roomNumber = doctor?.RoomNumber ?? "N/A",
                estimatedWaitMinutes = waitMinutes,
                sequenceNumber = waitingCount + 1,
                tokensAhead = waitingCount,
                currentTokenBeingServed = currentServing ?? "None (Idle)",
                estimatedStartTime = estStart,
                arrivalWindowStart = estStart.AddMinutes(-5),
                arrivalWindowEnd = estStart.AddMinutes(5)
            });
        }

        [HttpGet("tv")]
        public async Task<IActionResult> GetTVDisplay()
        {
            var data = await _queueService.GetTVDashboardDataAsync();
            return Ok(data);
        }

        [HttpPost("settings")]
        [Authorize(Roles = "Receptionist")]
        public async Task<IActionResult> UpdateSettings([FromQuery] int minutes)
        {
            if (minutes <= 0) return BadRequest(new { message = "Minutes must be positive." });
            await _queueService.UpdateGlobalSettingsAsync(minutes);
            return Ok(new { message = "Global settings updated successfully.", averageConsultationTime = minutes });
        }

        [HttpPost("skip/{tokenId}")]
        [Authorize(Roles = "Receptionist,Doctor")]
        public async Task<IActionResult> SkipPatient(Guid tokenId)
        {
            var patient = await _queueService.SkipPatientAsync(tokenId);
            if (patient == null)
            {
                return BadRequest(new { message = "Patient could not be marked as skipped." });
            }
            return Ok(patient);
        }

        [HttpGet("patient/{patientId}/timeline")]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> GetPatientTimeline(Guid patientId)
        {
            var patient = await _queueRepository.GetPatientByIdAsync(patientId);
            if (patient == null) return NotFound(new { message = "Patient not found." });

            var events = await _queueRepository.GetEventsByPatientIdAsync(patientId);

            var checkIn = events.FirstOrDefault(e => e.EventType == "CheckIn")?.Timestamp;
            var called = events.FirstOrDefault(e => e.EventType == "Called")?.Timestamp;
            var started = events.FirstOrDefault(e => e.EventType == "Started")?.Timestamp;
            var completed = events.FirstOrDefault(e => e.EventType == "Completed")?.Timestamp;
            var skipped = events.FirstOrDefault(e => e.EventType == "Skipped")?.Timestamp;

            double waitingMinutes = 0;
            if (checkIn.HasValue)
            {
                var endWait = called ?? started ?? DateTime.UtcNow;
                waitingMinutes = Math.Round((endWait - checkIn.Value).TotalMinutes, 1);
            }

            double consultMinutes = 0;
            if (started.HasValue)
            {
                var endConsult = completed ?? skipped ?? DateTime.UtcNow;
                consultMinutes = Math.Round((endConsult - started.Value).TotalMinutes, 1);
            }

            return Ok(new
            {
                patientId = patient.Id,
                patientName = patient.Name,
                tokenNumber = patient.TokenNumber,
                checkInTime = checkIn,
                calledTime = called,
                startedTime = started,
                completedTime = completed,
                skippedTime = skipped,
                waitingDurationMinutes = waitingMinutes,
                consultationDurationMinutes = consultMinutes
            });
        }

        [HttpGet("history/averages")]
        [Authorize(Roles = "Receptionist,Doctor")]
        public async Task<IActionResult> GetHistoryAverages()
        {
            var averages = new System.Collections.Generic.Dictionary<string, double>();
            foreach (VisitCategory cat in System.Enum.GetValues(typeof(VisitCategory)))
            {
                averages[cat.ToString()] = await _queueRepository.GetAverageConsultationDurationAsync(cat);
            }
            return Ok(averages);
        }

        [HttpPost("mark-emergency/{patientId}")]
        [Authorize(Roles = "Receptionist,Doctor")]
        public async Task<IActionResult> MarkEmergency(Guid patientId)
        {
            var patient = await _queueService.MarkEmergencyAsync(patientId);
            if (patient == null)
            {
                return NotFound(new { message = "Patient not found." });
            }
            return Ok(patient);
        }
    }

    public class GenerateTokenDto
    {
        public string PatientName { get; set; } = string.Empty;
        public string PatientPhone { get; set; } = string.Empty;
        public Guid DoctorId { get; set; }
        public VisitCategory Category { get; set; } = VisitCategory.GeneralCheckup;
        public bool IsEmergency { get; set; } = false;
    }
}
