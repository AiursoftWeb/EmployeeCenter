using System.Text.RegularExpressions;

namespace Aiursoft.EmployeeCenter.Tests;

[TestClass]
public partial class ViewStyleConstraintTests
{
    [TestMethod]
    public void RazorViews_DoNotUseTextDarkClass()
    {
        var repositoryRoot = FindRepositoryRoot();
        var viewsPath = Path.Combine(repositoryRoot, "src", "Aiursoft.EmployeeCenter", "Views");
        var violatingFiles = Directory
            .EnumerateFiles(viewsPath, "*.cshtml", SearchOption.AllDirectories)
            .Where(file => TextDarkClassRegex().IsMatch(File.ReadAllText(file)))
            .Select(file => Path.GetRelativePath(repositoryRoot, file))
            .OrderBy(file => file)
            .ToArray();

        Assert.AreEqual(
            0,
            violatingFiles.Length,
            $"The following Razor views use the forbidden text-dark class:{Environment.NewLine}{string.Join(Environment.NewLine, violatingFiles)}");
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
}
