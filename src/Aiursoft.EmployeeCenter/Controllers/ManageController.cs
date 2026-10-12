using Aiursoft.EmployeeCenter.Configuration;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Models.ManageViewModels;
using Aiursoft.EmployeeCenter.Services;
using Aiursoft.EmployeeCenter.Services.FileStorage;
using Aiursoft.UiStack.Navigation;
using Aiursoft.WebTools.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace Aiursoft.EmployeeCenter.Controllers;

/// <summary>
/// This controller is used to handle user related actions like change password, change avatar.
/// </summary>
[Authorize]
[LimitPerMin]
public class ManageController(
    ImageProcessingService image,
    StorageService storageService,
    IStringLocalizer<ManageController> localizer,
    IOptions<AppSettings> appSettings,
    UserManager<User> userManager,
    SignInManager<User> signInManager,
    GlobalSettingsService settingsService,
    EmployeeCenterDbContext dbContext,
    ILogger<ManageController> logger)
    : Controller
{
    //
    // GET: /Manage/Index
    [RenderInNavBar(
        NavGroupName = "Personal",
        NavGroupOrder = 2,
        CascadedLinksGroupName = "Settings",
        CascadedLinksIcon = "user-circle",
        CascadedLinksOrder = 3,
        LinkText = "Profile Settings",
        LinkOrder = 3)]
    [HttpGet]
    public IActionResult Index(ManageMessageId? message = null)
    {
        ViewData["StatusMessage"] =
            message == ManageMessageId.ChangeProfileSuccess ? localizer["Your profile has been saved."] :
            message == ManageMessageId.ChangeAvatarSuccess ? localizer["Your avatar has been saved."] :
            message == ManageMessageId.ChangePasswordSuccess ? localizer["Your password has been changed."] :
            message == ManageMessageId.ChangeBankInfoSuccess ? localizer["Your bank information has been saved."] :
            message == ManageMessageId.Error ? localizer["An error has occurred."]
            : "";

        var model = new IndexViewModel();
        return this.StackView(model);
    }

    //
    // GET: /Manage/MaintainProfile
    [HttpGet]
    public async Task<IActionResult> MaintainProfile()
    {
        var user = await GetCurrentUserAsync();
        return this.StackView(new MaintainProfileViewModel
        {
            LegalName = user!.LegalName,
            PhoneNumber = user.PhoneNumber,
            BankAccount = user.BankAccount,
            BankName = user.BankName,
            BankAccountName = user.BankAccountName
        });
    }

    //
    // POST: /Manage/MaintainProfile
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MaintainProfile(MaintainProfileViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return this.StackView(model);
        }

        var user = await GetCurrentUserAsync();
        if (user != null)
        {
            user.LegalName = model.LegalName;
            user.PhoneNumber = model.PhoneNumber;

            if (user.BankAccount != model.BankAccount ||
                user.BankName != model.BankName ||
                user.BankAccountName != model.BankAccountName)
            {
                var log = new BankCardChangeLog
                {
                    UserId = user.Id,
                    OldBankCardNumber = user.BankAccount,
                    NewBankCardNumber = model.BankAccount,
                    OldBankName = user.BankName,
                    NewBankName = model.BankName,
                    OldBankAccountName = user.BankAccountName,
                    NewBankAccountName = model.BankAccountName,
                    ChangedByUserId = user.Id,
                    ChangeTime = DateTime.UtcNow
                };
                dbContext.BankCardChangeLogs.Add(log);

                user.BankAccount = model.BankAccount;
                user.BankName = model.BankName;
                user.BankAccountName = model.BankAccountName;
            }

            await userManager.UpdateAsync(user);
            await dbContext.SaveChangesAsync();

            return RedirectToAction(nameof(Index), new { Message = ManageMessageId.ChangeProfileSuccess });
        }
        return RedirectToAction(nameof(Index), new { Message = ManageMessageId.Error });
    }

    //
    // GET: /Manage/ChangeBankInfo
    [HttpGet]
    public async Task<IActionResult> ChangeBankInfo()
    {
        var user = await GetCurrentUserAsync();
        return this.StackView(new ChangeBankInfoViewModel
        {
            BankCardNumber = user!.BankAccount,
            BankName = user.BankName,
            BankAccountName = user.BankAccountName
        });
    }

    //
    // POST: /Manage/ChangeBankInfo
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeBankInfo(ChangeBankInfoViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return this.StackView(model);
        }

        var user = await GetCurrentUserAsync();
        if (user != null)
        {
            if (user.BankAccount != model.BankCardNumber ||
                user.BankName != model.BankName ||
                user.BankAccountName != model.BankAccountName)
            {
                var log = new BankCardChangeLog
                {
                    UserId = user.Id,
                    OldBankCardNumber = user.BankAccount,
                    NewBankCardNumber = model.BankCardNumber,
                    OldBankName = user.BankName,
                    NewBankName = model.BankName,
                    OldBankAccountName = user.BankAccountName,
                    NewBankAccountName = model.BankAccountName,
                    ChangedByUserId = user.Id,
                    ChangeTime = DateTime.UtcNow
                };
                dbContext.BankCardChangeLogs.Add(log);

                user.BankAccount = model.BankCardNumber;
                user.BankName = model.BankName;
                user.BankAccountName = model.BankAccountName;
                await userManager.UpdateAsync(user);
                await dbContext.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index), new { Message = ManageMessageId.ChangeBankInfoSuccess });
        }
        return RedirectToAction(nameof(Index), new { Message = ManageMessageId.Error });
    }

    //
    // GET: /Manage/ChangePassword
    [HttpGet]
    public IActionResult ChangePassword()
    {
        if (appSettings.Value.OIDCEnabled)
        {
            return BadRequest("Local password is disabled when OIDC authentication is enabled.");
        }
        return this.StackView(new ChangePasswordViewModel());
    }

    //
    // POST: /Manage/ChangePassword
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (appSettings.Value.OIDCEnabled)
        {
            return BadRequest("Local password is disabled when OIDC authentication is enabled.");
        }
        if (!ModelState.IsValid)
        {
            return this.StackView(model);
        }
        var user = await GetCurrentUserAsync();
        if (user != null)
        {
            var result = await userManager.ChangePasswordAsync(user, model.OldPassword!, model.NewPassword!);
            if (result.Succeeded)
            {
                await signInManager.SignInAsync(user, isPersistent: false);
                logger.LogInformation(3, "User changed their password successfully");
                return RedirectToAction(nameof(Index), new { Message = ManageMessageId.ChangePasswordSuccess });
            }
            AddErrors(result);
            return this.StackView(model);
        }
        return RedirectToAction(nameof(Index), new { Message = ManageMessageId.Error });
    }

    //
    // GET: /Manage/ChangeProfile
    [HttpGet]
    public async Task<IActionResult> ChangeProfile()
    {
        if (!await settingsService.GetBoolSettingAsync(SettingsMap.AllowUserAdjustNickname))
        {
            return BadRequest("Adjusting nickname is disabled by administrator.");
        }

        var user = await GetCurrentUserAsync();
        return this.StackView(new ChangeProfileViewModel
        {
            Name = user!.DisplayName
        });
    }

    //
    // POST: /Manage/ChangeProfile
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeProfile(ChangeProfileViewModel model)
    {
        if (!await settingsService.GetBoolSettingAsync(SettingsMap.AllowUserAdjustNickname))
        {
            return BadRequest("Adjusting nickname is disabled by administrator.");
        }

        if (!ModelState.IsValid)
        {
            return this.StackView(model);
        }

        var user = await GetCurrentUserAsync();
        if (user != null)
        {
            user.DisplayName = model.Name;
            await userManager.UpdateAsync(user);
            await signInManager.SignInAsync(user, isPersistent: false);
            return RedirectToAction(nameof(Index), new { Message = ManageMessageId.ChangeProfileSuccess });
        }
        return RedirectToAction(nameof(Index), new { Message = ManageMessageId.Error });
    }

    //
    // GET: /Manage/ChangeAvatar
    [HttpGet]
    public async Task<IActionResult> ChangeAvatar()
    {
        var user = await GetCurrentUserAsync();
        return this.StackView(new ChangeAvatarViewModel
        {
            AvatarUrl = user!.AvatarRelativePath
        });
    }

    //
    // POST: /Manage/ChangeAvatar
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeAvatar(ChangeAvatarViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return this.StackView(model);
        }

        // Make sure the file is actually a photo.
        var absolutePath = storageService.GetFilePhysicalPath(model.AvatarUrl);
        if (!await image.IsValidImageAsync(absolutePath))
        {
            ModelState.AddModelError(string.Empty, localizer["The file is not a valid image."]);
            return this.StackView(model);
        }

        // Save the new avatar in the database.
        var user = await GetCurrentUserAsync();
        if (user != null)
        {
            user.AvatarRelativePath = model.AvatarUrl;
            await userManager.UpdateAsync(user);

            // Sign in the user to refresh the avatar.
            await signInManager.SignInAsync(user, isPersistent: false);
            return RedirectToAction(nameof(Index), new { Message = ManageMessageId.ChangeAvatarSuccess });
        }

        return this.StackView(model);
    }

    #region Helpers

    private void AddErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }
    }

    public enum ManageMessageId
    {
        ChangeAvatarSuccess,
        ChangePasswordSuccess,
        ChangeProfileSuccess,
        ChangeBankInfoSuccess,
        Error
    }

    private Task<User?> GetCurrentUserAsync()
    {
        return userManager.GetUserAsync(HttpContext.User);
    }

    #endregion
}
