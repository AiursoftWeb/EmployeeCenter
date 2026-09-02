using Aiursoft.EmployeeCenter.Authorization;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Models.CompanyEntityViewModels;
using Aiursoft.EmployeeCenter.Services;
using Aiursoft.UiStack.Navigation;
using Aiursoft.WebTools.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

using Aiursoft.EmployeeCenter.Services.FileStorage;

namespace Aiursoft.EmployeeCenter.Controllers;

[Authorize]
[LimitPerMin]
public class CompanyEntityController(
    EmployeeCenterDbContext dbContext,
    StorageService storageService,
    IAuthorizationService authorizationService,
    UserManager<User> userManager) : Controller
{
    [HttpGet]
    [RenderInNavBar(
        NavGroupName = "Career",
        NavGroupOrder = 1,
        CascadedLinksGroupName = "Resources",
        CascadedLinksIcon = "briefcase",
        CascadedLinksOrder = 6,
        LinkText = "Company Entity Info",
        LinkOrder = 1)]
    public async Task<IActionResult> Index()
    {
        var entities = await dbContext.CompanyEntities
            .OrderByDescending(t => t.CreationTime)
            .ToListAsync();
        var model = new IndexViewModel
        {
            Entities = entities
        };
        return this.StackView(model);
    }

    [HttpGet]
    [Authorize(Policy = AppPermissionNames.CanManageCompanyEntities)]
    [RenderInNavBar(
        NavGroupName = "Administration",
        NavGroupOrder = 3,
        CascadedLinksGroupName = "Legal",
        CascadedLinksIcon = "scale",
        CascadedLinksOrder = 5,
        LinkText = "Manage Company Entities",
        LinkOrder = 3)]
    public async Task<IActionResult> Manage()
    {
        var entities = await dbContext.CompanyEntities
            .OrderByDescending(t => t.CreationTime)
            .ToListAsync();
        var model = new IndexViewModel
        {
            Entities = entities
        };
        return this.StackView(model, "Index");
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var entity = await dbContext.CompanyEntities.FindAsync(id);
        if (entity == null)
        {
            return NotFound();
        }

        var canViewInfrastructure = (await authorizationService.AuthorizeAsync(
            User,
            AppPermissionNames.CanViewInfrastructure)).Succeeded;
        var servers = canViewInfrastructure
            ? await dbContext.Servers
                .Where(s => s.CompanyEntityId == id)
                .OrderBy(s => s.Hostname)
                .ToListAsync()
            : [];

        var intangibleAssets = await dbContext.IntangibleAssets
            .Where(a => a.CompanyEntityId == id)
            .OrderBy(a => a.Name)
            .ToListAsync();

        var signedEmployees = await dbContext.Users
            .Where(u => u.SigningEntityId == id)
            .OrderBy(u => u.DisplayName)
            .ToListAsync();

        var model = new DetailsViewModel
        {
            Entity = entity,
            Servers = servers,
            CanViewInfrastructure = canViewInfrastructure,
            IntangibleAssets = intangibleAssets,
            SignedEmployees = signedEmployees
        };
        return this.StackView(model);
    }

    [HttpGet]
    [Authorize(Policy = AppPermissionNames.CanManageCompanyEntities)]
    public IActionResult Create()
    {
        return this.StackView(new CreateViewModel
        {
            CompanyName = string.Empty,
            EntityCode = string.Empty
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanManageCompanyEntities)]
    public async Task<IActionResult> Create(CreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return this.StackView(model);
        }

        // Validate Organization Certificate (Strict Vault)
        if (!string.IsNullOrEmpty(model.OrganizationCertificatePath))
        {
            try
            {
                var physicalPath = storageService.GetFilePhysicalPath(model.OrganizationCertificatePath, isVault: true);
                if (!System.IO.File.Exists(physicalPath))
                {
                    ModelState.AddModelError(nameof(model.OrganizationCertificatePath), "File not found or lost. Please re-upload.");
                    return this.StackView(model);
                }
            }
            catch (ArgumentException)
            {
                return BadRequest();
            }
        }

        // Validate License (Dynamic)
        if (!string.IsNullOrEmpty(model.LicensePath))
        {
            try
            {
                bool isVault = model.LicensePath.StartsWith("company-certs");
                var physicalPath = storageService.GetFilePhysicalPath(model.LicensePath, isVault: isVault);
                if (!System.IO.File.Exists(physicalPath))
                {
                    ModelState.AddModelError(nameof(model.LicensePath), "File not found or lost. Please re-upload.");
                    return this.StackView(model);
                }
            }
            catch (ArgumentException)
            {
                return BadRequest();
            }
        }

        var entity = new CompanyEntity
        {
            CompanyName = model.CompanyName,
            CompanyNameEnglish = model.CompanyNameEnglish,
            EntityCode = model.EntityCode,
            CINumber = model.CINumber,
            RegisteredAddress = model.RegisteredAddress,
            OfficeAddress = model.OfficeAddress,
            ZipCode = model.ZipCode,
            LegalRepresentative = model.LegalRepresentative,
            LegalRepresentativeLegalName = model.LegalRepresentativeLegalName,
            CompanyType = model.CompanyType,
            EstablishmentDate = model.EstablishmentDate,
            ExpiryDate = model.ExpiryDate,
            BankName = model.BankName,
            BankAccount = model.BankAccount,
            BankAccountName = model.BankAccountName,
            SwiftCode = model.SwiftCode,
            BankCode = model.BankCode,
            BankAddress = model.BankAddress,
            LogoPath = model.LogoPath,
            SealPath = model.SealPath,
            LicensePath = model.LicensePath,
            OrganizationCertificatePath = model.OrganizationCertificatePath,
            RegisteredCapital = model.RegisteredCapital,
            OperationStatus = model.OperationStatus,
            SCRLocation = model.SCRLocation,
            CompanySecretary = model.CompanySecretary,
            BaseCurrency = model.BaseCurrency,
            CreateLedger = model.CreateLedger
        };

        dbContext.CompanyEntities.Add(entity);
        await dbContext.SaveChangesAsync();

        var user = await userManager.GetUserAsync(User);
        var log = new CompanyEntityLog
        {
            CompanyEntityId = entity.Id,
            UserId = user!.Id,
            Action = "Create",
            Details = JsonConvert.SerializeObject(model),
            Snapshot = JsonConvert.SerializeObject(entity)
        };
        dbContext.CompanyEntityLogs.Add(log);
        await dbContext.SaveChangesAsync();

        return RedirectToAction(nameof(Manage));
    }

    [HttpGet]
    [Authorize(Policy = AppPermissionNames.CanManageCompanyEntities)]
    public async Task<IActionResult> Edit(int id)
    {
        var entity = await dbContext.CompanyEntities.FindAsync(id);
        if (entity == null)
        {
            return NotFound();
        }

        var model = new EditViewModel
        {
            Id = entity.Id,
            CompanyName = entity.CompanyName,
            CompanyNameEnglish = entity.CompanyNameEnglish,
            EntityCode = entity.EntityCode,
            CINumber = entity.CINumber,
            RegisteredAddress = entity.RegisteredAddress,
            OfficeAddress = entity.OfficeAddress,
            ZipCode = entity.ZipCode,
            LegalRepresentative = entity.LegalRepresentative,
            LegalRepresentativeLegalName = entity.LegalRepresentativeLegalName,
            CompanyType = entity.CompanyType,
            EstablishmentDate = entity.EstablishmentDate,
            ExpiryDate = entity.ExpiryDate,
            BankName = entity.BankName,
            BankAccount = entity.BankAccount,
            BankAccountName = entity.BankAccountName,
            SwiftCode = entity.SwiftCode,
            BankCode = entity.BankCode,
            BankAddress = entity.BankAddress,
            LogoPath = entity.LogoPath,
            SealPath = entity.SealPath,
            LicensePath = entity.LicensePath,
            OrganizationCertificatePath = entity.OrganizationCertificatePath,
            RegisteredCapital = entity.RegisteredCapital,
            OperationStatus = entity.OperationStatus,
            SCRLocation = entity.SCRLocation,
            CompanySecretary = entity.CompanySecretary,
            BaseCurrency = entity.BaseCurrency,
            CreateLedger = entity.CreateLedger
        };

        return this.StackView(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanManageCompanyEntities)]
    public async Task<IActionResult> Edit(EditViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return this.StackView(model);
        }

        // Validate Organization Certificate (Strict Vault)
        if (!string.IsNullOrEmpty(model.OrganizationCertificatePath))
        {
            try
            {
                var physicalPath = storageService.GetFilePhysicalPath(model.OrganizationCertificatePath, isVault: true);
                if (!System.IO.File.Exists(physicalPath))
                {
                    ModelState.AddModelError(nameof(model.OrganizationCertificatePath), "File not found or lost. Please re-upload.");
                    return this.StackView(model);
                }
            }
            catch (ArgumentException)
            {
                return BadRequest();
            }
        }

        // Validate License (Dynamic)
        if (!string.IsNullOrEmpty(model.LicensePath))
        {
            try
            {
                bool isVault = model.LicensePath.StartsWith("company-certs");
                var physicalPath = storageService.GetFilePhysicalPath(model.LicensePath, isVault: isVault);
                if (!System.IO.File.Exists(physicalPath))
                {
                    ModelState.AddModelError(nameof(model.LicensePath), "File not found or lost. Please re-upload.");
                    return this.StackView(model);
                }
            }
            catch (ArgumentException)
            {
                return BadRequest();
            }
        }

        var entity = await dbContext.CompanyEntities.FindAsync(model.Id);
        if (entity == null)
        {
            return NotFound();
        }

        var oldSnapshot = JsonConvert.SerializeObject(entity);

        entity.CompanyName = model.CompanyName;
        entity.CompanyNameEnglish = model.CompanyNameEnglish;
        entity.EntityCode = model.EntityCode;
        entity.CINumber = model.CINumber;
        entity.RegisteredAddress = model.RegisteredAddress;
        entity.OfficeAddress = model.OfficeAddress;
        entity.ZipCode = model.ZipCode;
        entity.LegalRepresentative = model.LegalRepresentative;
        entity.LegalRepresentativeLegalName = model.LegalRepresentativeLegalName;
        entity.CompanyType = model.CompanyType;
        entity.EstablishmentDate = model.EstablishmentDate;
        entity.ExpiryDate = model.ExpiryDate;
        entity.BankName = model.BankName;
        entity.BankAccount = model.BankAccount;
        entity.BankAccountName = model.BankAccountName;
        entity.SwiftCode = model.SwiftCode;
        entity.BankCode = model.BankCode;
        entity.BankAddress = model.BankAddress;
        entity.LogoPath = model.LogoPath;
        entity.SealPath = model.SealPath;
        entity.LicensePath = model.LicensePath;
        entity.OrganizationCertificatePath = model.OrganizationCertificatePath;
        entity.RegisteredCapital = model.RegisteredCapital;
        entity.OperationStatus = model.OperationStatus;
        entity.SCRLocation = model.SCRLocation;
        entity.CompanySecretary = model.CompanySecretary;
        entity.BaseCurrency = model.BaseCurrency;
        entity.CreateLedger = model.CreateLedger;
        entity.UpdateTime = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();

        var user = await userManager.GetUserAsync(User);
        var log = new CompanyEntityLog
        {
            CompanyEntityId = entity.Id,
            UserId = user!.Id,
            Action = "Update",
            Details = $"From: {oldSnapshot} To: {JsonConvert.SerializeObject(entity)}",
            Snapshot = JsonConvert.SerializeObject(entity)
        };
        dbContext.CompanyEntityLogs.Add(log);
        await dbContext.SaveChangesAsync();

        return RedirectToAction(nameof(Manage));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPermissionNames.CanManageCompanyEntities)]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await dbContext.CompanyEntities.FindAsync(id);
        if (entity == null)
        {
            return NotFound();
        }

        // Check for required dependencies
        var financeAccounts = await dbContext.FinanceAccounts.Where(f => f.CompanyEntityId == id).ToListAsync();
        var collectionChannelsAsPayer = await dbContext.CollectionChannels.Where(c => c.PayerId == id).ToListAsync();
        var collectionChannelsAsPayee = await dbContext.CollectionChannels.Where(c => c.PayeeId == id).ToListAsync();
        var servers = await dbContext.Servers.Where(s => s.CompanyEntityId == id).ToListAsync();
        var services = await dbContext.Services.Where(s => s.CompanyEntityId == id).ToListAsync();

        if (financeAccounts.Any() || collectionChannelsAsPayer.Any() || collectionChannelsAsPayee.Any() ||
            servers.Any() || services.Any())
        {
            var dependencies = new List<string>();
            if (financeAccounts.Any()) dependencies.Add($"{financeAccounts.Count} finance accounts (Ledger)");
            if (collectionChannelsAsPayer.Any()) dependencies.Add($"{collectionChannelsAsPayer.Count} collection channels as payer");
            if (collectionChannelsAsPayee.Any()) dependencies.Add($"{collectionChannelsAsPayee.Count} collection channels as payee");
            if (servers.Any()) dependencies.Add($"{servers.Count} servers (Infrastructure)");
            if (services.Any()) dependencies.Add($"{services.Count} services (Infrastructure)");

            ModelState.AddModelError(string.Empty, $"Cannot delete this company entity because it is referenced by: {string.Join(", ", dependencies)}. Please delete or reassign these references first.");

            var entities = await dbContext.CompanyEntities
                .OrderByDescending(t => t.CreationTime)
                .ToListAsync();
            var model = new IndexViewModel
            {
                Entities = entities
            };
            return this.StackView(model, "Index");
        }

        // Handle optional dependencies (Set null)
        var assets = await dbContext.Assets.Where(a => a.CompanyEntityId == id).ToListAsync();
        foreach (var asset in assets) asset.CompanyEntityId = null;

        var intangibleAssets = await dbContext.IntangibleAssets.Where(a => a.CompanyEntityId == id).ToListAsync();
        foreach (var ia in intangibleAssets) ia.CompanyEntityId = null;

        var relationships = await dbContext.CustomerRelationships.Where(r => r.CompanyEntityId == id).ToListAsync();
        foreach (var r in relationships) r.CompanyEntityId = null;

        var users = await dbContext.Users.Where(u => u.SigningEntityId == id).ToListAsync();
        foreach (var u in users) u.SigningEntityId = null;

        // Handle logs (Recursive delete)
        var logs = await dbContext.CompanyEntityLogs.Where(l => l.CompanyEntityId == id).ToListAsync();
        dbContext.CompanyEntityLogs.RemoveRange(logs);

        // Add a log for the deletion itself, but with CompanyEntityId = null to avoid FK constraint
        var user = await userManager.GetUserAsync(User);
        var deletionLog = new CompanyEntityLog
        {
            CompanyEntityId = null,
            UserId = user!.Id,
            Action = "Delete",
            Details = $"Deleted company entity: {entity.CompanyName} (ID: {id})",
            Snapshot = JsonConvert.SerializeObject(entity)
        };
        dbContext.CompanyEntityLogs.Add(deletionLog);

        dbContext.CompanyEntities.Remove(entity);
        await dbContext.SaveChangesAsync();

        return RedirectToAction(nameof(Manage));
    }
}
