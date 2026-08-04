namespace Aiursoft.EmployeeCenter.Tests;

[TestClass]
public partial class ViewStyleConstraintTests
{
    [TestMethod]
    public void RazorViews_DoNotUseTextDarkClassOutsideBadges()
    {
        var repositoryRoot = FindRepositoryRoot();
        var viewsPath = Path.Combine(repositoryRoot, "src", "Aiursoft.EmployeeCenter", "Views");
        var violatingFiles = Directory
            .EnumerateFiles(viewsPath, "*.cshtml", SearchOption.AllDirectories)
            .Where(file => ClassAttributeRegex()
                .Matches(File.ReadAllText(file))
                .Any(match =>
                {
                    var classes = match.Groups["classes"].Value;
                    return TextDarkClassRegex().IsMatch(classes) && !BadgeClassRegex().IsMatch(classes);
                }))
            .Select(file => Path.GetRelativePath(repositoryRoot, file))
            .OrderBy(file => file)
            .ToArray();

        Assert.AreEqual(
            0,
            violatingFiles.Length,
            $"The following Razor views use the forbidden text-dark class outside badges:{Environment.NewLine}{string.Join(Environment.NewLine, violatingFiles)}");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Aiursoft.EmployeeCenter.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find the repository root containing Aiursoft.EmployeeCenter.sln.");
    }

    [GeneratedRegex(@"(?<![A-Za-z0-9_-])text-dark(?![A-Za-z0-9_-])")]
    private static partial Regex TextDarkClassRegex();

    [GeneratedRegex(@"(?<![A-Za-z0-9_-])badge(?![A-Za-z0-9_-])")]
    private static partial Regex BadgeClassRegex();

    [GeneratedRegex("""\bclass\s*=\s*(?<quote>["'])(?<classes>.*?)\k<quote>""", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ClassAttributeRegex();
}
