using Aiursoft.Canon;
using Aiursoft.EmployeeCenter.Configuration;
using Aiursoft.EmployeeCenter.Models.ProjectsViewModels;
using Aiursoft.Scanner.Abstractions;
using Newtonsoft.Json;

namespace Aiursoft.EmployeeCenter.Services.GitLab;

public class GitLabService : IScopedDependency
{
    private readonly HttpClient _httpClient;
    private readonly GlobalSettingsService _globalSettings;
    private readonly CanonPool _canonPool;
    private readonly CacheService _cache;

    public GitLabService(
        HttpClient httpClient,
        GlobalSettingsService globalSettings,
        CanonPool canonPool,
        CacheService cache)
    {
        _httpClient = httpClient;
        _globalSettings = globalSettings;
        _canonPool = canonPool;
        _cache = cache;
    }

    public async Task<List<GitLabProject>> GetAllProjectsAsync()
    {
        var organizationUrl = await _globalSettings.GetSettingValueAsync(SettingsMap.GitLabOrganizationUrl);
        var projectMustBeStaredBy = await _globalSettings.GetSettingValueAsync(SettingsMap.GitLabProjectMustBeStaredBy);
        var ensureGitHubOrgMirrored = await _globalSettings.GetSettingValueAsync(SettingsMap.GitLabEnsureGitHubOrgMirrored);
        var githubToken = await _globalSettings.GetSettingValueAsync(SettingsMap.GitLabGitHubToken);

        if (string.IsNullOrWhiteSpace(organizationUrl))
        {
            return new List<GitLabProject>();
        }

        // Parse organization URL to get the GitLab base URL and organization name
        var orgUrl = organizationUrl.TrimEnd('/');
        var segments = orgUrl.Split('/');
        var orgName = segments[^1];
        var baseUrl = string.Join('/', segments[0..^1]);

        // Get the user ID for the configured username
        var userId = await GetUserIdByUsernameAsync(baseUrl, projectMustBeStaredBy);

        // Get group ID by name
        var groupSearchUrl = $"{baseUrl}/api/v4/groups?search={orgName}";
        var groupResponse = await _httpClient.GetStringAsync(groupSearchUrl);
        var groups = JsonConvert.DeserializeObject<List<GitLabGroup>>(groupResponse);

        if (groups == null || groups.Count == 0)
        {
            return new List<GitLabProject>();
        }

        var groupId = groups[0].Id;

        // Get all projects in the group
        var projectsUrl = $"{baseUrl}/api/v4/groups/{groupId}/projects?simple=true&per_page=999&visibility=public&page=1";
        var projectsResponse = await _httpClient.GetStringAsync(projectsUrl);
        var projectsDto = JsonConvert.DeserializeObject<List<GitLabProjectDto>>(projectsResponse) ?? new List<GitLabProjectDto>();

        // Filter out archived projects
        var activeProjects = projectsDto.Where(p => !p.Archived).ToList();

        var resultProjects = new List<GitLabProject>();

        // Register tasks to the pool instead of creating a list of tasks
        foreach (var projectDto in activeProjects)
        {
            _canonPool.RegisterNewTaskToPool(async () =>
            {
                var project = new GitLabProject
                {
                    Id = projectDto.Id,
                    Name = projectDto.Name,
                    Description = projectDto.Description ?? string.Empty,
                    WebUrl = projectDto.WebUrl,
                    HttpUrlToRepo = projectDto.HttpUrlToRepo,
                    SshUrlToRepo = projectDto.SshUrlToRepo,
                    Archived = projectDto.Archived,
                    DefaultBranch = projectDto.DefaultBranch,
                    Topics = projectDto.Topics ?? new List<string>()
                };

                try
                {
                    var readmeUrl = $"{baseUrl}/api/v4/projects/{projectDto.Id}/repository/files/README.md/raw?ref={projectDto.DefaultBranch}";
                    var readmeContent = await _httpClient.GetStringAsync(readmeUrl);
                    project.Badges = ExtractBadges(readmeContent);
                }
                catch
                {
                    // Ignore if README not found or other errors
                }

                // Check if the project is starred by the required user
                if (userId.HasValue)
                {
                    project.IsStarredByRequiredUser = await IsProjectStarredByUserAsync(baseUrl, projectDto.Id, userId.Value);
                }

                // Check if the project is mirrored on GitHub
                if (!string.IsNullOrEmpty(ensureGitHubOrgMirrored))
                {
                    project.IsMirroredOnGitHub = await IsProjectMirroredOnGitHubAsync(ensureGitHubOrgMirrored, projectDto.Name, githubToken);
                }

                lock (resultProjects)
                {
                    resultProjects.Add(project);
                }
            });
        }

        // Execute tasks in pool with controlled concurrency (default 8, prevents API overload)
        await _canonPool.RunAllTasksInPoolAsync();

        return resultProjects;
    }

