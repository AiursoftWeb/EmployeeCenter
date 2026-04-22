namespace Aiursoft.EmployeeCenter.Models.ProjectsViewModels;

public class GitLabProject
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string WebUrl { get; set; } = string.Empty;
    public string HttpUrlToRepo { get; set; } = string.Empty;
    public string SshUrlToRepo { get; set; } = string.Empty;
    public bool Archived { get; set; }
    public List<string> Topics { get; set; } = new();
    public List<Badge> Badges { get; set; } = new();
    public string DefaultBranch { get; set; } = string.Empty;
    public bool IsStarredByRequiredUser { get; set; }
    public bool IsMirroredOnGitHub { get; set; }
}
