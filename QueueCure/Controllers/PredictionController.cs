using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QueueCure.Services;

namespace QueueCure.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PredictionController : ControllerBase
    {
        private readonly IMLPredictionService _mlPredictionService;

        public PredictionController(IMLPredictionService mlPredictionService)
        {
            _mlPredictionService = mlPredictionService;
        }

        [HttpGet("patient/{patientId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPatientPrediction(Guid patientId)
        {
            try
            {
                var result = await _mlPredictionService.PredictWaitTimeAsync(patientId);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred", details = ex.Message });
            }
        }

        [HttpGet("doctor/{doctorId}/profile")]
        public async Task<IActionResult> GetDoctorProfile(Guid doctorId)
        {
            try
            {
                var profile = await _mlPredictionService.GetDoctorProfileAsync(doctorId);
                return Ok(profile);
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred", details = ex.Message });
            }
        }
    }
}
