using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.WpfClient.Features.CalendarHub.Services
{
    // =========================================================================
    // HolidayService.cs
    // Generates South African public holidays for a given year, including the
    // Easter-based moveable feasts and the SA "Sunday-observed" rule.
    // Ported from OCC.Client (Avalonia) — no external API dependency.
    // =========================================================================

    #region Interface

    /// <summary>
    /// Provides South African public holiday information for calendar display
    /// and business-day calculations.
    /// </summary>
    public interface IHolidayService
    {
        /// <summary>Returns all SA public holidays for the specified calendar year.</summary>
        Task<IEnumerable<PublicHoliday>> GetHolidaysForYearAsync(int year);

        /// <summary>Returns <c>true</c> if the given date is a SA public holiday.</summary>
        Task<bool> IsHolidayAsync(DateTime date);

        /// <summary>Returns the holiday name for the given date, or <c>null</c> if not a holiday.</summary>
        Task<string?> GetHolidayNameAsync(DateTime date);
    }

    #endregion

    #region Implementation

    /// <summary>
    /// Computes South African public holidays entirely in-memory (no network call).
    /// Results are cached per year so repeated calls for the same year are free.
    /// </summary>
    public class HolidayService : IHolidayService
    {
        #region Fields

        // Per-year cache: avoids recalculating Easter and observed-day rules on every call.
        private readonly Dictionary<int, List<PublicHoliday>> _cache = new();

        #endregion

        #region Public Methods

        /// <inheritdoc/>
        public Task<IEnumerable<PublicHoliday>> GetHolidaysForYearAsync(int year)
        {
            // Return cached result if available
            if (_cache.TryGetValue(year, out var cached))
                return Task.FromResult<IEnumerable<PublicHoliday>>(cached);

            var holidays = GenerateSAHolidays(year);
            _cache[year] = holidays;
            return Task.FromResult<IEnumerable<PublicHoliday>>(holidays);
        }

        /// <inheritdoc/>
        public async Task<bool> IsHolidayAsync(DateTime date)
        {
            var holidays = await GetHolidaysForYearAsync(date.Year);
            return holidays.Any(h => h.Date.Date == date.Date);
        }

        /// <inheritdoc/>
        public async Task<string?> GetHolidayNameAsync(DateTime date)
        {
            var holidays = await GetHolidaysForYearAsync(date.Year);
            return holidays.FirstOrDefault(h => h.Date.Date == date.Date)?.Name;
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Builds the full list of SA public holidays for <paramref name="year"/>,
        /// including Easter-based dates and the Sunday-observed rule.
        /// </summary>
        private List<PublicHoliday> GenerateSAHolidays(int year)
        {
            var list = new List<PublicHoliday>();

            // ── Fixed-date holidays ───────────────────────────────────────────
            Add(list, year, 1,  1,  "New Year's Day");
            Add(list, year, 3,  21, "Human Rights Day");
            Add(list, year, 4,  27, "Freedom Day");
            Add(list, year, 5,  1,  "Workers' Day");
            Add(list, year, 6,  16, "Youth Day");
            Add(list, year, 8,  9,  "National Women's Day");
            Add(list, year, 9,  24, "Heritage Day");
            Add(list, year, 12, 16, "Day of Reconciliation");
            Add(list, year, 12, 25, "Christmas Day");
            Add(list, year, 12, 26, "Day of Goodwill");

            // ── Easter-based moveable feasts ──────────────────────────────────
            var easterSunday = CalculateEasterSunday(year);
            list.Add(new PublicHoliday { Date = easterSunday.AddDays(-2), Name = "Good Friday" });
            list.Add(new PublicHoliday { Date = easterSunday.AddDays(1),  Name = "Family Day" });

            // ── Sunday-observed rule ──────────────────────────────────────────
            // In SA, when a public holiday falls on a Sunday the following Monday
            // becomes a public holiday in its place.  If that Monday is already a
            // holiday (e.g. Christmas Sunday → Goodwill Monday), Tuesday is used.
            var observed = new List<PublicHoliday>();
            foreach (var h in list)
            {
                if (h.Date.DayOfWeek != DayOfWeek.Sunday)
                    continue;

                var monday = h.Date.AddDays(1);
                if (!list.Any(x => x.Date == monday))
                {
                    // Monday is free — use it as the observed holiday
                    observed.Add(new PublicHoliday { Date = monday, Name = $"{h.Name} (Observed)" });
                }
                else
                {
                    // Monday is already taken — fall back to Tuesday
                    var tuesday = h.Date.AddDays(2);
                    if (!list.Any(x => x.Date == tuesday))
                        observed.Add(new PublicHoliday { Date = tuesday, Name = $"{h.Name} (Observed)" });
                }
            }

            list.AddRange(observed);
            return list.OrderBy(h => h.Date).ToList();
        }

        /// <summary>Appends a fixed-date holiday to <paramref name="list"/>.</summary>
        private static void Add(List<PublicHoliday> list, int year, int month, int day, string name)
            => list.Add(new PublicHoliday { Date = new DateTime(year, month, day), Name = name });

        /// <summary>
        /// Calculates Easter Sunday for the given year using the
        /// Anonymous Gregorian algorithm (Computus).
        /// </summary>
        private static DateTime CalculateEasterSunday(int year)
        {
            // Standard Computus algorithm — works for all years 1900–2099
            int a = year % 19;
            int b = year / 100;
            int c = year % 100;
            int d = b / 4;
            int e = b % 4;
            int f = (b + 8) / 25;
            int g = (b - f + 1) / 3;
            int h = (19 * a + b - d - g + 15) % 30;
            int i = c / 4;
            int k = c % 4;
            int l = (32 + 2 * e + 2 * i - h - k) % 7;
            int m = (a + 11 * h + 22 * l) / 451;
            int month = (h + l - 7 * m + 114) / 31;
            int day   = ((h + l - 7 * m + 114) % 31) + 1;
            return new DateTime(year, month, day);
        }

        #endregion
    }

    #endregion
}
