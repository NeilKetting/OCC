namespace OCC.Shared.Models
{
    /// <summary>
    /// Classification of a wage run execution type.
    /// </summary>
    public enum WageRunType
    {
        /// <summary> Regular scheduled pay cycle run (Weekly, Fortnightly, or Monthly). </summary>
        Standard = 0,

        /// <summary> Ad-hoc mid-week advance pay run ("Mamparra" run for urgent money/debit orders). </summary>
        AdHocAdvance = 1,

        /// <summary> Special correction/adjustment wage run. </summary>
        Correction = 2
    }
}
