using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QueueCure.Services
{
    public interface IQueueImpactService
    {
        Task<Dictionary<Guid, double>> GetCurrentWaitTimesSnapshotAsync(Guid doctorId);
        Task RecordImpactAsync(string eventType, string eventDetail, Guid doctorId, Dictionary<Guid, double> preWaitTimes);
    }
}
