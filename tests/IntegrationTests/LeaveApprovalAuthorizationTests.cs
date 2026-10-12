using Aiursoft.EmployeeCenter.Authorization;

namespace Aiursoft.EmployeeCenter.Tests.IntegrationTests;

[TestClass]
public class LeaveApprovalAuthorizationTests
{
    private readonly int _port;
    private readonly HttpClient _http;
    private IHost? _server;

    public LeaveApprovalAuthorizationTests()
    {
        var cookieContainer = new CookieContainer();
        var handler = new HttpClientHandler
        {
            CookieContainer = cookieContainer,
            AllowAutoRedirect = false
        };
        _port = Network.GetAvailablePort();
        _http = new HttpClient(handler)
        {
            BaseAddress = new Uri($"http://localhost:{_port}")
        };
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

    private async Task<string> GetAntiCsrfToken(string url)
    {
        var response = await _http.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        var match = Regex.Match(html,
            @"<input name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)"" />");
        if (!match.Success)
        {
            throw new InvalidOperationException($"Could not find anti-CSRF token on page: {url}");
        }
        return match.Groups[1].Value;
    }

    private async Task LoginAsync(string email, string password)
    {
        var loginToken = await GetAntiCsrfToken("/Account/Login");
        var loginContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "EmailOrUserName", email },
            { "Password", password },
            { "__RequestVerificationToken", loginToken }
        });
        var loginResponse = await _http.PostAsync("/Account/Login", loginContent);
        Assert.AreEqual(HttpStatusCode.Found, loginResponse.StatusCode);
    }

    /// <summary>
    /// Test: Manager can approve direct subordinate's leave
    /// </summary>
    [TestMethod]
    public async Task Manager_CanApprove_DirectSubordinate()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        string employeeId, managerId, managerEmail;

        // Create users using UserManager
        using (var scope = _server!.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

            var employee = new User
            {
                UserName = "emp" + suffix,
                DisplayName = "Employee",
                Email = "emp" + suffix + "@test.com",
                AvatarRelativePath = User.DefaultAvatarPath
            };
            var manager = new User
            {
                UserName = "mgr" + suffix,
                DisplayName = "Manager",
                Email = "mgr" + suffix + "@test.com",
                AvatarRelativePath = User.DefaultAvatarPath
            };

            var empResult = await userManager.CreateAsync(employee, "Password123!");
            var mgrResult = await userManager.CreateAsync(manager, "Password123!");

            Assert.IsTrue(empResult.Succeeded && mgrResult.Succeeded, "User creation failed");

            employeeId = employee.Id;
            managerId = manager.Id;
            managerEmail = manager.Email!;
        }

        // Set reporting relationship: employee reports to manager
        using (var scope = _server!.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var employee = await userManager.FindByIdAsync(employeeId);
            employee!.ManagerId = managerId;
            await userManager.UpdateAsync(employee);
        }

        // Employee applies for leave
        int leaveId;
        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var leave = new LeaveApplication
            {
                UserId = employeeId,
                LeaveType = LeaveType.AnnualLeave,
                StartDate = DateTime.UtcNow.Date.AddDays(1),
                EndDate = DateTime.UtcNow.Date.AddDays(3),
                TotalDays = 3m,
                Reason = "Test leave",
                SubmittedAt = DateTime.UtcNow,
                IsPending = true
            };
            db.LeaveApplications.Add(leave);
            await db.SaveChangesAsync();
            leaveId = leave.Id;
        }

        // Manager logs in and approves
        await LoginAsync(managerEmail, "Password123!");

        var token = await GetAntiCsrfToken("/Leave/Incoming");
        var reviewContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "id", leaveId.ToString() },
            { "approved", "true" },
            { "__RequestVerificationToken", token }
        });

        var reviewResponse = await _http.PostAsync("/Leave/Review", reviewContent);
        Assert.AreEqual(HttpStatusCode.Found, reviewResponse.StatusCode, "Manager should approve subordinate's leave");

        // Verify
        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var leave = await db.LeaveApplications.FindAsync(leaveId);
            Assert.IsTrue(leave!.IsApproved, "Leave should be approved");
            Assert.IsFalse(leave.IsPending, "Leave should not be pending");
        }
    }

    /// <summary>
    /// Test: Manager CANNOT approve non-subordinate's leave
    /// </summary>
    [TestMethod]
    public async Task Manager_CannotApprove_NonSubordinate()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        string employeeId, otherEmail;

        // Create users
        using (var scope = _server!.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

            var employee = new User
            {
                UserName = "emp" + suffix,
                DisplayName = "Employee",
                Email = "emp" + suffix + "@test.com",
                AvatarRelativePath = User.DefaultAvatarPath
            };
            var other = new User
            {
                UserName = "other" + suffix,
                DisplayName = "Other Person",
                Email = "other" + suffix + "@test.com",
                AvatarRelativePath = User.DefaultAvatarPath
            };

            await userManager.CreateAsync(employee, "Password123!");
            await userManager.CreateAsync(other, "Password123!");

            employeeId = employee.Id;
            otherEmail = other.Email!;
        }

        // NO reporting relationship set

        // Employee applies for leave
        int leaveId;
        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var leave = new LeaveApplication
            {
                UserId = employeeId,
                LeaveType = LeaveType.AnnualLeave,
                StartDate = DateTime.UtcNow.Date.AddDays(1),
                EndDate = DateTime.UtcNow.Date.AddDays(2),
                TotalDays = 2m,
                Reason = "Test leave",
                SubmittedAt = DateTime.UtcNow,
                IsPending = true
            };
            db.LeaveApplications.Add(leave);
            await db.SaveChangesAsync();
            leaveId = leave.Id;
        }

        // Other person (not manager) tries to approve
        await LoginAsync(otherEmail, "Password123!");

        // Get token from home page since they can't access /Leave/Incoming
        var token = await GetAntiCsrfToken("/");
        var reviewContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "id", leaveId.ToString() },
            { "approved", "true" },
            { "__RequestVerificationToken", token }
        });

        var reviewResponse = await _http.PostAsync("/Leave/Review", reviewContent);
        // Should be either Forbidden (403) or Unauthorized (401)
        Assert.IsTrue(
            reviewResponse.StatusCode == HttpStatusCode.Forbidden || reviewResponse.StatusCode == HttpStatusCode.Unauthorized,
            $"Non-manager should NOT approve. Got: {reviewResponse.StatusCode}");

        // Verify still pending
        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var leave = await db.LeaveApplications.FindAsync(leaveId);
            Assert.IsTrue(leave!.IsPending, "Leave should still be pending");
        }
    }

    /// <summary>
    /// Test: Recursive approval - A reports to B, B reports to C, C can approve A's leave
    /// </summary>
    [TestMethod]
    public async Task RecursiveApproval_GrandManager_CanApprove()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        string idA, idB, idC, emailC;

        // Create A, B, C
        using (var scope = _server!.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

            var userA = new User { UserName = "usera" + suffix, DisplayName = "User A", Email = "usera" + suffix + "@test.com", AvatarRelativePath = User.DefaultAvatarPath };
            var userB = new User { UserName = "userb" + suffix, DisplayName = "User B", Email = "userb" + suffix + "@test.com", AvatarRelativePath = User.DefaultAvatarPath };
            var userC = new User { UserName = "userc" + suffix, DisplayName = "User C", Email = "userc" + suffix + "@test.com", AvatarRelativePath = User.DefaultAvatarPath };

            await userManager.CreateAsync(userA, "Password123!");
            await userManager.CreateAsync(userB, "Password123!");
            await userManager.CreateAsync(userC, "Password123!");

            idA = userA.Id;
            idB = userB.Id;
            idC = userC.Id;
            emailC = userC.Email!;
        }

        // Set up hierarchy: A → B → C
        using (var scope = _server!.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var userA = await userManager.FindByIdAsync(idA);
            var userB = await userManager.FindByIdAsync(idB);

            userA!.ManagerId = idB;
            userB!.ManagerId = idC;

            await userManager.UpdateAsync(userA);
            await userManager.UpdateAsync(userB);
        }

        // A applies for leave
        int leaveId;
        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var leave = new LeaveApplication
            {
                UserId = idA,
                LeaveType = LeaveType.AnnualLeave,
                StartDate = DateTime.UtcNow.Date.AddDays(1),
                EndDate = DateTime.UtcNow.Date.AddDays(2),
                TotalDays = 2m,
                Reason = "Test recursive",
                SubmittedAt = DateTime.UtcNow,
                IsPending = true
            };
            db.LeaveApplications.Add(leave);
            await db.SaveChangesAsync();
            leaveId = leave.Id;
        }

        // C (grand-manager) approves
        await LoginAsync(emailC, "Password123!");

        var token = await GetAntiCsrfToken("/Leave/Incoming");
        var reviewContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "id", leaveId.ToString() },
            { "approved", "true" },
            { "__RequestVerificationToken", token }
        });

        var reviewResponse = await _http.PostAsync("/Leave/Review", reviewContent);
        Assert.AreEqual(HttpStatusCode.Found, reviewResponse.StatusCode, "Grand-manager should approve via recursive");

        // Verify
        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var leave = await db.LeaveApplications.FindAsync(leaveId);
            Assert.IsTrue(leave!.IsApproved, "Leave should be approved");
            Assert.AreEqual(idC, leave.ReviewedById, "Reviewer should be C");
        }
    }

    /// <summary>
    /// Test: User with CanApproveAnyLeave permission can approve anyone's leave
    /// </summary>
    [TestMethod]
    public async Task UserWithPermission_CanApprove_AnyLeave()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        string employeeId, hrEmail;

        // Create users
        using (var scope = _server!.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            var employee = new User { UserName = "emp" + suffix, DisplayName = "Employee", Email = "emp" + suffix + "@test.com", AvatarRelativePath = User.DefaultAvatarPath };
            var hr = new User { UserName = "hr" + suffix, DisplayName = "HR Person", Email = "hr" + suffix + "@test.com", AvatarRelativePath = User.DefaultAvatarPath };

            await userManager.CreateAsync(employee, "Password123!");
            await userManager.CreateAsync(hr, "Password123!");

            employeeId = employee.Id;
            hrEmail = hr.Email!;

            // Create role with CanApproveAnyLeave permission
            var hrRole = new IdentityRole("HRApprover_" + suffix);
            await roleManager.CreateAsync(hrRole);

            // Add permission claim to role
            await roleManager.AddClaimAsync(hrRole, new Claim("Permission", AppPermissionNames.CanApproveAnyLeave));

            // Assign role to HR user
            await userManager.AddToRoleAsync(hr, hrRole.Name!);
        }

        // Employee applies for leave (NO reporting to HR)
        int leaveId;
        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var leave = new LeaveApplication
            {
                UserId = employeeId,
                LeaveType = LeaveType.AnnualLeave,
                StartDate = DateTime.UtcNow.Date.AddDays(1),
                EndDate = DateTime.UtcNow.Date.AddDays(2),
                TotalDays = 2m,
                Reason = "Test permission",
                SubmittedAt = DateTime.UtcNow,
                IsPending = true
            };
            db.LeaveApplications.Add(leave);
            await db.SaveChangesAsync();
            leaveId = leave.Id;
        }

        // HR person approves
        await LoginAsync(hrEmail, "Password123!");

        var token = await GetAntiCsrfToken("/Leave/Incoming");
        var reviewContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "id", leaveId.ToString() },
            { "approved", "true" },
            { "__RequestVerificationToken", token }
        });

        var reviewResponse = await _http.PostAsync("/Leave/Review", reviewContent);
        Assert.AreEqual(HttpStatusCode.Found, reviewResponse.StatusCode, "HR with permission should approve");

        // Verify
        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var leave = await db.LeaveApplications.FindAsync(leaveId);
            Assert.IsTrue(leave!.IsApproved, "Leave should be approved");
        }
    }

    /// <summary>
    /// Test: User cannot approve their own leave application
    /// </summary>
    [TestMethod]
    public async Task User_CannotApprove_OwnLeave()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        string userId, userEmail;

        // Create user
        using (var scope = _server!.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var user = new User { UserName = "user" + suffix, DisplayName = "User", Email = "user" + suffix + "@test.com", AvatarRelativePath = User.DefaultAvatarPath };
            await userManager.CreateAsync(user, "Password123!");
            userId = user.Id;
            userEmail = user.Email!;
        }

        // User applies for leave
        int leaveId;
        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var leave = new LeaveApplication
            {
                UserId = userId,
                LeaveType = LeaveType.AnnualLeave,
                StartDate = DateTime.UtcNow.Date.AddDays(1),
                EndDate = DateTime.UtcNow.Date.AddDays(2),
                TotalDays = 2m,
                Reason = "Self-approval test",
                SubmittedAt = DateTime.UtcNow,
                IsPending = true
            };
            db.LeaveApplications.Add(leave);
            await db.SaveChangesAsync();
            leaveId = leave.Id;
        }

        // User logs in and tries to approve
        await LoginAsync(userEmail, "Password123!");

        var token = await GetAntiCsrfToken("/");
        var reviewContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "id", leaveId.ToString() },
            { "approved", "true" },
            { "__RequestVerificationToken", token }
        });

        var reviewResponse = await _http.PostAsync("/Leave/Review", reviewContent);
        Assert.AreEqual(HttpStatusCode.BadRequest, reviewResponse.StatusCode, "Should be BadRequest for self-approval");

        // Verify still pending
        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var leave = await db.LeaveApplications.FindAsync(leaveId);
            Assert.IsTrue(leave!.IsPending, "Leave should still be pending");
        }
    }

    /// <summary>
    /// Test: Even user with CanApproveAnyLeave permission cannot approve their own leave
    /// </summary>
    [TestMethod]
    public async Task Admin_CannotApprove_OwnLeave()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        string adminId, adminEmail;

        // Create admin user
        using (var scope = _server!.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            var admin = new User { UserName = "admin" + suffix, DisplayName = "Admin", Email = "admin" + suffix + "@test.com", AvatarRelativePath = User.DefaultAvatarPath };
            await userManager.CreateAsync(admin, "Password123!");
            adminId = admin.Id;
            adminEmail = admin.Email!;

            var adminRole = new IdentityRole("AdminRole_" + suffix);
            await roleManager.CreateAsync(adminRole);
            await roleManager.AddClaimAsync(adminRole, new Claim("Permission", AppPermissionNames.CanApproveAnyLeave));
            await userManager.AddToRoleAsync(admin, adminRole.Name!);
        }

        // Admin applies for leave
        int leaveId;
        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var leave = new LeaveApplication
            {
                UserId = adminId,
                LeaveType = LeaveType.AnnualLeave,
                StartDate = DateTime.UtcNow.Date.AddDays(1),
                EndDate = DateTime.UtcNow.Date.AddDays(2),
                TotalDays = 2m,
                Reason = "Admin self-approval test",
                SubmittedAt = DateTime.UtcNow,
                IsPending = true
            };
            db.LeaveApplications.Add(leave);
            await db.SaveChangesAsync();
            leaveId = leave.Id;
        }

        // Admin logs in and tries to approve
        await LoginAsync(adminEmail, "Password123!");

        var token = await GetAntiCsrfToken("/");
        var reviewContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "id", leaveId.ToString() },
            { "approved", "true" },
            { "__RequestVerificationToken", token }
        });

        var reviewResponse = await _http.PostAsync("/Leave/Review", reviewContent);
        Assert.AreEqual(HttpStatusCode.BadRequest, reviewResponse.StatusCode, "Admin should NOT be able to approve own leave");

        // Verify still pending
        using (var scope = _server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EmployeeCenterDbContext>();
            var leave = await db.LeaveApplications.FindAsync(leaveId);
            Assert.IsTrue(leave!.IsPending, "Leave should still be pending");
        }
    }
}
