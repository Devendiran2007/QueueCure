using System.Collections.Generic;

namespace QueueCure.Models
{
    public class PredictionExplanationDto
    {
        public double EstimatedWaitMinutes { get; set; }
        public List<string> Explanations { get; set; } = new();
    }
}
