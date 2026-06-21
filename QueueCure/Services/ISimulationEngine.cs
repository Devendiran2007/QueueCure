using System.Threading.Tasks;
using QueueCure.Models;

namespace QueueCure.Services
{
    public interface ISimulationEngine
    {
        Task<SimulationResultDto> RunSimulationAsync(SimulationRequestDto request);
    }
}
