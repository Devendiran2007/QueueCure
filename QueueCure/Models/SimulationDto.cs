using System;

namespace QueueCure.Models
{
    public class SimulationRequestDto
    {
        public string Scenario { get; set; } = string.Empty; // "A", "B", "C", "D"
        public Guid? DoctorId { get; set; }
    }

    public class SimulationResultDto
    {
        public double RealAvgWaitTime { get; set; }
        public int RealQueueLength { get; set; }
        public int RealPredictedDelaysCount { get; set; }
        public double RealAvgDoctorUtilization { get; set; }

        public double SimulatedAvgWaitTime { get; set; }
        public int SimulatedQueueLength { get; set; }
        public int SimulatedPredictedDelaysCount { get; set; }
        public double SimulatedAvgDoctorUtilization { get; set; }
    }
}
