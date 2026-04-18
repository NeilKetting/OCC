using System;

namespace OCC.WpfClient.Infrastructure.Messages
{
    public class GanttScrollToDateMessage
    {
        public DateTime TargetDate { get; }

        public GanttScrollToDateMessage(DateTime targetDate)
        {
            TargetDate = targetDate;
        }
    }
}
