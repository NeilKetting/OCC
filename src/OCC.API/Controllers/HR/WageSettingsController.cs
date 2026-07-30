using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OCC.API.Data;
using OCC.API.Hubs;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using System;
using System.Threading.Tasks;

namespace OCC.API.Controllers.HR
{
    /// <summary>
    /// API Controller for retrieving and managing system-wide customizable wage settings.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class WageSettingsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;

        public WageSettingsController(AppDbContext context, IHubContext<NotificationHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        /// <summary>
        /// Gets the current wage settings. If no settings record exists in the database,
        /// a default instance is created and saved.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<WageSettings>> GetSettings()
        {
            var settings = await _context.WageSettings.FirstOrDefaultAsync();
            if (settings == null)
            {
                settings = new WageSettings
                {
                    Id = Guid.NewGuid(),
                    CreatedAtUtc = DateTime.UtcNow
                };
                _context.WageSettings.Add(settings);
                await _context.SaveChangesAsync();
            }
            return Ok(settings);
        }

        /// <summary>
        /// Updates the system wage settings.
        /// </summary>
        [HttpPut]
        public async Task<IActionResult> UpdateSettings([FromBody] WageSettings updatedSettings)
        {
            if (updatedSettings == null)
            {
                return BadRequest("Invalid Wage Settings data.");
            }

            var existing = await _context.WageSettings.FirstOrDefaultAsync();
            if (existing == null)
            {
                updatedSettings.Id = Guid.NewGuid();
                updatedSettings.CreatedAtUtc = DateTime.UtcNow;
                _context.WageSettings.Add(updatedSettings);
            }
            else
            {
                existing.CptDefaultPayFrequency = updatedSettings.CptDefaultPayFrequency;
                existing.JhbDefaultPayFrequency = updatedSettings.JhbDefaultPayFrequency;
                existing.WeeklyShiftCutoffDay = updatedSettings.WeeklyShiftCutoffDay;
                existing.BibcRatePerDay = updatedSettings.BibcRatePerDay;
                existing.DefaultSupervisorFee = updatedSettings.DefaultSupervisorFee;
                existing.DefaultCompanyHousingWashingFee = updatedSettings.DefaultCompanyHousingWashingFee;
                existing.DefaultShiftStartTime = updatedSettings.DefaultShiftStartTime;
                existing.DefaultShiftEndTime = updatedSettings.DefaultShiftEndTime;
                existing.LunchEndHourThreshold = updatedSettings.LunchEndHourThreshold;
                existing.DeductLunchOnSaturday = updatedSettings.DeductLunchOnSaturday;
                existing.DeductLunchOnSunday = updatedSettings.DeductLunchOnSunday;
                existing.DeductLunchOnPublicHoliday = updatedSettings.DeductLunchOnPublicHoliday;
                existing.EnableProjectedHours = updatedSettings.EnableProjectedHours;
                existing.AutoRecoverAdHocAdvances = updatedSettings.AutoRecoverAdHocAdvances;
                existing.UpdatedAtUtc = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            var saved = existing ?? updatedSettings;
            await _hubContext.Clients.All.SendAsync("WageSettingsChanged", new EntityChangeDto<WageSettings> { Action = "Updated", Entity = saved, EntityId = saved.Id });

            return Ok(saved);
        }
    }
}
