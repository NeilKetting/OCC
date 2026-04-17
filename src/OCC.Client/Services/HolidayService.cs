using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Managers.Interfaces;
using OCC.Client.Services.Repositories.Interfaces;
using OCC.Shared.Models;

namespace OCC.Client.Services
{
    public class HolidayService : IHolidayService
    {
        // Cache to prevent re-calculating for same year repeatedly
        private readonly Dictionary<int, List<PublicHoliday>> _cache = new();

        public Task<IEnumerable<PublicHoliday>> GetHolidaysForYearAsync(int year)
        {
            if (_cache.ContainsKey(year))
            {
                return Task.FromResult<IEnumerable<PublicHoliday>>(_cache[year]);
            }

            var holidays = GenerateSAHolidays(year);
            _cache[year] = holidays;
            return Task.FromResult<IEnumerable<PublicHoliday>>(holidays);
        }

        public async Task<bool> IsHolidayAsync(DateTime date)
        {
            var holidays = await GetHolidaysForYearAsync(date.Year);
            return holidays.Any(h => h.Date.Date == date.Date);
        }

        public async Task<string?> GetHolidayNameAsync(DateTime date)
        {
             var holidays = await GetHolidaysForYearAsync(date.Year);
             var holiday = holidays.FirstOrDefault(h => h.Date.Date == date.Date);
             return holiday?.Name;
        }

        private List<PublicHoliday> GenerateSAHolidays(int year)
        {
            var list = new List<PublicHoliday>();

            // Fixed Dates
            AddHoliday(list, year, 1, 1, "New Year's Day");
            AddHoliday(list, year, 3, 21, "Human Rights Day");
            AddHoliday(list, year, 4, 27, "Freedom Day");
            AddHoliday(list, year, 5, 1, "Workers' Day");
            AddHoliday(list, year, 6, 16, "Youth Day");
            AddHoliday(list, year, 8, 9, "National Women's Day");
            AddHoliday(list, year, 9, 24, "Heritage Day");
            AddHoliday(list, year, 12, 16, "Day of Reconciliation");
            AddHoliday(list, year, 12, 25, "Christmas Day");
            AddHoliday(list, year, 12, 26, "Day of Goodwill");

            // Variable Dates (Easter)
            var easterSunday = CalculateEasterSunday(year);
            var goodFriday = easterSunday.AddDays(-2);
            var familyDay = easterSunday.AddDays(1);

            list.Add(new PublicHoliday { Date = goodFriday, Name = "Good Friday" });
            list.Add(new PublicHoliday { Date = familyDay, Name = "Family Day" });

            // Sunday Rule: If a public holiday falls on a Sunday, the following Monday is a public holiday.
            // Note: This applies to the *original* date falling on Sunday.
            
            var observed = new List<PublicHoliday>();
            foreach (var h in list)
            {
                if (h.Date.DayOfWeek == DayOfWeek.Sunday)
                {
                    // Check if Monday is already a holiday (e.g. Christmas on Sunday, Goodwill on Monday)
                    // In SA, if Xmas is Sunday, Boxing Day is Monday. Need to confirm if Tuesday becomes holiday.
                    // Usually yes. But simplistic rule: Add Monday.
                    
                    var mondayDetails = h.Date.AddDays(1);
                    
                    // Specific edge case: 25 Dec (Sun) -> 26 Dec (Mon is Goodwill). 
                    // Does 26 Dec move? 
                    // Interpretation: 25th is holiday (Sun). 26th is holiday (Mon - observed Xmas).
                    // But 26th is ALSO Goodwill Day. So 27th becomes public holiday.
                    
                    // Simple implement: Just ensure we don't duplicate logic here too much.
                    // Proper way: Add observed entries.
                    
                    if (!list.Any(existing => existing.Date == mondayDetails))
                    {
                         observed.Add(new PublicHoliday { Date = mondayDetails, Name = $"{h.Name} (Observed)" });
                    }
                    else
                    {
                        // Collision! (e.g. Xmas Sunday, Goodwill Monday)
                        // Then Tuesday is the observed holiday.
                        var tuesday = h.Date.AddDays(2);
                         if (!list.Any(existing => existing.Date == tuesday))
                        {
                             observed.Add(new PublicHoliday { Date = tuesday, Name = $"{h.Name} (Observed)" });
                        }
                    }
                }
            }
            
            list.AddRange(observed);
            return list.OrderBy(h => h.Date).ToList();
        }

        private void AddHoliday(List<PublicHoliday> list, int year, int month, int day, string name)
        {
            list.Add(new PublicHoliday { Date = new DateTime(year, month, day), Name = name });
        }

        private DateTime CalculateEasterSunday(int year)
        {
            // Anonymous Date Computus algorithm
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
            int day = ((h + l - 7 * m + 114) % 31) + 1;

            return new DateTime(year, month, day);
        }
    }
}
