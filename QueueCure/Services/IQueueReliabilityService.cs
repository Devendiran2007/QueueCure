using System;
using System.Threading.Tasks;

namespace QueueCure.Services
{
    public interface IQueueReliabilityService
    {
        Task<int> CalculateReliabilityScoreAsync(Guid doctorId);
        string GetReliabilityLabel(int score);
    }
}