    private List<Badge> ExtractBadges(string markdown)
    {
        var badges = new List<Badge>();
        var lines = markdown.Split('\n');
        // Regex for linked image: [![Alt](ImgUrl)](LinkUrl)
        var linkedImageRegex = new System.Text.RegularExpressions.Regex(@"^\[!\[(.*?)\]\((.*?)\)\]\((.*?)\)");
        // Regex for image: ![Alt](ImgUrl)
        var imageRegex = new System.Text.RegularExpressions.Regex(@"^!\[(.*?)\]\((.*?)\)");

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmedLine)) continue;
            if (trimmedLine.StartsWith("#")) continue; // Skip headers

            // Attempt to match linked image first
            var linkedMatch = linkedImageRegex.Match(trimmedLine);
            if (linkedMatch.Success)
            {
                badges.Add(new Badge
                {
                    AltText = linkedMatch.Groups[1].Value,
                    ImageUrl = linkedMatch.Groups[2].Value,
                    LinkUrl = linkedMatch.Groups[3].Value
                });
                continue;
            }

            // Attempt to match simple image
            var imageMatch = imageRegex.Match(trimmedLine);
            if (imageMatch.Success)
            {
                badges.Add(new Badge
                {
                    AltText = imageMatch.Groups[1].Value,
                    ImageUrl = imageMatch.Groups[2].Value
                });
                continue;
            }

            // Detect end of badge section: a line that is not a badge?
            // The prompt says "continuous badge part".
            // If the line has content but is not a badge, we stop assuming the badge section is over.
            // But sometimes badges are inline. The user example shows one per line basically or same line.
            // If the line doesn't start with ! or [, it's likely text.
            if (!trimmedLine.StartsWith("[") && !trimmedLine.StartsWith("!"))
            {
                // If we already found badges, break.
                if (badges.Count > 0)
                {
                    break;
                }
            }
        }
        return badges;
    }

    public List<ProjectsByTag> GroupProjectsByFirstTag(List<GitLabProject> projects)
    {
        var grouped = projects
            .GroupBy(p => p.Topics.FirstOrDefault() ?? "no-topic")
            .Select(g => new ProjectsByTag
            {
                Tag = g.Key,
                Projects = g.ToList()
            })
            .OrderBy(g => g.Tag)
            .ToList();

        return grouped;
    }

    private async Task<int?> GetUserIdByUsernameAsync(string baseUrl, string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return null;
        }

        try
        {
            var userSearchUrl = $"{baseUrl}/api/v4/users?username={username}";
            var userResponse = await _httpClient.GetStringAsync(userSearchUrl);
            var users = JsonConvert.DeserializeObject<List<GitLabUser>>(userResponse);

            if (users != null && users.Count > 0)
            {
                return users[0].Id;
            }
        }
        catch
        {
            // Ignore if user not found or other errors
        }

        return null;
    }

    private async Task<bool> IsProjectStarredByUserAsync(string baseUrl, int projectId, int userId)
    {
        try
        {
            var starrersUrl = $"{baseUrl}/api/v4/projects/{projectId}/starrers";
            var starrersResponse = await _httpClient.GetStringAsync(starrersUrl);
            var starrers = JsonConvert.DeserializeObject<List<GitLabStarrer>>(starrersResponse);

            return starrers?.Any(s => s.User?.Id == userId) ?? false;
        }
        catch
        {
            // If we can't fetch starrers, assume not starred
            return false;
        }
    }

    private async Task<bool> IsProjectMirroredOnGitHubAsync(string githubOrg, string projectName, string? githubToken)
    {
        // Use cache to avoid hitting GitHub API rate limits
        // Cache key is unique per org and project
        var cacheKey = $"github-mirror-{githubOrg}-{projectName}";

        return await _cache.RunWithCache(cacheKey, async () =>
        {
            try
            {
                // Check if a GitHub repository exists with the same name in the specified organization
                // Convert to lowercase as GitHub repository names are case-sensitive and typically lowercase
                var repoName = projectName.ToLowerInvariant();
                var githubApiUrl = $"https://api.github.com/repos/{githubOrg}/{repoName}";
                var request = new HttpRequestMessage(HttpMethod.Head, githubApiUrl);
                request.Headers.Add("User-Agent", "EmployeeCenter-GitLab-Checker");

                // Add GitHub token if configured (increases rate limit from 60/hr to 5000/hr)
                if (!string.IsNullOrEmpty(githubToken))
                {
                    request.Headers.Add("Authorization", $"Bearer {githubToken}");
                }

                var response = await _httpClient.SendAsync(request);

                // Log when we hit rate limits
                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    Console.WriteLine($"[GitHub API] Rate limit or forbidden for {githubOrg}/{repoName} (will be cached)");
                    // If rate limited, we can't determine - return false to avoid false positives
                    return false;
                }

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[GitHub API] Failed to check {githubOrg}/{repoName}: {response.StatusCode} (will be cached)");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GitHub API] Exception checking {githubOrg}/{projectName}: {ex.Message}");
                // If we can't fetch the GitHub repo, assume not mirrored
                return false;
            }
        },
        cachedMinutes: _ => TimeSpan.FromHours(1)); // Cache for 1 hour
    }

    // Internal DTOs for GitLab API responses
    private class GitLabGroup
    {
        public int Id { get; set; }
    }

    private class GitLabUser
    {
        public int Id { get; set; }
    }

    private class GitLabStarrer
    {
        public GitLabUser? User { get; set; }
    }

    private class GitLabProjectDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }

        [JsonProperty("web_url")]
        public string WebUrl { get; set; } = string.Empty;

        [JsonProperty("http_url_to_repo")]
        public string HttpUrlToRepo { get; set; } = string.Empty;

        [JsonProperty("ssh_url_to_repo")]
        public string SshUrlToRepo { get; set; } = string.Empty;

        public bool Archived { get; set; }
        public List<string>? Topics { get; set; }

        [JsonProperty("default_branch")]
        public string DefaultBranch { get; set; } = "master";
    }
}

