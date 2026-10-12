using Aiursoft.EmployeeCenter.Authorization;
using Aiursoft.EmployeeCenter.Configuration;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Models.PayrollViewModels;
using Aiursoft.EmployeeCenter.Services;
using Aiursoft.UiStack.Navigation;
using Aiursoft.WebTools.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Aiursoft.CSTools.Tools;

namespace Aiursoft.EmployeeCenter.Controllers;

[Authorize(Policy = AppPermissionNames.CanManagePayroll)]
[LimitPerMin]
public class ManagePayrollController(
    EmployeeCenterDbContext context,
    GlobalSettingsService globalSettingsService)
    : Controller
{
    [RenderInNavBar(
        NavGroupName = "Administration",
        NavGroupOrder = 3,
        CascadedLinksGroupName = "Finance",
        CascadedLinksIcon = "shopping-cart",
        CascadedLinksOrder = 2,
        LinkText = "Manage Payrolls",
        LinkOrder = 1)]
    public async Task<IActionResult> Index()
    {
        var payrolls = await context.Payrolls
            .Include(p => p.Owner)
            .OrderByDescending(p => p.TargetMonth)
            .ToListAsync();

        var users = await context.Users
            .OrderBy(u => u.UserName)
            .ToListAsync();

        return this.StackView(new ManageViewModel
        {
            Payrolls = payrolls,
            AllUsers = users
        });
    }

    [HttpGet]
    public async Task<IActionResult> Export(string? userId, int? year)
    {
        var query = context.Payrolls.Include(t => t.Owner).AsQueryable();

        if (!string.IsNullOrWhiteSpace(userId))
        {
            query = query.Where(t => t.OwnerId == userId);
        }

        if (year.HasValue)
        {
            query = query.Where(t => t.TargetMonth.Year == year.Value);
        }

        var list = await query
            .OrderByDescending(p => p.TargetMonth)
            .ToListAsync();

        var exportList = list.Select(p => new PayrollExportViewModel
        {
            Id = p.Id,
            OwnerId = p.OwnerId,
            OwnerName = p.Owner.UserName ?? string.Empty,
            TargetMonth = p.TargetMonth,
            Content = p.Content,
            BaseSalary = p.BaseSalary,
            JobSalary = p.JobSalary,
            PerformanceBonus = p.PerformanceBonus,
            Overtime = p.Overtime,
            FullAttendance = p.FullAttendance,
            OtherAllowances = p.OtherAllowances,
            LateEarly = p.LateEarly,
            SickLeave = p.SickLeave,
            AdministrativeFines = p.AdministrativeFines,
            PensionPersonal = p.PensionPersonal,
            MedicalPersonal = p.MedicalPersonal,
            UnemploymentPersonal = p.UnemploymentPersonal,
            HousingFundPersonal = p.HousingFundPersonal,
            SpecialAdditionalDeduction = p.SpecialAdditionalDeduction,
            PersonalIncomeTax = p.PersonalIncomeTax,
            Currency = p.Currency,
            TotalAmount = p.TotalAmount,
            BankName = p.BankName,
            BankAccount = p.BankAccount,
            PensionCompany = p.PensionCompany,
            MedicalCompany = p.MedicalCompany,
            UnemploymentCompany = p.UnemploymentCompany,
            WorkInjuryCompany = p.WorkInjuryCompany,
            MaternityCompany = p.MaternityCompany,
            HousingFundCompany = p.HousingFundCompany,
            CreationTime = p.CreationTime
        }).ToList();

        var csv = exportList.ToCsv();
        return File(csv, "text/csv", $"payroll-export-{DateTime.UtcNow:yyyy-MM-dd}.csv");
    }

    public async Task<IActionResult> Create(string? userId = null)
    {
        return this.StackView(new CreateViewModel
        {
            UserId = userId ?? string.Empty,
            Content = string.Empty,
            Currency = await globalSettingsService.GetSettingValueAsync(SettingsMap.DefaultPayrollCurrency),
            CurrencyOptions = SettingsMap.Definitions.First(d => d.Key == SettingsMap.DefaultPayrollCurrency).ChoiceOptions!,
            AllUsers = await context.Users.ToListAsync()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateViewModel model)
    {
        if (ModelState.IsValid)
        {
            var payroll = new Payroll
            {
                OwnerId = model.UserId,
                TargetMonth = model.TargetMonth,
                Content = model.Content,

                BaseSalary = model.BaseSalary,
                JobSalary = model.JobSalary,
                PerformanceBonus = model.PerformanceBonus,
                Overtime = model.Overtime,
                FullAttendance = model.FullAttendance,
                OtherAllowances = model.OtherAllowances,

                LateEarly = model.LateEarly,
                SickLeave = model.SickLeave,
                AdministrativeFines = model.AdministrativeFines,

                PensionPersonal = model.PensionPersonal,
                MedicalPersonal = model.MedicalPersonal,
                UnemploymentPersonal = model.UnemploymentPersonal,
                HousingFundPersonal = model.HousingFundPersonal,

                SpecialAdditionalDeduction = model.SpecialAdditionalDeduction,
                PersonalIncomeTax = model.PersonalIncomeTax,

                Currency = model.Currency,
                TotalAmount = model.TotalAmount,
                BankName = model.BankName,
                BankAccount = model.BankAccount,

                PensionCompany = model.PensionCompany,
                MedicalCompany = model.MedicalCompany,
                UnemploymentCompany = model.UnemploymentCompany,
                WorkInjuryCompany = model.WorkInjuryCompany,
                MaternityCompany = model.MaternityCompany,
                HousingFundCompany = model.HousingFundCompany,

                CreationTime = DateTime.UtcNow
            };
            context.Payrolls.Add(payroll);
            await context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        model.AllUsers = await context.Users.ToListAsync();
        model.CurrencyOptions = SettingsMap.Definitions.First(d => d.Key == SettingsMap.DefaultPayrollCurrency).ChoiceOptions!;
        return this.StackView(model);
    }

    public async Task<IActionResult> Details(int id)
    {
        var payroll = await context.Payrolls
            .Include(p => p.Owner)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (payroll == null) return NotFound();

        return this.StackView(new DetailsViewModel
        {
            Payroll = payroll
        });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var payroll = await context.Payrolls
            .FirstOrDefaultAsync(p => p.Id == id);

        if (payroll == null) return NotFound();

        return this.StackView(new EditViewModel
        {
            Id = payroll.Id,
            UserId = payroll.OwnerId,
            TargetMonth = payroll.TargetMonth,
            Content = payroll.Content,

            BaseSalary = payroll.BaseSalary,
            JobSalary = payroll.JobSalary,
            PerformanceBonus = payroll.PerformanceBonus,
            Overtime = payroll.Overtime,
            FullAttendance = payroll.FullAttendance,
            OtherAllowances = payroll.OtherAllowances,

            LateEarly = payroll.LateEarly,
            SickLeave = payroll.SickLeave,
            AdministrativeFines = payroll.AdministrativeFines,

            PensionPersonal = payroll.PensionPersonal,
            MedicalPersonal = payroll.MedicalPersonal,
            UnemploymentPersonal = payroll.UnemploymentPersonal,
            HousingFundPersonal = payroll.HousingFundPersonal,

            SpecialAdditionalDeduction = payroll.SpecialAdditionalDeduction,
            PersonalIncomeTax = payroll.PersonalIncomeTax,

            Currency = payroll.Currency,
            TotalAmount = payroll.TotalAmount,
            BankName = payroll.BankName,
            BankAccount = payroll.BankAccount,

            PensionCompany = payroll.PensionCompany,
            MedicalCompany = payroll.MedicalCompany,
            UnemploymentCompany = payroll.UnemploymentCompany,
            WorkInjuryCompany = payroll.WorkInjuryCompany,
            MaternityCompany = payroll.MaternityCompany,
            HousingFundCompany = payroll.HousingFundCompany,

            AllUsers = await context.Users.ToListAsync(),
            CurrencyOptions = SettingsMap.Definitions.First(d => d.Key == SettingsMap.DefaultPayrollCurrency).ChoiceOptions!
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditViewModel model)
    {
        if (ModelState.IsValid)
        {
            var payroll = await context.Payrolls
                .FirstOrDefaultAsync(p => p.Id == model.Id);

            if (payroll == null) return NotFound();

            payroll.OwnerId = model.UserId;
            payroll.TargetMonth = model.TargetMonth;
            payroll.Content = model.Content;

            payroll.BaseSalary = model.BaseSalary;
            payroll.JobSalary = model.JobSalary;
            payroll.PerformanceBonus = model.PerformanceBonus;
            payroll.Overtime = model.Overtime;
            payroll.FullAttendance = model.FullAttendance;
            payroll.OtherAllowances = model.OtherAllowances;

            payroll.LateEarly = model.LateEarly;
            payroll.SickLeave = model.SickLeave;
            payroll.AdministrativeFines = model.AdministrativeFines;

            payroll.PensionPersonal = model.PensionPersonal;
            payroll.MedicalPersonal = model.MedicalPersonal;
            payroll.UnemploymentPersonal = model.UnemploymentPersonal;
            payroll.HousingFundPersonal = model.HousingFundPersonal;

            payroll.SpecialAdditionalDeduction = model.SpecialAdditionalDeduction;
            payroll.PersonalIncomeTax = model.PersonalIncomeTax;

            payroll.Currency = model.Currency;
            payroll.TotalAmount = model.TotalAmount;
            payroll.BankName = model.BankName;
            payroll.BankAccount = model.BankAccount;

            payroll.PensionCompany = model.PensionCompany;
            payroll.MedicalCompany = model.MedicalCompany;
            payroll.UnemploymentCompany = model.UnemploymentCompany;
            payroll.WorkInjuryCompany = model.WorkInjuryCompany;
            payroll.MaternityCompany = model.MaternityCompany;
            payroll.HousingFundCompany = model.HousingFundCompany;

            await context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        model.AllUsers = await context.Users.ToListAsync();
        model.CurrencyOptions = SettingsMap.Definitions.First(d => d.Key == SettingsMap.DefaultPayrollCurrency).ChoiceOptions!;
        return this.StackView(model);
    }
}
