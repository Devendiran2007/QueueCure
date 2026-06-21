using System;
using System.Threading.Tasks;
using QueueCure.Models;

namespace QueueCure.Services
{
    public interface IPredictionExplanationService
    {
        Task<PredictionExplanationDto> GetWaitExplanationAsync(Guid patientId);
    }
}
