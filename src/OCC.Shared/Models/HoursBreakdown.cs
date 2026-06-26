namespace OCC.Shared.Models
{
    /// <summary>
    /// Immutable result of a single attendance-record hours calculation.
    /// All values are in decimal hours.
    /// </summary>
    /// <param name="Normal">
    /// Standard weekday hours within the employee's defined shift window,
    /// after lunch deduction (if applicable).
    /// </param>
    /// <param name="Overtime15">
    /// Hours attracting 1.5× pay — Saturday hours or weekday hours beyond
    /// the shift end time.
    /// </param>
    /// <param name="Overtime20">
    /// Hours attracting 2.0× pay — Sunday hours or Public Holiday hours.
    /// </param>
    /// <param name="Lunch">
    /// Unpaid lunch hours deducted (0 on Saturdays, Sundays, and
    /// Public Holidays per client payroll rules).
    /// </param>
    public record HoursBreakdown(
        double Normal,
        double Overtime15,
        double Overtime20,
        double Lunch);
}
