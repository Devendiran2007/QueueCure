using System.Threading.Tasks;

namespace QueueCure.Services
{
    public interface IDelayDetectionService
    {
        Task CheckActiveConsultationsAsync();
    }
}
