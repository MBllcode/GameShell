using GameShell.Core.Displays;
using GameShell.Core.Settings;
using GameShell.Desktop.Bridge;
using Xunit;

namespace GameShell.Core.Tests;

public sealed class ProfileFileSearchTests
{
    [Theory]
    [InlineData("Chrome.EXE", true)]
    [InlineData("Work.slnx", true)]
    [InlineData("Budget.xlsm", true)]
    [InlineData("Report.pdf", true)]
    [InlineData("debug.log", false)]
    [InlineData("cache.tmp", false)]
    [InlineData("library.dll", false)]
    public void FiltersSearchIndexByUsefulFileTypes(string path, bool expected)
    {
        Assert.Equal(expected, ProfileFileSearch.IsSearchableFile(path));
    }

    [Fact]
    public async Task SearchFindsSolutionsCaseInsensitivelyAndSkipsDependencies()
    {
        var root = Path.Combine(Path.GetTempPath(), "gameshell-search-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(root, "node_modules"));
        try
        {
            var solution = Path.Combine(root, "Coding.slnx");
            File.WriteAllText(solution, "");
            File.WriteAllText(Path.Combine(root, "node_modules", "CodingHidden.slnx"), "");
            var result = ProfileFileSearch.Search("CODING", root);
            for (var attempt = 0; result.Scanning && attempt < 100; attempt++)
            {
                await Task.Delay(30);
                result = ProfileFileSearch.Search("CODING", root);
            }
            Assert.False(result.Scanning);
            Assert.Equal(new[] { solution }, result.Paths);
            Assert.Empty(ProfileFileSearch.Search("absent", root).Paths);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void ValidationPreservesNonExecutableFiles()
    {
        var settings = new AppSettings();
        settings.Display.GamingProfiles = [new DisplayLayoutProfile { Name = "Coding" }];
        settings.Display.GamingProfileConfigurations = [new GamingProfileConfiguration {
            ProfileName = "Coding", Applications = [new ProfileApplication(@"C:\projects\Coding.slnx", "screen", false)]
        }];
        var validated = AppSettingsValidator.Validate(settings);
        Assert.EndsWith("Coding.slnx", Assert.Single(validated.Display.GamingProfileConfigurations[0].Applications).ExecutablePath);
    }
}
