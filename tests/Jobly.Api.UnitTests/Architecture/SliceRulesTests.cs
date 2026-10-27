using System.Text.RegularExpressions;

namespace Jobly.Api.UnitTests.Architecture;

// With one project the compiler no longer stops a feature from using another feature's code, so this test does.
// It reads the source files and checks the slice rules in .claude/rules/code-style.md.
public sealed partial class SliceRulesTests
{
    // Which other slices each slice may use. Matches compares a job with the CV, so it needs both. The others need nothing.
    private static readonly Dictionary<string, string[]> AllowedSlices = new()
    {
        ["Jobs"] = [],
        ["Cvs"] = [],
        ["Matches"] = ["Jobs", "Cvs"],
    };

    [GeneratedRegex(@"Jobly\.Api\.Features\.(\w+)")]
    private static partial Regex FeatureReference();

    [Fact]
    public void Slices_OnlyReferenceAllowedSlices()
    {
        // Arrange
        var featuresFolder = Path.Combine(FindRepositoryRoot(), "src", "Jobly.Api", "Features");
        var files = Directory.GetFiles(featuresFolder, "*.cs", SearchOption.AllDirectories)
            .Select(path => (Slice: Path.GetRelativePath(featuresFolder, path).Split(Path.DirectorySeparatorChar)[0], Name: Path.GetFileName(path), Text: File.ReadAllText(path)))
            .ToList();

        // Act
        var violations = files.SelectMany(file => FindViolations(file.Slice, file.Name, file.Text)).ToList();

        // Assert: the message lists every offending file, so the fix is obvious.
        Assert.Empty(violations);
        Assert.Contains(files, file => file.Slice == "Jobs"); // Guards against checking an empty or wrong folder.
    }

    [Fact]
    public void FindViolations_JobsUsesCvs_ReportsIt()
    {
        // Arrange
        const string text = "using Jobly.Api.Features.Cvs;\nnamespace Jobly.Api.Features.Jobs;";

        // Act
        var violations = FindViolations("Jobs", "JobService.cs", text);

        // Assert
        Assert.Single(violations);
        Assert.Contains("Cvs", violations[0], StringComparison.Ordinal);
    }

    [Fact]
    public void FindViolations_MatchesUsesJobsAndCvs_ReportsNothing()
    {
        // Arrange
        const string text = "using Jobly.Api.Features.Jobs;\nusing Jobly.Api.Features.Cvs;\nnamespace Jobly.Api.Features.Matches;";

        // Act
        var violations = FindViolations("Matches", "MatchAgent.cs", text);

        // Assert
        Assert.Empty(violations);
    }

    [Fact]
    public void FindViolations_CvsUsesMatches_ReportsIt()
    {
        // Arrange: a fully written name counts too, not only a using line.
        const string text = "var match = new Jobly.Api.Features.Matches.JobMatch();";

        // Act
        var violations = FindViolations("Cvs", "CvService.cs", text);

        // Assert
        Assert.Single(violations);
    }

    private static List<string> FindViolations(string slice, string fileName, string text)
    {
        var allowed = AllowedSlices.GetValueOrDefault(slice, []);

        return FeatureReference().Matches(text)
            .Select(match => match.Groups[1].Value)
            .Where(referenced => referenced != slice && !allowed.Contains(referenced))
            .Distinct()
            .Select(referenced => $"{slice}/{fileName} uses the {referenced} slice, which {slice} may not use.")
            .ToList();
    }

    // The test runs from tests/<project>/bin/Debug/net10.0, so walk up until the solution file is found.
    private static string FindRepositoryRoot()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "Jobly.slnx"))) folder = folder.Parent;

        return folder?.FullName ?? throw new InvalidOperationException("Jobly.slnx was not found above the test folder.");
    }
}
