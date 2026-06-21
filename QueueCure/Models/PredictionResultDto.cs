using System;

namespace QueueCure.Models
{
    public class PredictionResultDto
    {
        public double PredictedWaitMinutes { get; set; }
        public int Confidence { get; set; }
        public DateTime EstimatedStartTime { get; set; }
        public DateTime ArrivalWindowStart { get; set; }
        public DateTime ArrivalWindowEnd { get; set; }
    }
}
