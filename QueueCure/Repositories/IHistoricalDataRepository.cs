using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QueueCure.Models;

namespace QueueCure.Repositories
{
    public interface IHistoricalDataRepository
    {
        Task AddHistoricalRecordAsync(HistoricalConsultation record);
        Task<IEnumerable<HistoricalConsultation>> GetHistoryByDoctorAsync(Guid doctorId);
        Task<IEnumerable<HistoricalConsultation>> GetHistoryByCategoryAsync(VisitCategory category);
        Task<IEnumerable<HistoricalConsultation>> GetAllHistoryAsync();
        Task<int> GetRecordCountAsync();
    }
}
