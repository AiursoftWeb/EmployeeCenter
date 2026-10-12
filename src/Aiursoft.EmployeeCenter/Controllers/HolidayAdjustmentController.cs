using Aiursoft.EmployeeCenter.Authorization;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Models.HolidayAdjustmentViewModels;
using Aiursoft.EmployeeCenter.Services;
using Aiursoft.UiStack.Navigation;
using Aiursoft.WebTools.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.EmployeeCenter.Controllers;

[Authorize(Policy = AppPermissionNames.CanManageGlobalSettings)]
[LimitPerMin]
public class HolidayAdjustmentController(EmployeeCenterDbContext context, HolidayService holidayService) : Controller
{
    [RenderInNavBar(
        NavGroupName = "Career",
        NavGroupOrder = 1,
        CascadedLinksGroupName = "Leave Management",
        CascadedLinksIcon = "calendar-days",
        CascadedLinksOrder = 5,
        LinkText = "Holiday Adjustments",
        LinkOrder = 4)]
    public IActionResult Index()
    {
        return this.StackView(new IndexViewModel());
    }

    /// <summary>
    /// JSON API: get holidays and adjustments for a date range (used by the calendar JS)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetCalendarData(DateTime start, DateTime end)
    {
        // Get all holidays in the range (includes weekends, public holidays, and adjustments)
        var holidays = await holidayService.GetPublicHolidaysInRangeAsync(start, end);

        // Get all manual adjustments in the range
        var adjustments = await context.AdjustedHolidays
            .Where(a => a.Date >= start.Date && a.Date <= end.Date)
            .ToListAsync();

        var adjustmentMap = adjustments.ToDictionary(
            a => a.Date.Date.ToString("yyyy-MM-dd"),
            a => new { type = a.Type == HolidayType.WorkDay ? "work" : "rest", a.Reason });

        var holidayStrings = holidays.Select(h => h.ToString("yyyy-MM-dd")).ToList();

        return Json(new { holidays = holidayStrings, adjustments = adjustmentMap });
    }

    /// <summary>
    /// JSON API: toggle a day's adjustment status.
    /// If already adjusted, remove the adjustment (revert to natural).
    /// If not adjusted, create an adjustment that flips the natural status.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ToggleDay([FromBody] ToggleDayRequest request)
    {
        var targetDate = request.Date.Date;

        // Check if an adjustment already exists
        var existing = await context.AdjustedHolidays
            .FirstOrDefaultAsync(a => a.Date.Date == targetDate);

        if (existing != null)
        {
            // Remove it — revert to natural
            context.AdjustedHolidays.Remove(existing);
            await context.SaveChangesAsync();

            // Re-check the natural status after removal
            var isNaturalHoliday = await holidayService.IsPublicHolidayAsync(targetDate);
            return Json(new
            {
                date = targetDate.ToString("yyyy-MM-dd"),
                isNonWorking = isNaturalHoliday,
                isAdjusted = false,
                adjustmentType = (string?)null
            });
        }

        // Determine the natural status of this day
        var isNaturallyNonWorking = await IsNaturallyNonWorkingAsync(targetDate);

        // Create an adjustment that flips the natural status
        var newType = isNaturallyNonWorking ? HolidayType.WorkDay : HolidayType.RestDay;
        var reason = isNaturallyNonWorking ? "Compensatory work day (调休上班)" : "Holiday adjustment (调休放假)";

        context.AdjustedHolidays.Add(new AdjustedHoliday
        {
            Date = targetDate,
            Type = newType,
            Reason = reason
        });
        await context.SaveChangesAsync();

        return Json(new
        {
            date = targetDate.ToString("yyyy-MM-dd"),
            isNonWorking = newType == HolidayType.RestDay,
            isAdjusted = true,
            adjustmentType = newType == HolidayType.WorkDay ? "work" : "rest"
        });
    }

    /// <summary>
    /// Check the "natural" (without adjustments) status of a date.
    /// A date is naturally non-working if it's a weekend or an API-reported holiday.
    /// </summary>
    private async Task<bool> IsNaturallyNonWorkingAsync(DateTime date)
    {
        // Check the API (ignoring local adjustments — we already removed any existing adjustment above)
        // The service will automatically fallback to weekend logic if the date is not in the API list.
        return await holidayService.IsPublicHolidayAsync(date);
    }

    public class ToggleDayRequest
    {
        public DateTime Date { get; set; }
    }
}
