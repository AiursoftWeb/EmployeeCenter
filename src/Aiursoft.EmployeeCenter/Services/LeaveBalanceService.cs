using Aiursoft.EmployeeCenter.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.EmployeeCenter.Services;

/// <summary>
/// Service for calculating leave balances and managing leave allocations
/// </summary>
public class LeaveBalanceService(
    EmployeeCenterDbContext context,
    HolidayService holidayService,
    GlobalSettingsService globalSettingsService)
{
    private readonly EmployeeCenterDbContext _context = context;
    private readonly HolidayService _holidayService = holidayService;
    private readonly GlobalSettingsService _globalSettingsService = globalSettingsService;

    // Company Leave Policy - Can be modified to change future allocations
    // Note: Changing these values only affects NEW allocations created after the change.
    // Existing LeaveBalance records in the database retain their original allocation values.
    // This ensures historical data integrity while allowing policy adjustments.

    /// <summary>
    /// Annual leave days allocated per year for new allocations
    /// </summary>
    public const decimal DefaultAnnualLeavePerYear = 12m;

    /// <summary>
    /// Sick leave days allocated per year for new allocations
    /// </summary>
    public const decimal DefaultSickLeavePerYear = 7m;

    public async Task<decimal> GetAnnualLeavePerYearAsync()
    {
        return await _globalSettingsService.GetIntSettingAsync(Configuration.SettingsMap.AnnualLeavePerYear);
    }

    /// <summary>
    /// Ensure leave allocation record exists for a user in a specific year
    /// Creates with default values if not exists
    /// </summary>
    public async Task EnsureLeaveAllocationExistsAsync(string userId, int year)
    {
        var existingAllocation = await _context.LeaveBalances
            .FirstOrDefaultAsync(lb => lb.UserId == userId && lb.Year == year);

        if (existingAllocation == null)
        {
            var annualLeave = await GetAnnualLeavePerYearAsync();
            var newAllocation = new LeaveBalance
            {
                UserId = userId,
                Year = year,
                AnnualLeaveAllocation = annualLeave,
                SickLeaveAllocation = DefaultSickLeavePerYear
            };

            _context.LeaveBalances.Add(newAllocation);
            await _context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Get remaining annual leave breakdown (carried vs current)
    /// Returns tuple of (carriedRemaining, currentRemaining) using FIFO deduction
    /// </summary>
    public async Task<(decimal carriedRemaining, decimal currentRemaining)> GetRemainingByTypeAsync(string userId, int year)
    {
        await EnsureLeaveAllocationExistsAsync(userId, year);

        var allocation = await _context.LeaveBalances
            .FirstAsync(lb => lb.UserId == userId && lb.Year == year);

        var startOfYear = new DateTime(year, 1, 1);
        var endOfYear = startOfYear.AddYears(1);

        // Calculate used annual leave (including pending to prevent duplicate requests)
        var usedThisYear = await _context.LeaveApplications
            .Where(la => la.UserId == userId
                && la.LeaveType == LeaveType.AnnualLeave
                && la.StartDate >= startOfYear
                && la.StartDate < endOfYear
                && !la.IsWithdrawn
                && (la.IsPending || la.IsApproved))
            .SumAsync(la => la.TotalDays);

        // Use carried first (FIFO)
        var carriedUsed = Math.Min(usedThisYear, allocation.CarriedOverAnnualLeave);
        var currentUsed = usedThisYear - carriedUsed;

        return (
            carriedRemaining: allocation.CarriedOverAnnualLeave - carriedUsed,
            currentRemaining: allocation.AnnualLeaveAllocation - currentUsed
        );
    }

    /// <summary>
    /// Get remaining annual leave for a user in a specific year
    /// Total = CarriedOver + Current allocation
    /// </summary>
    public async Task<decimal> GetRemainingAnnualLeaveAsync(string userId, int year)
    {
        var (carriedRemaining, currentRemaining) = await GetRemainingByTypeAsync(userId, year);
        var total = carriedRemaining + currentRemaining;
        return Math.Max(0m, total); // Prevent negative balances
    }

    /// <summary>
    /// Get remaining sick leave for a user in a specific year
    /// Sick leave does not carry over
    /// </summary>
    public async Task<decimal> GetRemainingSickLeaveAsync(string userId, int year)
    {
        await EnsureLeaveAllocationExistsAsync(userId, year);

        var allocation = await _context.LeaveBalances
            .FirstAsync(lb => lb.UserId == userId && lb.Year == year);

        var startOfYear = new DateTime(year, 1, 1);
        var endOfYear = startOfYear.AddYears(1);

        // Calculate used sick leave (including pending applications)
        var usedThisYear = await _context.LeaveApplications
            .Where(la => la.UserId == userId
                && la.LeaveType == LeaveType.SickLeave
                && la.StartDate >= startOfYear
                && la.StartDate < endOfYear
                && !la.IsWithdrawn // Exclude withdrawn
                && (la.IsPending || la.IsApproved)) // Exclude rejected
            .SumAsync(la => la.TotalDays);

        var remaining = allocation.SickLeaveAllocation - usedThisYear;

        return Math.Max(0, remaining);
    }

    /// <summary>
    /// Calculate working days in a date range, excluding weekends and public holidays, but respecting Adjusted Holidays
    /// </summary>
    public async Task<decimal> CalculateWorkingDaysAsync(DateTime start, DateTime end, HashSet<DateTime>? publicHolidays = null)
    {
        // Fetch public holidays if not provided
        publicHolidays ??= await _holidayService.GetPublicHolidaysInRangeAsync(start, end);

        var adjustments = await _context.AdjustedHolidays
            .Where(a => a.Date >= start.Date && a.Date <= end.Date)
            .ToDictionaryAsync(a => a.Date.Date);

        var workingDays = 0m;
        var currentDate = start.Date;

        while (currentDate <= end.Date)
        {
            if (adjustments.TryGetValue(currentDate, out var adjustment))
            {
                if (adjustment.Type == HolidayType.WorkDay)
                {
                    workingDays += 1m;
                }
            }
            else
            {
                // Skip weekends (Saturday = 6, Sunday = 0)
                var dayOfWeek = (int)currentDate.DayOfWeek;
                var isWeekend = dayOfWeek == 0 || dayOfWeek == 6;

                // Skip public holidays
                var isPublicHoliday = publicHolidays.Contains(currentDate);

                if (!isWeekend && !isPublicHoliday)
                {
                    workingDays += 1m;
                }
            }

            currentDate = currentDate.AddDays(1);
        }

        return workingDays;
    }
}
