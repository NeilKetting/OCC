using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OCC.Shared.Models;

namespace OCC.Client.Services.Interfaces
{
    public interface IHolidayService
    {
        Task<IEnumerable<PublicHoliday>> GetHolidaysForYearAsync(int year);
        Task<bool> IsHolidayAsync(DateTime date);
        Task<string?> GetHolidayNameAsync(DateTime date);
    }
}
