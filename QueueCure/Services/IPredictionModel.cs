using System.Collections.Generic;
using System.Threading.Tasks;
using QueueCure.Models;

namespace QueueCure.Services
{
    public interface IPredictionModel
    {
        Task<double> PredictWaitTimeAsync(PredictionInput input, IEnumerable<HistoricalConsultation> historicalData);
    }
}
