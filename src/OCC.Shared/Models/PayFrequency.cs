namespace OCC.Shared.Models
{
    /// <summary>
    /// Pay frequency of a wage run or employee pay schedule.
    /// </summary>
    public enum PayFrequency
    {
        /// <summary> Weekly pay cycle (e.g. Cape Town or specific weekly runs). </summary>
        Weekly = 0,

        /// <summary> Fortnightly pay cycle (e.g. Johannesburg standard cycle). </summary>
        Fortnightly = 1,

        /// <summary> Monthly pay cycle (Salary earners). </summary>
        Monthly = 2
    }
}
