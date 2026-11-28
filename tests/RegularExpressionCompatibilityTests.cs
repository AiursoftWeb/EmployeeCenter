using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Aiursoft.EmployeeCenter.Models.QualificationsViewModels;

namespace Aiursoft.EmployeeCenter.Tests;

[TestClass]
public sealed class RegularExpressionCompatibilityTests
{
    [TestMethod]
    public void AllRegularExpressionAttributesAreJavaScriptCompatible()
    {
        var appAssembly = typeof(Startup).Assembly;
        var assemblies = new[] { appAssembly }
            .Concat(appAssembly.GetReferencedAssemblies()
                .Where(name => name.Name?.StartsWith("Aiursoft.EmployeeCenter", StringComparison.Ordinal) == true)
                .Select(Assembly.Load))
            .Distinct()
            .ToArray();

        var violations = new List<string>();
        var checkedCount = 0;

        foreach (var assembly in assemblies)
        {
            foreach (var type in assembly.GetTypes())
            {
                foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    foreach (var attr in property.GetCustomAttributes<RegularExpressionAttribute>(inherit: false))
                    {
                        checkedCount++;
                        var issues = FindJavaScriptIncompatibilities(attr.Pattern);
                        if (issues.Count > 0)
                        {
                            violations.Add($"{type.FullName}.{property.Name}: '{attr.Pattern}' -> {string.Join("; ", issues)}");
                        }
                    }
                }
            }
        }

        Assert.IsGreaterThan(0, checkedCount, "Expected to inspect at least one [RegularExpression] attribute.");
        Assert.AreEqual(0, violations.Count,
            "Found [RegularExpression] attributes using .NET-only or non-universal JavaScript syntax " +
            "(ASP.NET Core unobtrusive validation runs new RegExp(pattern) without the /u flag):\n" +
            string.Join('\n', violations));
    }

    [TestMethod]
    [DataRow(@"[^\p{Cc}]+", true)]
    [DataRow(@"\P{L}+", true)]
    [DataRow(@"^test\.(?i:png|jpg)$", true)]
    [DataRow(@"(?i)^abc$", true)]
    [DataRow(@"(?>abc)+", true)]
    [DataRow(@"\Aabc\Z", true)]
    [DataRow(@"a++", true)]
    [DataRow(@"[a-z-[aeiou]]+", true)]
    [DataRow(@"[^\x00-\x1F\x7F-\x9F]+", false)]
    [DataRow(@"^avatar.*", false)]
    [DataRow(@"^qualifications/uploads/[a-f0-9]{64}/(?:[a-f0-9]{32}/)?[^/\\]+\.(?:[pP][nN][gG]|[jJ][pP][eE]?[gG])$", false)]
    public void DetectorIdentifiesJavaScriptIncompatibleRegexConstructs(string pattern, bool expectViolation)
    {
        var issues = FindJavaScriptIncompatibilities(pattern);
        Assert.AreEqual(expectViolation, issues.Count > 0,
            $"Pattern '{pattern}' issues: [{string.Join(", ", issues)}]");
    }

    [TestMethod]
    public void QualificationImagePathsAcceptCaseInsensitiveExtensions()
    {
        var hash = new string('a', 64);
        foreach (var ext in new[] { "png", "PNG", "jpg", "JPG", "jpeg", "JPEG", "Png" })
        {
            var createVm = new CreateViewModel
            {
                CompanyEntityId = 1,
                Name = "ISO 9001",
                Version = "2026",
                OriginalPath = $"qualifications/uploads/{hash}/cert.{ext}"
            };
            Assert.IsTrue(
                Validator.TryValidateObject(createVm, new ValidationContext(createVm), new List<ValidationResult>(), validateAllProperties: true),
                $"CreateViewModel should accept .{ext}");

            var detectVm = new DetectViewModel
            {
                ImagePath = $"qualifications/detections/{hash}/sample.{ext}"
            };
            Assert.IsTrue(
                Validator.TryValidateObject(detectVm, new ValidationContext(detectVm), new List<ValidationResult>(), validateAllProperties: true),
                $"DetectViewModel should accept .{ext}");
        }

        var invalidDetect = new DetectViewModel { ImagePath = $"qualifications/detections/{hash}/sample.gif" };
        Assert.IsFalse(Validator.TryValidateObject(invalidDetect, new ValidationContext(invalidDetect), new List<ValidationResult>(), validateAllProperties: true));
    }

    private static List<string> FindJavaScriptIncompatibilities(string pattern)
    {
        var issues = new List<string>();
        try
        {
            _ = new Regex(pattern);
        }
        catch (ArgumentException ex)
        {
            issues.Add($"Invalid .NET regex: {ex.Message}");
            return issues;
        }

        var inCharClass = false;
        for (var i = 0; i < pattern.Length; i++)
        {
            var c = pattern[i];
            if (c == '\\')
            {
                if (i + 1 >= pattern.Length)
                {
                    break;
                }

                var next = pattern[i + 1];
                if ((next is 'p' or 'P') && i + 2 < pattern.Length && pattern[i + 2] == '{')
                {
                    issues.Add(@"Unicode property escape \p{...}/\P{...} breaks non-Unicode JS RegExp");
                }
                else if (!inCharClass && next is 'A' or 'Z' or 'z' or 'G')
                {
                    issues.Add($@".NET-only anchor \{next} is treated as a literal character in JS RegExp");
                }

                i++;
                continue;
            }

            if (inCharClass)
            {
                if (c == ']')
                {
                    inCharClass = false;
                }
                else if (c == '-' && i + 1 < pattern.Length && pattern[i + 1] == '[')
                {
                    issues.Add(".NET character class subtraction '-[...]' is not supported in standard JS RegExp");
                }

                continue;
            }

            if (c == '[')
            {
                inCharClass = true;
                continue;
            }

            if (c == '(' && i + 2 < pattern.Length && pattern[i + 1] == '?')
            {
                var afterQ = pattern[i + 2];
                if (afterQ == '>')
                {
                    issues.Add("Atomic group '(?>...)' is not supported in JS RegExp");
                }
                else if (afterQ is 'i' or 'm' or 'n' or 's' or 'x' or '-')
                {
                    issues.Add("Inline regex option modifier '(?i...)' is not universally supported in JS RegExp");
                }
            }
            else if (c is '+' or '*' or '?' or '}' && i + 1 < pattern.Length && pattern[i + 1] == '+')
            {
                if (c != '?' || (i > 0 && pattern[i - 1] != '('))
                {
                    issues.Add($"Possessive quantifier '{c}+' is not supported in JS RegExp");
                }
            }
        }

        return issues.Distinct().ToList();
    }
}
