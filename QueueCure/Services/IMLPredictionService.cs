using System;
using System.Threading.Tasks;
using QueueCure.Models;

namespace QueueCure.Services
{
    public interface IMLPredictionService
    {
        Task<PredictionResultDto> PredictWaitTimeAsync(Guid patientId);
        Task RecordCompletedConsultationAsync(Guid patientId);
        Task<DoctorLearningProfileDto> GetDoctorProfileAsync(Guid doctorId);
    }
}
