using Aiursoft.Scanner.Abstractions;
using Aiursoft.EmployeeCenter.Configuration;
using Aiursoft.EmployeeCenter.Controllers;
using Aiursoft.EmployeeCenter.Entities;
using Aiursoft.EmployeeCenter.Services.Authentication;
using Aiursoft.EmployeeCenter.Services.FileStorage;
using Aiursoft.UiStack.Layout;
using Aiursoft.UiStack.Navigation;
using Aiursoft.UiStack.Views.Shared.Components.FooterMenu;
using Aiursoft.UiStack.Views.Shared.Components.LanguagesDropdown;
using Aiursoft.UiStack.Views.Shared.Components.MegaMenu;
using Aiursoft.UiStack.Views.Shared.Components.Navbar;
using Aiursoft.UiStack.Views.Shared.Components.SideAdvertisement;
using Aiursoft.UiStack.Views.Shared.Components.Sidebar;
using Aiursoft.UiStack.Views.Shared.Components.SideLogo;
using Aiursoft.UiStack.Views.Shared.Components.SideMenu;
using Aiursoft.UiStack.Views.Shared.Components.UserDropdown;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace Aiursoft.EmployeeCenter.Services;

public class ViewModelArgsInjector(
    IStringLocalizer<ViewModelArgsInjector> localizer,
    StorageService storageService,
    GlobalSettingsService globalSettingsService,
    NavigationState<Startup> navigationState,
    IAuthorizationService authorizationService,
    IOptions<AppSettings> appSettings,
    SignInManager<User> signInManager) : IScopedDependency
{

    // ReSharper disable once UnusedMember.Local
    private void _useless_for_localizer()
    {
        // Titles, navbar strings.
        _ = localizer["Career"];
        _ = localizer["Missions"];
        _ = localizer["Features"];
        _ = localizer["Index"];
        _ = localizer["Directory"];
        _ = localizer["Users"];
        _ = localizer["Roles"];
        _ = localizer["Administration"];
        _ = localizer["System"];
        _ = localizer["Info"];
        _ = localizer["Manage"];
        _ = localizer["Login"];
        _ = localizer["System Info"];
        _ = localizer["Create User"];
        _ = localizer["User Details"];
        _ = localizer["Edit User"];
        _ = localizer["Delete User"];
        _ = localizer["Create Role"];
        _ = localizer["Role Details"];
        _ = localizer["Edit Role"];
        _ = localizer["Delete Role"];
        _ = localizer["Change Profile"];
        _ = localizer["Change Avatar"];
        _ = localizer["Change Password"];
        _ = localizer["Home"];
        _ = localizer["Settings"];
        _ = localizer["Profile Settings"];
        _ = localizer["Personal"];
        _ = localizer["Unauthorized"];
        _ = localizer["Error"];
        _ = localizer["Permissions"];
        _ = localizer["Background Jobs"];
        _ = localizer["Global Settings"];

        _ = localizer["Approval Center"];
        _ = localizer["Assets"];
        _ = localizer["Certificate of Employment"];
        _ = localizer["Certificate of Income"];
        _ = localizer["Certificate Print"];
        _ = localizer["Company Entity Info"];
        _ = localizer["Company Public Contracts"];
        _ = localizer["Development"];
        _ = localizer["Finance"];
        _ = localizer["Leave Management"];
        _ = localizer["Legal"];
        _ = localizer["Manage Company Entities"];
        _ = localizer["Manage Contracts"];
        _ = localizer["Manage IT Assets"];
        _ = localizer["Manage Payrolls"];
        _ = localizer["My Assets"];
        _ = localizer["My IT Assets"];
        _ = localizer["My Leave Balance"];
        _ = localizer["My Payrolls"];
        _ = localizer["My Report Line"];
        _ = localizer["Onboarding"];
        _ = localizer["Onboarding Process"];
        _ = localizer["Payrolls"];
        _ = localizer["Print Certificate"];
        _ = localizer["Projects"];
        _ = localizer["Services"];
        _ = localizer["Weekly Report"];
        _ = localizer["Report Line"];
        _ = localizer["Resources"];
        _ = localizer["Shared Passwords"];
        _ = localizer["Team Calendar"];

        _ = localizer["Promotion History"];

        _ = localizer["Employee Signals"];
        _ = localizer["Manage Feedback"];

        _ = localizer["Manage Ledger"];

        _ = localizer["Blueprints"];
        _ = localizer["Manage Blueprints"];
        _ = localizer["View Blueprints"];

        _ = localizer["Servers"];

        _ = localizer["Project Requirements"];
        _ = localizer["Approved Projects"];
        _ = localizer["My Requirements"];
        _ = localizer["Approval History"];
        _ = localizer["Manage Requirements"];
        _ = localizer["Submit New Requirement"];
        _ = localizer["Requirement Details"];
        _ = localizer["Requirement"];
        _ = localizer["Submit Requirement"];
        _ = localizer["Edit Requirement"];
        _ = localizer["Requirement Title"];
        _ = localizer["Submit for Approval"];
        _ = localizer["Requirement saved successfully."];
        _ = localizer["Comments"];
        _ = localizer["Reply"];
        _ = localizer["Replying to"];
        _ = localizer["Write a comment..."];
        _ = localizer["Submit Comment"];
        _ = localizer["Approve"];
        _ = localizer["Reject"];
        _ = localizer["Request Changes"];

        _ = localizer["PendingApproval"];
        _ = localizer["Approved"];
        _ = localizer["Rejected"];
        _ = localizer["RequestChanges"];

        _ = localizer["Access Denied"];
        _ = localizer["Add SSH Key"];
        _ = localizer["Bad Request"];
        _ = localizer["Blueprint"];
        _ = localizer["Change Bank Information"];
        _ = localizer["Company Entities"];
        _ = localizer["Company Entity Details"];
        _ = localizer["Contracts"];
        _ = localizer["Create Company Entity"];
        _ = localizer["Create New Contract"];
        _ = localizer["Create Onboarding Task"];
        _ = localizer["Create Password"];
        _ = localizer["Create Promotion"];
        _ = localizer["Create Question"];
        _ = localizer["Create Questionnaire"];
        _ = localizer["Dashboard"];
        _ = localizer["Edit Blueprint"];
        _ = localizer["Edit Company Entity"];
        _ = localizer["Edit Contract"];
        _ = localizer["Edit Onboarding Task"];
        _ = localizer["Edit Password"];
        _ = localizer["Edit Payroll"];
        _ = localizer["Edit SSH Key"];
        _ = localizer["Edit Weekly Report"];
        _ = localizer["Fill Questionnaire"];
        _ = localizer["Internal Server Error"];
        _ = localizer["Issue Payroll"];
        _ = localizer["Lockout"];
        _ = localizer["Maintain Profile"];
        _ = localizer["Manage Onboarding Tasks"];
        _ = localizer["Manage Password Shares"];
        _ = localizer["Manage Printing"];
        _ = localizer["Manage Questions"];
        _ = localizer["Not Found"];
        _ = localizer["Password Details"];
        _ = localizer["Passwords Management"];
        _ = localizer["Payroll Details"];
        _ = localizer["Permission Details"];
        _ = localizer["Questionnaire Responses"];
        _ = localizer["Register"];
        _ = localizer["Response Detail"];
        _ = localizer["SSH Keys"];

        _ = localizer["Issue Invoice"];

        _ = localizer["Manage Intangible Assets"];

        _ = localizer["Company Intangible Assets"];

        _ = localizer["Customer Relations"];
        _ = localizer["Customer Relationships"];
        _ = localizer["Edit Customer Relationship"];
        _ = localizer["Manage Customer Relations"];
        _ = localizer["View Customer Relations"];

        _ = localizer["Manage Market Channels"];
        _ = localizer["Market Channels"];
        _ = localizer["View Market Channels"];
    
        _ = localizer["Holiday Adjustments"];
    
        _ = localizer["Preview"];
    
        _ = localizer["My Reimbursements"];
        _ = localizer["Reimbursement"];
    
        _ = localizer["Collection Channels"];
        _ = localizer["Create New Blueprint Folder"];
        _ = localizer["Create New Folder"];
        _ = localizer["Edit Blueprint Folder"];
        _ = localizer["Edit Folder"];
    
        _ = localizer["Contract Finance Details"];
        _ = localizer["Contract Finance Statistics"];
    
        _ = localizer["AI"];
        _ = localizer["AI Assistant"];
        _ = localizer["Company Info Consultation"];
    
        _ = localizer["Apply Leave"];
        _ = localizer["Incoming Leave"];
        _ = localizer["Leave"];
    
        _ = localizer["Accounts"];
        _ = localizer["Assign"];
        _ = localizer["Assign Asset"];
        _ = localizer["Create"];
        _ = localizer["Create Account"];
        _ = localizer["Create Asset"];
        _ = localizer["Create Server"];
        _ = localizer["Create Service"];
        _ = localizer["Create Transaction"];
        _ = localizer["Details"];
        _ = localizer["Edit"];
        _ = localizer["Edit Account"];
        _ = localizer["Edit Asset"];
        _ = localizer["Edit Question"];
        _ = localizer["Edit Questionnaire"];
        _ = localizer["Edit Server"];
        _ = localizer["Edit Service"];
        _ = localizer["Edit Transaction"];
        _ = localizer["Editor"];
        _ = localizer["Index Server"];
        _ = localizer["Manage Categories"];
        _ = localizer["Manage Dns Providers"];
        _ = localizer["Manage Locations"];
        _ = localizer["Manage Models"];
        _ = localizer["Manage Providers"];
        _ = localizer["Manage Vendors"];
        _ = localizer["Ocr Results"];
        _ = localizer["Print"];
        _ = localizer["Transactions"];
    
        _ = localizer["Import Contracts from Zip"];
    
        _ = localizer["Audio"];
        _ = localizer["Edit Meeting Recording"];
        _ = localizer["Edit Transcript"];
        _ = localizer["Manage Audio Shares"];
        _ = localizer["Meeting Transcript"];
        _ = localizer["Meeting Transcripts"];
        _ = localizer["Meeting Minutes"];
        _ = localizer["Rename Meeting Recording"];
        _ = localizer["Upload Audio"];
    
        _ = localizer["Move Contract"];
    
        _ = localizer["DNS Audit"];
    
        _ = localizer["Services Dashboard"];
    }

    public void InjectSimple(
        HttpContext context,
        UiStackLayoutViewModel toInject)
    {
        toInject.PageTitle = localizer[toInject.PageTitle ?? "View"];
        toInject.AppName = globalSettingsService.GetSettingValueAsync(SettingsMap.ProjectName).GetAwaiter().GetResult();
        toInject.Theme = UiTheme.Light;
        toInject.SidebarTheme = UiSidebarTheme.Default;
        toInject.Layout = UiLayout.Fluid;
        toInject.ContentNoPadding = true;
    }

    public void Inject(
        HttpContext context,
        UiStackLayoutViewModel toInject)
    {
        var preferDarkTheme = context.Request.Cookies[ThemeController.ThemeCookieKey] == true.ToString();
        var projectName = globalSettingsService.GetSettingValueAsync(SettingsMap.ProjectName).GetAwaiter().GetResult();
        var brandName = globalSettingsService.GetSettingValueAsync(SettingsMap.BrandName).GetAwaiter().GetResult();
        var brandHomeUrl = globalSettingsService.GetSettingValueAsync(SettingsMap.BrandHomeUrl).GetAwaiter().GetResult();
        toInject.PageTitle = localizer[toInject.PageTitle ?? "View"];
        toInject.AppName = projectName;
        toInject.Theme = preferDarkTheme ? UiTheme.Dark : UiTheme.Light;
        toInject.SidebarTheme = preferDarkTheme ? UiSidebarTheme.Dark : UiSidebarTheme.Default;
        toInject.Layout = UiLayout.Fluid;
        toInject.FooterMenu = new FooterMenuViewModel
        {
            AppBrand = new Link { Text = brandName, Href = brandHomeUrl },
            Links =
            [
                new Link { Text = localizer["Home"], Href = "/" },
                new Link { Text = "Aiursoft", Href = "https://www.aiursoft.com" },
            ]
        };
        toInject.Navbar = new NavbarViewModel
        {
            ThemeSwitchApiCallEndpoint = "/api/switch-theme"
        };

        var currentViewingController = context.GetRouteValue("controller")?.ToString();
        var navGroupsForView = new List<NavGroup>();

        foreach (var groupDef in navigationState.NavMap)
        {
            var itemsForView = new List<CascadedSideBarItem>();
            foreach (var itemDef in groupDef.Items)
            {
                var linksForView = new List<CascadedLink>();
                foreach (var linkDef in itemDef.Links)
                {
                    bool isVisible;
                    if (string.IsNullOrEmpty(linkDef.RequiredPolicy))
                    {
                        isVisible = true;
                    }
                    else
                    {
                        var authResult = authorizationService.AuthorizeAsync(context.User, linkDef.RequiredPolicy).Result;
                        isVisible = authResult.Succeeded;
                    }

                    if (isVisible)
                    {
                        linksForView.Add(new CascadedLink
                        {
                            Href = linkDef.Href,
                            Text = localizer[linkDef.Text]
                        });
                    }
                }

                if (linksForView.Any())
                {
                    itemsForView.Add(new CascadedSideBarItem
                    {
                        UniqueId = itemDef.UniqueId,
                        Text = localizer[itemDef.Text],
                        LucideIcon = itemDef.Icon,
                        IsActive = linksForView.Any(l =>
                        {
                            // Extract controller name from href (e.g., "/Manage/Index" -> "Manage")
                            var hrefController = l.Href.TrimStart('/').Split('/').FirstOrDefault();
                            // Exact match to avoid false positives like "Manage" matching "ManagePayroll"
                            return string.Equals(hrefController, currentViewingController, StringComparison.OrdinalIgnoreCase);
                        }),
                        Links = linksForView
                    });
                }
            }

            if (itemsForView.Any())
            {
                navGroupsForView.Add(new NavGroup
                {
                    Name = localizer[groupDef.Name],
                    Items = itemsForView.Select(t => (SideBarItem)t).ToList()
                });
            }
        }

        toInject.Sidebar = new SidebarViewModel
        {
            SideLogo = new SideLogoViewModel
            {
                AppName = projectName,
                LogoUrl = GetLogoUrl(context).GetAwaiter().GetResult(),
                Href = "/"
            },
            SideMenu = new SideMenuViewModel
            {
                Groups = navGroupsForView
            }
        };

        var currentCulture = context.Features
            .Get<IRequestCultureFeature>()?
            .RequestCulture.Culture.Name; // zh-CN

        // ReSharper disable once RedundantNameQualifier
        var suppportedCultures = Aiursoft.WebTools.OfficialPlugins.LocalizationPlugin.SupportedCultures
            .Select(c => new LanguageSelection
            {
                Link = $"/Culture/Set?culture={c.Key}&returnUrl={context.Request.Path}",
                Name = c.Value // 中文 - 中国
            })
            .ToArray();

        // ReSharper disable once RedundantNameQualifier
        toInject.Navbar.LanguagesDropdown = new LanguagesDropdownViewModel
        {
            Languages = suppportedCultures,
            SelectedLanguage = new LanguageSelection
            {
                Name = Aiursoft.WebTools.OfficialPlugins.LocalizationPlugin.SupportedCultures[currentCulture ?? "en-US"],
                Link = "#",
            }
        };

        if (signInManager.IsSignedIn(context.User))
        {
            var avatarPath = context.User.Claims.First(c => c.Type == UserClaimsPrincipalFactory.AvatarClaimType)
                .Value;
            toInject.Navbar.UserDropdown = new UserDropdownViewModel
            {
                UserName = context.User.Claims.First(c => c.Type == UserClaimsPrincipalFactory.DisplayNameClaimType).Value,
                UserAvatarUrl = $"{storageService.RelativePathToInternetUrl(avatarPath)}?w=100&square=true",
                IconLinkGroups =
                [
                    new IconLinkGroup
                    {
                        Links =
                        [
                            new IconLink { Icon = "user", Text = localizer["Profile"], Href = "/Manage" },
                        ]
                    },
                    new IconLinkGroup
                    {
                        Links =
                        [
                            new IconLink { Icon = "log-out", Text = localizer["Sign out"], Href = "/Account/Logoff" }
                        ]
                    }
                ]
            };
        }
        else
        {
            toInject.Sidebar.SideAdvertisement = new SideAdvertisementViewModel
            {
                Title = localizer["Login"],
                Description = localizer["Login to get access to all features."],
                Href = "/Account/Login",
                ButtonText = localizer["Login"]
            };

            var allowRegister = appSettings.Value.Local.AllowRegister;
            var links = new List<IconLink>
            {
                new()
                {
                    Text = localizer["Login"],
                    Href = "/Account/Login",
                    Icon = "user"
                }
            };
            if (allowRegister && appSettings.Value.LocalEnabled)
            {
                links.Add(new IconLink
                {
                    Text = localizer["Register"],
                    Href = "/Account/Register",
                    Icon = "user-plus"
                });
            }
            toInject.Navbar.UserDropdown = new UserDropdownViewModel
            {
                UserName = localizer["Click to login"],
                UserAvatarUrl = string.Empty,
                IconLinkGroups =
                [
                    new IconLinkGroup
                    {
                        Links = links.ToArray()
                    }
                ]
            };
        }
    }


    private async Task<string> GetLogoUrl(HttpContext context)
    {
        var logoPath = await globalSettingsService.GetSettingValueAsync(SettingsMap.ProjectLogo);
        if (string.IsNullOrWhiteSpace(logoPath))
        {
            return "/logo.svg";
        }
        return storageService.RelativePathToInternetUrl(logoPath, context);
    }
}
