using Aiursoft.EmployeeCenter.Services;

namespace Aiursoft.EmployeeCenter.Tests.IntegrationTests;

[TestClass]
public class LeaveCarryOverTests
{
    private readonly int _port;
    private IHost? _server;

    public LeaveCarryOverTests()
    {
        _port = Network.GetAvailablePort();
    }

    [TestInitialize]
    public async Task CreateServer()
    {
        _server = await AppAsync<Startup>([], port: _port);
        await _server.UpdateDbAsync<EmployeeCenterDbContext>();
        await _server.SeedAsync();
        await _server.StartAsync();
    }

    [TestCleanup]
    public async Task CleanServer()
    {
        if (_server == null) return;
        await _server.StopAsync();
        _server.Dispose();
    }

    /// <summary>
    /// Test basic scenario: Year 1 use 5 days → Year 2 gets 7 carried + 12 new = 19 total
    /// </summary>
    [TestMethod]
    public async Task Year1_Use5Days_Year2_Has19Days()
    {
        using var scope = _server!.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
        var leaveService = scope.ServiceProvider.GetRequiredService<LeaveBalanceService>();

        var userId = Guid.NewGuid().ToString();
        var year1 = 2024;
        var year2 = 2025;

        // Create user
        var user = new User
        {
            Id = userId,
            UserName = $"test{userId}",
            Email = $"test{userId}@test.com",
            DisplayName = "Test User",
            AvatarRelativePath = "/default.jpg"
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        // Year 1: Create allocation
        await leaveService.EnsureLeaveAllocationExistsAsync(userId, year1);

        // Year 1: Use 5 days
        var leave1 = new LeaveApplication
        {
            UserId = userId,
            LeaveType = LeaveType.AnnualLeave,
            StartDate = new DateTime(year1, 6, 1),
            EndDate = new DateTime(year1, 6, 5),
            TotalDays = 5m,
            Reason = "Vacation",
            SubmittedAt = DateTime.UtcNow,
            IsPending = false,
            IsApproved = true,
            ReviewedAt = DateTime.UtcNow
        };
        context.LeaveApplications.Add(leave1);
        await context.SaveChangesAsync();

        // Verify Year 1: 12 - 5 = 7 remaining
        var remainingYear1 = await leaveService.GetRemainingAnnualLeaveAsync(userId, year1);
        Assert.AreEqual(7m, remainingYear1, "Year 1 should have 7 days remaining");

        // Simulate Year 2 allocation with carry-over (what background job does)
        var year1Allocation = await context.LeaveBalances.FirstAsync(lb => lb.UserId == userId && lb.Year == year1);
        var usedInYear1 = 5m;
        var carriedToYear2 = Math.Min(12m, year1Allocation.AnnualLeaveAllocation - usedInYear1);

        var year2Allocation = new LeaveBalance
        {
            UserId = userId,
            Year = year2,
            AnnualLeaveAllocation = 12m,
            SickLeaveAllocation = 7m,
            CarriedOverAnnualLeave = carriedToYear2
        };
        context.LeaveBalances.Add(year2Allocation);
        await context.SaveChangesAsync();

        // Verify Year 2: Carried = 7, Current = 12, Total = 19
        var (carriedRemaining, currentRemaining) = await leaveService.GetRemainingByTypeAsync(userId, year2);
        Assert.AreEqual(7m, carriedRemaining, "Year 2 should have 7 days carried over");
        Assert.AreEqual(12m, currentRemaining, "Year 2 should have 12 current days");

        var totalYear2 = await leaveService.GetRemainingAnnualLeaveAsync(userId, year2);
        Assert.AreEqual(19m, totalYear2, "Year 2 total should be 19 days");
    }

    /// <summary>
    /// Complete multi-year scenario verifying Year 3 = 24 days
    /// </summary>
    [TestMethod]
    public async Task MultiYear_CarryOver_Year3_Has24Days()
    {
        using var scope = _server!.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
        var leaveService = scope.ServiceProvider.GetRequiredService<LeaveBalanceService>();

        var userId = Guid.NewGuid().ToString();
        var year1 = 2024;
        var year2 = 2025;
        var year3 = 2026;

        // Create user
        var user = new User
        {
            Id = userId,
            UserName = $"test{userId}",
            Email = $"test{userId}@test.com",
            DisplayName = "Test User",
            AvatarRelativePath = "/default.jpg"
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        // === YEAR 1 ===
        await leaveService.EnsureLeaveAllocationExistsAsync(userId, year1);

        // Use 5 days in Year 1
        var leave1 = new LeaveApplication
        {
            UserId = userId,
            LeaveType = LeaveType.AnnualLeave,
            StartDate = new DateTime(year1, 3, 1),
            EndDate = new DateTime(year1, 3, 5),
            TotalDays = 5m,
            Reason = "Year 1 leave",
            SubmittedAt = DateTime.UtcNow,
            IsPending = false,
            IsApproved = true,
            ReviewedAt = DateTime.UtcNow
        };
        context.LeaveApplications.Add(leave1);
        await context.SaveChangesAsync();

        // Year 1 remaining: 12 - 5 = 7
        var remainingYear1 = await leaveService.GetRemainingAnnualLeaveAsync(userId, year1);
        Assert.AreEqual(7m, remainingYear1, "Year 1: 12 - 5 = 7 remaining");

        // === YEAR 2 START ===
        // Simulate background job: carry over 7 days
        var year2Allocation = new LeaveBalance
        {
            UserId = userId,
            Year = year2,
            AnnualLeaveAllocation = 12m,
            SickLeaveAllocation = 7m,
            CarriedOverAnnualLeave = 7m  // From Year 1
        };
        context.LeaveBalances.Add(year2Allocation);
        await context.SaveChangesAsync();

        // Verify Year 2 start: 7 carried + 12 current = 19
        var (carried2, current2) = await leaveService.GetRemainingByTypeAsync(userId, year2);
        Assert.AreEqual(7m, carried2, "Year 2 start: 7 days carried");
        Assert.AreEqual(12m, current2, "Year 2 start: 12 days current");

        var totalYear2Start = await leaveService.GetRemainingAnnualLeaveAsync(userId, year2);
        Assert.AreEqual(19m, totalYear2Start, "Year 2 start: 19 total");

        // === YEAR 2: USE 3 DAYS ===
        var leave2 = new LeaveApplication
        {
            UserId = userId,
            LeaveType = LeaveType.AnnualLeave,
            StartDate = new DateTime(year2, 8, 10),
            EndDate = new DateTime(year2, 8, 12),
            TotalDays = 3m,
            Reason = "Year 2 leave",
            SubmittedAt = DateTime.UtcNow,
            IsPending = false,
            IsApproved = true,
            ReviewedAt = DateTime.UtcNow
        };
        context.LeaveApplications.Add(leave2);
        await context.SaveChangesAsync();

        // Verify Year 2 after using 3: FIFO deduction (carried first)
        var (carried2After, current2After) = await leaveService.GetRemainingByTypeAsync(userId, year2);
        Assert.AreEqual(4m, carried2After, "Year 2 after use: 7 - 3 = 4 carried remaining");
        Assert.AreEqual(12m, current2After, "Year 2 after use: 12 current remaining (untouched)");

        var totalYear2After = await leaveService.GetRemainingAnnualLeaveAsync(userId, year2);
        Assert.AreEqual(16m, totalYear2After, "Year 2 after use: 4 + 12 = 16 total");

        // === YEAR 3 START ===
        // Simulate background job: Year 2's current allocation (12 days) carries over
        // Year 1's carried (4 days remaining) EXPIRES (2-year rule)
        var year3Allocation = new LeaveBalance
        {
            UserId = userId,
            Year = year3,
            AnnualLeaveAllocation = 12m,
            SickLeaveAllocation = 7m,
            CarriedOverAnnualLeave = 12m  // Only Year 2's current, Year 1's carried expires
        };
        context.LeaveBalances.Add(year3Allocation);
        await context.SaveChangesAsync();

        // Verify Year 3: 12 carried (from Year 2 current) + 12 new = 24 total
        var (carried3, current3) = await leaveService.GetRemainingByTypeAsync(userId, year3);
        Assert.AreEqual(12m, carried3, "Year 3: 12 days carried from Year 2's current allocation");
        Assert.AreEqual(12m, current3, "Year 3: 12 days new current allocation");

        var totalYear3 = await leaveService.GetRemainingAnnualLeaveAsync(userId, year3);
        Assert.AreEqual(24m, totalYear3, "Year 3: 12 + 12 = 24 total ✓");
    }

    /// <summary>
    /// Test that users cannot use more leave than available
    /// </summary>
    [TestMethod]
    public async Task CannotOverUse_AnnualLeave()
    {
        using var scope = _server!.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
        var leaveService = scope.ServiceProvider.GetRequiredService<LeaveBalanceService>();

        var userId = Guid.NewGuid().ToString();
        var year = 2024;

        // Create user
        var user = new User
        {
            Id = userId,
            UserName = $"test{userId}",
            Email = $"test{userId}@test.com",
            DisplayName = "Test User",
            AvatarRelativePath = "/default.jpg"
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        // Create allocation: 12 days
        await leaveService.EnsureLeaveAllocationExistsAsync(userId, year);

        // Use 10 days
        var leave1 = new LeaveApplication
        {
            UserId = userId,
            LeaveType = LeaveType.AnnualLeave,
            StartDate = new DateTime(year, 5, 1),
            EndDate = new DateTime(year, 5, 10),
            TotalDays = 10m,
            Reason = "First leave",
            SubmittedAt = DateTime.UtcNow,
            IsPending = false,
            IsApproved = true,
            ReviewedAt = DateTime.UtcNow
        };
        context.LeaveApplications.Add(leave1);
        await context.SaveChangesAsync();

        // Remaining: 12 - 10 = 2
        var remaining = await leaveService.GetRemainingAnnualLeaveAsync(userId, year);
        Assert.AreEqual(2m, remaining, "Should have 2 days remaining");

        // Try to use 3 more days (pending)
        var leave2 = new LeaveApplication
        {
            UserId = userId,
            LeaveType = LeaveType.AnnualLeave,
            StartDate = new DateTime(year, 6, 1),
            EndDate = new DateTime(year, 6, 3),
            TotalDays = 3m,
            Reason = "Second leave",
            SubmittedAt = DateTime.UtcNow,
            IsPending = true,
            IsApproved = false
        };
        context.LeaveApplications.Add(leave2);
        await context.SaveChangesAsync();

        // Pending counts, so: 12 - 10 - 3 = -1, but clamped to 0
        var remainingAfter = await leaveService.GetRemainingAnnualLeaveAsync(userId, year);
        Assert.AreEqual(0m, remainingAfter, "Cannot go negative, should be 0");
    }

    /// <summary>
    /// Test that withdrawn leaves don't count towards usage
    /// </summary>
    [TestMethod]
    public async Task WithdrawnLeaves_DoNotCount()
    {
        using var scope = _server!.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
        var leaveService = scope.ServiceProvider.GetRequiredService<LeaveBalanceService>();

        var userId = Guid.NewGuid().ToString();
        var year = 2024;

        // Create user
        var user = new User
        {
            Id = userId,
            UserName = $"test{userId}",
            Email = $"test{userId}@test.com",
            DisplayName = "Test User",
            AvatarRelativePath = "/default.jpg"
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        await leaveService.EnsureLeaveAllocationExistsAsync(userId, year);

        // Apply and approve 5 days
        var leave = new LeaveApplication
        {
            UserId = userId,
            LeaveType = LeaveType.AnnualLeave,
            StartDate = new DateTime(year, 5, 1),
            EndDate = new DateTime(year, 5, 5),
            TotalDays = 5m,
            Reason = "Vacation",
            SubmittedAt = DateTime.UtcNow,
            IsPending = false,
            IsApproved = true,
            ReviewedAt = DateTime.UtcNow
        };
        context.LeaveApplications.Add(leave);
        await context.SaveChangesAsync();

        // Remaining: 12 - 5 = 7
        var remaining = await leaveService.GetRemainingAnnualLeaveAsync(userId, year);
        Assert.AreEqual(7m, remaining);

        // Withdraw the leave
        leave.IsWithdrawn = true;
        leave.WithdrawnAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        // Should be back to 12
        var remainingAfterWithdraw = await leaveService.GetRemainingAnnualLeaveAsync(userId, year);
        Assert.AreEqual(12m, remainingAfterWithdraw, "Withdrawn leaves should not count");
    }
}
