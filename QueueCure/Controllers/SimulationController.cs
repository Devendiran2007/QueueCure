using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QueueCure.Models;
using QueueCure.Services;

namespace QueueCure.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Receptionist")]
    public class SimulationController : ControllerBase
    {
        private readonly ISimulationEngine _simulationEngine;

        public SimulationController(ISimulationEngine simulationEngine)
        {
            _simulationEngine = simulationEngine;
        }

        [HttpPost("run")]
        public async Task<IActionResult> RunSimulation([FromBody] SimulationRequestDto model)
        {
            try
            {
                var result = await _simulationEngine.RunSimulationAsync(model);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred during simulation.", details = ex.Message });
            }
        }
    }
}
