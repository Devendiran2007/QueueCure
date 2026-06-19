using System;
using System.Linq;
using System.Security.Claims;
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
    public class DoctorController : ControllerBase
    {
        private readonly IQueueService _queueService;
        private readonly IQueueRepository _queueRepository;

        public DoctorController(IQueueService queueService, IQueueRepository queueRepository)
        {
            _queueService = queueService;
            _queueRepository = queueRepository;
        }

        [HttpGet("list")]
        public async Task<IActionResult> GetDoctors()
        {
            var doctors = await _queueService.GetDoctorsStatusAsync();
            var patientsToday = await _queueRepository.GetAllPatientsTodayAsync();

            var result = doctors.Select(d => new
            {
                d.Id,
                d.Specialty,
                d.RoomNumber,
                d.IsAvailable,
                // Check if any patient is currently called/in consultation
                CurrentTokenNumber = patientsToday
                    .FirstOrDefault(p => p.DoctorId == d.Id && p.Status == PatientStatus.InConsultation)
                    ?.TokenNumber,
                DoctorName = d.Name,
                d.AverageConsultationTime,
                WaitingCount = patientsToday.Count(p => p.DoctorId == d.Id && p.Status == PatientStatus.Waiting)
            });
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
    }
}
