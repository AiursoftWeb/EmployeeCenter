using Aiursoft.EmployeeCenter.Models;

namespace Aiursoft.EmployeeCenter.Configuration;

public class SettingsMap
{
    public const string ProjectName = "ProjectName";
    public const string BrandName = "BrandName";
    public const string BrandHomeUrl = "BrandHomeUrl";
    public const string ProjectLogo = "ProjectLogo";
    public const string AllowUserAdjustNickname = "Allow_User_Adjust_Nickname";
    public const string Icp = "Icp";
    public const string AnnualLeavePerYear = "Annual_Leave_Per_Year";
    public const string DefaultPayrollCurrency = "Default_Payroll_Currency";
    public const string ForceProjectAssociation = "Force_Project_Association";
    public const string GitLabOrganizationUrl = "GitLab_Organization_Url";
    public const string GitLabProjectMustBeStaredBy = "GitLab_Project_Must_Be_Stared_By";
    public const string GitLabEnsureGitHubOrgMirrored = "GitLab_Ensure_GitHub_Org_Mirrored";
    public const string GitLabGitHubToken = "GitLab_GitHub_Token";
    public const string AiAssistantSystemPrompt = "Ai_Assistant_System_Prompt";

    public class FakeLocalizer
    {
        public string this[string name] => name;
    }

    private static readonly FakeLocalizer Localizer = new();

    public static readonly List<GlobalSettingDefinition> Definitions = new()
    {
        new GlobalSettingDefinition
        {
            Key = ProjectName,
            Name = Localizer["Project Name"],
            Description = Localizer["The name of the project displayed in the frontend."],
            Type = SettingType.Text,
            DefaultValue = "Aiursoft EmployeeCenter"
        },
        new GlobalSettingDefinition
        {
            Key = BrandName,
            Name = Localizer["Brand Name"],
            Description = Localizer["The brand name of the company or project. E.g. Aiursoft."],
            Type = SettingType.Text,
            DefaultValue = "Aiursoft"
        },
        new GlobalSettingDefinition
        {
            Key = BrandHomeUrl,
            Name = Localizer["Brand Home URL"],
            Description = Localizer["The URL of the company or project. E.g. https://www.aiursoft.com"],
            Type = SettingType.Text,
            DefaultValue = "https://www.aiursoft.com"
        },
        new GlobalSettingDefinition
        {
            Key = ProjectLogo,
            Name = Localizer["Project Logo"],
            Description = Localizer["The logo of the project displayed in the navbar and footer. Support jpg, png, svg."],
            Type = SettingType.File,
            DefaultValue = "",
            Subfolder = "project-logo",
            AllowedExtensions = "jpg png svg",
            MaxSizeInMb = 5
        },
        new GlobalSettingDefinition
        {
            Key = AllowUserAdjustNickname,
            Name = Localizer["Allow User Adjust Nickname"],
            Description = Localizer["Allow users to adjust their nickname in the profile management page."],
            Type = SettingType.Bool,
            DefaultValue = "True"
        },
        new GlobalSettingDefinition
        {
            Key = Icp,
            Name = Localizer["ICP Number"],
            Description = Localizer["The ICP license number for China mainland users. Leave empty to hide."],
            Type = SettingType.Text,
            DefaultValue = ""
        },
        new GlobalSettingDefinition
        {
            Key = AnnualLeavePerYear,
            Name = Localizer["Annual Leave Per Year"],
            Description = Localizer["The number of paid annual leave days allocated to each employee every year."],
            Type = SettingType.Number,
            DefaultValue = "12"
        },
        new GlobalSettingDefinition
        {
            Key = DefaultPayrollCurrency,
            Name = Localizer["Default Payroll Currency"],
            Description = Localizer["The default currency to use when issuing a new payroll."],
            Type = SettingType.Choice,
            DefaultValue = "CNY",
            ChoiceOptions = new Dictionary<string, string>
            {
                { "CNY", "人民币 (CNY)" },
                { "JPY", "日元 (JPY)" },
                { "HKD", "港币 (HKD)" },
                { "USD", "美元 (USD)" }
            }
        },
        new GlobalSettingDefinition
        {
            Key = ForceProjectAssociation,
            Name = Localizer["Force Project Association"],
            Description = Localizer["Require employees to associate at least one project when submitting a weekly report."],
            Type = SettingType.Bool,
            DefaultValue = "False"
        },
        new GlobalSettingDefinition
        {
            Key = GitLabOrganizationUrl,
            Name = Localizer["GitLab Organization URL"],
            Description = Localizer["The URL of the GitLab organization to fetch projects from. E.g. https://gitlab.aiursoft.com/aiursoft"],
            Type = SettingType.Text,
            DefaultValue = ""
        },
        new GlobalSettingDefinition
        {
            Key = GitLabProjectMustBeStaredBy,
            Name = Localizer["GitLab Project Must Be Stared By"],
            Description = Localizer["Only projects starred by this user will be displayed. Leave empty to show all projects. E.g. anduin"],
            Type = SettingType.Text,
            DefaultValue = ""
        },
        new GlobalSettingDefinition
        {
            Key = GitLabEnsureGitHubOrgMirrored,
            Name = Localizer["GitLab Ensure GitHub Org Mirrored"],
            Description = Localizer["Check if the project is mirrored to this GitHub organization. Leave empty to disable check. E.g. aiursoftweb"],
            Type = SettingType.Text,
            DefaultValue = ""
        },
        new GlobalSettingDefinition
        {
            Key = GitLabGitHubToken,
            Name = Localizer["GitLab GitHub Token"],
            Description = Localizer["Optional GitHub personal access token to increase API rate limits for mirroring checks."],
            Type = SettingType.Text,
            DefaultValue = ""
        },
        new GlobalSettingDefinition
        {
            Key = AiAssistantSystemPrompt,
            Name = Localizer["AI Assistant System Prompt"],
            Description = Localizer["The system prompt for the AI assistant."],
            Type = SettingType.Text,
            DefaultValue = "你是这个员工中心的专业AI助手。您的职责是帮助员工解答问题，并提供有关公司政策和流程的信息。相关信息都已经放在了当前目录下你可以审阅。但是您**没有**权限修改任何数据或代表用户执行任何操作。请提供有用且准确的信息。请检索相关文件，搜索上下文，结合公司现状，回答员工的问题。请使用 {{LANG}} 回答。"
        }
    };
}
