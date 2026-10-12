using Aiursoft.EmployeeCenter.Authorization;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Models.LeaveViewModels;
using Aiursoft.EmployeeCenter.Services;
using Aiursoft.UiStack.Navigation;
using Aiursoft.WebTools.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.EmployeeCenter.Controllers;

[Authorize]
[LimitPerMin]
public class LeaveController(
    UserManager<User> userManager,
    EmployeeCenterDbContext context,
    LeaveBalanceService leaveBalanceService,
    HolidayService holidayService,
    IAuthorizationService authorizationService,
    GlobalSettingsService globalSettingsService)
    : Controller
{
    [RenderInNavBar(
        NavGroupName = "Career",
        NavGroupOrder = 1,
        CascadedLinksGroupName = "Leave Management",
        CascadedLinksIcon = "calendar-days",
        CascadedLinksOrder = 5,
        LinkText = "My Leave Balance",
        LinkOrder = 1)]
    public async Task<IActionResult> Index()
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        var currentYear = DateTime.UtcNow.Year;

        // Ensure leave allocation exists
        await leaveBalanceService.EnsureLeaveAllocationExistsAsync(user.Id, currentYear);

        // Get leave balances
        var allocation = await context.LeaveBalances
            .FirstAsync(lb => lb.UserId == user.Id && lb.Year == currentYear);
        var remainingAnnual = await leaveBalanceService.GetRemainingAnnualLeaveAsync(user.Id, currentYear);
        var (carriedOver, _) = await leaveBalanceService.GetRemainingByTypeAsync(user.Id, currentYear);
        var remainingSick = await leaveBalanceService.GetRemainingSickLeaveAsync(user.Id, currentYear);
        var annualLeavePerYearPolicy = await globalSettingsService.GetIntSettingAsync(Configuration.SettingsMap.AnnualLeavePerYear);

        // Get leave history
        var leaveHistory = await context.LeaveApplications
            .Include(la => la.ReviewedBy)
            .Where(la => la.UserId == user.Id)
            .OrderByDescending(la => la.SubmittedAt)
            .ToListAsync();

        // Get next upcoming approved leave within 14 days
        var today = DateTime.UtcNow.Date;
        var fourteenDaysFromNow = today.AddDays(14);
        var nextUpcomingLeave = await context.LeaveApplications
            .Where(la => la.UserId == user.Id
                && la.IsApproved
                && !la.IsPending
                && !la.IsWithdrawn
                && la.StartDate >= today
                && la.StartDate <= fourteenDaysFromNow)
            .OrderBy(la => la.StartDate)
            .FirstOrDefaultAsync();

        // Calculate statistics
        var pendingCount = leaveHistory.Count(l => l.IsPending && !l.IsWithdrawn);
        int? daysUntilNextLeave = nextUpcomingLeave != null
            ? (int)(nextUpcomingLeave.StartDate.Date - today).TotalDays
            : null;

        var model = new IndexViewModel
        {
            AnnualLeaveAllocation = allocation.AnnualLeaveAllocation,
            CarriedOverAnnualLeave = carriedOver,
            RemainingAnnualLeave = remainingAnnual,
            SickLeaveAllocation = allocation.SickLeaveAllocation,
            RemainingSickLeave = remainingSick,
            LeaveHistory = leaveHistory,
            CanApplyLeave = remainingAnnual > 0 || remainingSick > 0,
            CurrentYear = currentYear,
            NextUpcomingLeave = nextUpcomingLeave,
            HasUpcomingLeaveWithin14Days = nextUpcomingLeave != null,
            PendingCount = pendingCount,
            DaysUntilNextLeave = daysUntilNextLeave,
            AnnualLeavePerYearPolicy = annualLeavePerYearPolicy
        };

        return this.StackView(model);
    }

    public async Task<IActionResult> Apply()
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        var currentYear = DateTime.UtcNow.Year;
        var remainingAnnual = await leaveBalanceService.GetRemainingAnnualLeaveAsync(user.Id, currentYear);
        var remainingSick = await leaveBalanceService.GetRemainingSickLeaveAsync(user.Id, currentYear);

        if (remainingAnnual <= 0 && remainingSick <= 0)
        {
            return RedirectToAction(nameof(Index));
        }

        var model = new ApplyViewModel
        {
            StartDate = DateTime.UtcNow.Date,
            EndDate = DateTime.UtcNow.Date
        };

        return this.StackView(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Apply(ApplyViewModel model)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        // Validate date range - allow from 7 days ago to end of current year
        var today = DateTime.UtcNow.Date;
        var minDate = today.AddDays(-7); // Allow up to 7 days in the past for retroactive entry
        var maxDate = new DateTime(today.Year, 12, 31); // End of current year

        if (model.StartDate < minDate)
        {
            ModelState.AddModelError(nameof(model.StartDate), "Start date cannot be more than 7 days in the past.");
        }

        if (model.StartDate > maxDate)
        {
            ModelState.AddModelError(nameof(model.StartDate), "Start date must be within the current year.");
        }

        if (model.EndDate < model.StartDate)
        {
            ModelState.AddModelError(nameof(model.EndDate), "End date must be on or after start date.");
        }

        if (model.EndDate > maxDate)
        {
            ModelState.AddModelError(nameof(model.EndDate), "End date must be within the current year.");
        }

        // Check for overlapping pending leave applications
        var overlappingLeave = await context.LeaveApplications
            .Where(l => l.UserId == user.Id)
            .Where(l => l.IsPending && !l.IsWithdrawn)
            .Where(l => l.StartDate <= model.EndDate && l.EndDate >= model.StartDate)
            .FirstOrDefaultAsync();

        if (overlappingLeave != null)
        {
            ModelState.AddModelError(string.Empty, $"You already have a pending leave application for {overlappingLeave.StartDate:yyyy-MM-dd} to {overlappingLeave.EndDate:yyyy-MM-dd}. Please withdraw it before applying for a new one that overlaps.");
        }

        // Calculate working days
        var holidays = await holidayService.GetPublicHolidaysInRangeAsync(model.StartDate, model.EndDate);
        var workingDays = await leaveBalanceService.CalculateWorkingDaysAsync(model.StartDate, model.EndDate, holidays);

        if (workingDays <= 0)
        {
            ModelState.AddModelError("", "The selected date range contains no working days.");
        }

        model.TotalDays = workingDays;

        // Check if user has enough leave balance
        var currentYear = DateTime.UtcNow.Year;
        if (model.LeaveType == LeaveType.AnnualLeave)
        {
            var remainingAnnual = await leaveBalanceService.GetRemainingAnnualLeaveAsync(user.Id, currentYear);
            if (workingDays > remainingAnnual)
            {
                ModelState.AddModelError("", $"Not enough annual leave balance. You have {remainingAnnual} days remaining.");
            }
        }
        else if (model.LeaveType == LeaveType.SickLeave)
        {
            var remainingSick = await leaveBalanceService.GetRemainingSickLeaveAsync(user.Id, currentYear);
            if (workingDays > remainingSick)
            {
                ModelState.AddModelError("", $"Not enough sick leave balance. You have {remainingSick} days remaining.");
            }
        }

        if (!ModelState.IsValid)
        {
            return this.StackView(model);
        }

        // Create leave application
        var leaveApplication = new LeaveApplication
        {
            UserId = user.Id,
            LeaveType = model.LeaveType,
            StartDate = model.StartDate,
            EndDate = model.EndDate,
            TotalDays = workingDays,
            Reason = model.Reason,
            IsPending = true,
            IsApproved = false
        };

        context.LeaveApplications.Add(leaveApplication);
        await context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// AJAX endpoint to get holidays in a date range for calendar rendering
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetHolidays(DateTime start, DateTime end)
    {
        var holidays = await holidayService.GetPublicHolidaysInRangeAsync(start, end);
        var holidayStrings = holidays.Select(h => h.ToString("yyyy-MM-dd")).ToList();
        return Json(new { holidays = holidayStrings });
    }
    /// <summary>
    /// Withdraw a leave application
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Withdraw(int id)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        var leave = await context.LeaveApplications
            .FirstOrDefaultAsync(l => l.Id == id && l.UserId == user.Id);

        if (leave == null)
        {
            return NotFound();
        }

        if (leave.IsWithdrawn)
        {
            return RedirectToAction(nameof(Index));
        }

        // Cannot withdraw if the leave starts within 24 hours (server time is UTC as per requirement)
        if (DateTime.UtcNow.AddHours(24) > leave.StartDate)
        {
            return BadRequest("Cannot withdraw leave that starts within 24 hours.");
        }

        leave.IsWithdrawn = true;
        leave.WithdrawnAt = DateTime.UtcNow;

        await context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    [RenderInNavBar(
        NavGroupName = "Career",
        NavGroupOrder = 1,
        CascadedLinksGroupName = "Leave Management",
        CascadedLinksIcon = "calendar-days",
        CascadedLinksOrder = 5,
        LinkText = "Team Calendar",
        LinkOrder = 2)]
    public async Task<IActionResult> TeamCalendar(string? searchUserId)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        var canApproveAny = (await authorizationService.AuthorizeAsync(User, AppPermissionNames.CanApproveAnyLeave)).Succeeded;
        var teamLeaves = new List<TeamMemberLeave>();

        // 0. Me
        var myLeaves = await GetLeavesForUserAsync(user.Id);
        teamLeaves.Add(new TeamMemberLeave
        {
            User = user,
            Leaves = myLeaves,
            Relation = "Me"
        });

        // 1. My Boss
        if (!string.IsNullOrEmpty(user.ManagerId))
        {
            var boss = await context.Users.FindAsync(user.ManagerId);
            if (boss != null)
            {
                var bossLeaves = await GetLeavesForUserAsync(boss.Id);
                teamLeaves.Add(new TeamMemberLeave
                {
                    User = boss,
                    Leaves = bossLeaves,
                    Relation = "Boss"
                });
            }
        }

        // 2. My Direct Reports
        var directReports = await context.Users
            .Where(u => u.ManagerId == user.Id)
            .ToListAsync();
        foreach (var report in directReports)
        {
            var reportLeaves = await GetLeavesForUserAsync(report.Id);
            teamLeaves.Add(new TeamMemberLeave
            {
                User = report,
                Leaves = reportLeaves,
                Relation = "Direct Report"
            });
        }

        // 3. My Peers (colleagues reporting to the same boss)
        if (!string.IsNullOrEmpty(user.ManagerId))
        {
            var peers = await context.Users
                .Where(u => u.ManagerId == user.ManagerId && u.Id != user.Id)
                .ToListAsync();
            foreach (var peer in peers)
            {
                var peerLeaves = await GetLeavesForUserAsync(peer.Id);
                teamLeaves.Add(new TeamMemberLeave
                {
                    User = peer,
                    Leaves = peerLeaves,
                    Relation = "Colleague"
                });
            }
        }

        var model = new TeamCalendarViewModel
        {
            TeamLeaves = teamLeaves,
            CanApproveAnyLeave = canApproveAny
        };

        if (canApproveAny && !string.IsNullOrEmpty(searchUserId))
        {
            var searchedUser = await context.Users.FindAsync(searchUserId);
            if (searchedUser != null)
            {
                var oneYearAgo = DateTime.UtcNow.Date.AddDays(-365);
                var oneYearLater = DateTime.UtcNow.Date.AddDays(365);

                var searchedUserLeaves = await context.LeaveApplications
                    .Include(l => l.ReviewedBy)
                    .Where(l => l.UserId == searchUserId && l.StartDate >= oneYearAgo && l.StartDate <= oneYearLater)
                    .OrderByDescending(l => l.StartDate)
                    .ToListAsync();

                model.SearchedUser = searchedUser;
                model.SearchedUserLeaves = searchedUserLeaves;
            }
        }

        return this.StackView(model);
    }

    private async Task<List<LeaveApplication>> GetLeavesForUserAsync(string userId)
    {
        return await context.LeaveApplications
            .Where(l => l.UserId == userId && l.IsApproved && !l.IsPending && !l.IsWithdrawn)
            .OrderBy(l => l.StartDate)
            .ToListAsync();
    }

    [RenderInNavBar(
        NavGroupName = "Career",
        NavGroupOrder = 1,
        CascadedLinksGroupName = "Leave Management",
        CascadedLinksIcon = "calendar-days",
        CascadedLinksOrder = 5,
        LinkText = "Approval Center",
        LinkOrder = 3)]
    [HttpGet]
    public async Task<IActionResult> Incoming(bool showAll = true)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        var canApproveAny = (await authorizationService.AuthorizeAsync(User, AppPermissionNames.CanApproveAnyLeave)).Succeeded;

        // If user cannot approve any leave, they can only see their subordinates.
        if (!canApproveAny)
        {
            showAll = false;
        }

        // Get leaves that are pending approval
        // Condition:
        // 1. Pending (IsPending = true)
        // 2. Not Withdrawn (IsWithdrawn = false)
        // 3. User is in applicant's management chain (recursive) OR User has CanApproveAnyLeave

        var query = context.LeaveApplications
            .Include(l => l.User)
            .Where(l => l.IsPending && !l.IsWithdrawn && l.UserId != user.Id);

        if (!showAll)
        {
            // Get all subordinates recursively
            var approverSubordinates = await GetAllSubordinatesRecursivelyAsync(user.Id);
            query = query.Where(l => approverSubordinates.Contains(l.UserId));
        }

        var incomingLeaves = await query
            .OrderBy(l => l.SubmittedAt)
            .ToListAsync();

        // Get approval history
        var historyQuery = context.LeaveApplications
            .Include(l => l.User)
            .Include(l => l.ReviewedBy)
            .Where(l => l.ReviewedById != null);

        if (!showAll)
        {
            historyQuery = historyQuery.Where(l => l.ReviewedById == user.Id);
        }

        var approvalHistory = await historyQuery
            .OrderByDescending(l => l.ReviewedAt)
            .ToListAsync();

        // Calculate statistics
        var today = DateTime.UtcNow;
        var startOfMonth = new DateTime(today.Year, today.Month, 1);
        var approvedThisMonth = approvalHistory.Count(l => l.IsApproved && l.ReviewedAt >= startOfMonth);
        var rejectedThisMonth = approvalHistory.Count(l => !l.IsApproved && l.ReviewedAt >= startOfMonth);

        // Count members currently on leave
        var subordinateIds = showAll
            ? (await context.Users.Select(u => u.Id).ToListAsync()).ToHashSet()
            : await GetAllSubordinatesRecursivelyAsync(user.Id);

        var teamOnLeaveCount = await context.LeaveApplications
            .Where(l => subordinateIds.Contains(l.UserId)
                && l.IsApproved
                && !l.IsPending
                && !l.IsWithdrawn
                && l.StartDate <= today.Date
                && l.EndDate >= today.Date)
            .CountAsync();

        return this.StackView(new IncomingViewModel
        {
            IncomingLeaves = incomingLeaves,
            ApprovalHistory = approvalHistory,
            PendingCount = incomingLeaves.Count,
            ApprovedThisMonth = approvedThisMonth,
            RejectedThisMonth = rejectedThisMonth,
            TeamOnLeaveCount = teamOnLeaveCount,
            CanApproveAnyLeave = canApproveAny,
            ShowAll = showAll
        });
    }

    /// <summary>
    /// Recursively get all subordinates in the management tree below the given user
    /// </summary>
    private async Task<HashSet<string>> GetAllSubordinatesRecursivelyAsync(string userId)
    {
        var result = new HashSet<string>();
        var toProcess = new Queue<string>();
        toProcess.Enqueue(userId);

        while (toProcess.Count > 0)
        {
            var currentUserId = toProcess.Dequeue();

            // Get direct reports
            var directReports = await context.Users
                .Where(u => u.ManagerId == currentUserId)
                .Select(u => u.Id)
                .ToListAsync();

            foreach (var reportId in directReports)
            {
                if (!result.Contains(reportId))
                {
                    result.Add(reportId);
                    toProcess.Enqueue(reportId);
                }
            }
        }

        return result;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(int id, bool approved)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        var leave = await context.LeaveApplications
            .Include(l => l.User)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (leave == null) return NotFound();

        // Check permission
        // 1. User is in applicant's management chain (recursive)
        // 2. User has CanApproveAnyLeave
        var canApproveAny = (await authorizationService.AuthorizeAsync(User, AppPermissionNames.CanApproveAnyLeave)).Succeeded;

        if (leave.UserId == user.Id)
        {
            return BadRequest("You cannot approve your own leave application.");
        }

        bool isInManagementChain = false;
        if (!canApproveAny)
        {
            // Check if current user is in the applicant's management chain
            var subordinateIds = await GetAllSubordinatesRecursivelyAsync(user.Id);
            isInManagementChain = subordinateIds.Contains(leave.UserId);
        }

        if (!canApproveAny && !isInManagementChain)
        {
            return Unauthorized();
        }

        if (!leave.IsPending || leave.IsWithdrawn)
        {
            return BadRequest("Leave application is not pending or has been withdrawn.");
        }

        leave.IsPending = false;
        leave.IsApproved = approved;
        leave.ReviewedAt = DateTime.UtcNow;
        leave.ReviewedById = user.Id;

        await context.SaveChangesAsync();

        return RedirectToAction(nameof(Incoming));
    }
}
