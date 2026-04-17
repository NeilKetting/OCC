using System;

namespace OCC.Shared.Models
{
    /// <summary>
    /// Represents a record of safety training or certification completed by an employee.
    /// Used to track compliance with HSEQ requirements (e.g., Working at Heights, First Aid).
    /// </summary>
    /// <remarks>
    /// <b>Where:</b> Persisted in the <c>HseqTrainingRecords</c> table.
    /// <b>How:</b> Linked to an <see cref="Employee"/>. The system can use <see cref="ValidUntil"/> and <see cref="ExpiryWarningDays"/> to alert when retraining is needed.
    /// </remarks>
    public class HseqTrainingRecord : BaseEntity
    {


        /// <summary> Name of the employee who completed the training (snapshot or fallback). </summary>
        public string EmployeeName { get; set; } = string.Empty; 

        /// <summary> Foreign Key linking to the <see cref="Employee"/> entity. </summary>
        public Guid? EmployeeId { get; set; }

        /// <summary> The role or job description of the employee at the time of training. </summary>
        public string Role { get; set; } = string.Empty;

        /// <summary> The subject matter of the training (e.g., "Fire Fighting Level 1"). </summary>
        public string TrainingTopic { get; set; } = string.Empty;

        /// <summary> The date the training was finalized. </summary>
        public DateTime DateCompleted { get; set; }

        /// <summary> The date the certification expires. </summary>
        public DateTime? ValidUntil { get; set; }

        /// <summary> The name of the organization or individual who provided the training. </summary>
        public string Trainer { get; set; } = string.Empty;

        /// <summary> URL or path to a digital copy of the certificate. </summary>
        public string CertificateUrl { get; set; } = string.Empty;
        
        /// <summary> The specific type or level of certificate (e.g., "NQF Level 3"). </summary>
        public string CertificateType { get; set; } = string.Empty;

        /// <summary> Number of days before expiration to trigger a warning. </summary>
        public int ExpiryWarningDays { get; set; } = 30;


    }
}
