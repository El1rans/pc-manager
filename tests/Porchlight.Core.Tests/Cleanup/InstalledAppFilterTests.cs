using Porchlight.Core.Cleanup;
using Xunit;

namespace Porchlight.Core.Tests.Cleanup;

public sealed class InstalledAppFilterTests
{
    private static RawUninstallEntry Entry(
        string? name = "Some App",
        string? version = "1.0",
        int? sizeKb = 1000,
        string? uninstall = @"""C:\x\unins000.exe""",
        int? systemComponent = null,
        string? parentKey = null,
        string? releaseType = null,
        bool perMachine = true,
        string? installDate = null,
        string? publisher = "Pub") =>
        new(name, publisher, version, sizeKb, installDate, uninstall, systemComponent, parentKey, releaseType, perMachine);

    [Fact]
    public void Apply_KeepsANormalApp_ConvertingKilobytesToBytes()
    {
        var app = Assert.Single(InstalledAppFilter.Apply([Entry(sizeKb: 2048, installDate: "20230615")]));

        Assert.Equal("Some App", app.DisplayName);
        Assert.Equal(2048L * 1024, app.EstimatedSizeBytes);
        Assert.Equal(new DateOnly(2023, 6, 15), app.InstallDate);
        Assert.True(app.IsPerMachine);
    }

    [Fact]
    public void Apply_NoSizeOrZeroSize_IsUnknownSize()
    {
        var apps = InstalledAppFilter.Apply([Entry(name: "A", sizeKb: null), Entry(name: "B", sizeKb: 0)]);

        Assert.All(apps, app => Assert.Null(app.EstimatedSizeBytes));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Apply_ExcludesEntriesWithNoDisplayName(string? name)
    {
        Assert.Empty(InstalledAppFilter.Apply([Entry(name: name)]));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Apply_ExcludesEntriesWithNoUninstallString(string? uninstall)
    {
        Assert.Empty(InstalledAppFilter.Apply([Entry(uninstall: uninstall)]));
    }

    [Fact]
    public void Apply_ExcludesSystemComponents()
    {
        Assert.Empty(InstalledAppFilter.Apply([Entry(systemComponent: 1)]));
        Assert.Single(InstalledAppFilter.Apply([Entry(systemComponent: 0)]));
    }

    [Fact]
    public void Apply_ExcludesUpdatesAndPatches()
    {
        Assert.Empty(InstalledAppFilter.Apply([Entry(parentKey: "Office16")]));
        Assert.Empty(InstalledAppFilter.Apply([Entry(releaseType: "Update")]));
        Assert.Empty(InstalledAppFilter.Apply([Entry(releaseType: "Hotfix")]));
        Assert.Empty(InstalledAppFilter.Apply([Entry(releaseType: "Security Update")]));
        Assert.Single(InstalledAppFilter.Apply([Entry(releaseType: "Application")]));
    }

    [Theory]
    [InlineData("Porchlight")]
    [InlineData("PORCHLIGHT 0.1.0")]
    [InlineData("AnyDesk")]
    [InlineData("OpenRGB 0.9")]
    [InlineData("PawnIO")]
    public void Apply_ExcludesPorchlightAndItsComponents(string name)
    {
        Assert.Empty(InstalledAppFilter.Apply([Entry(name: name)]));
    }

    [Fact]
    public void Apply_DeduplicatesByNameAndVersion_KeepingTheFirst()
    {
        var apps = InstalledAppFilter.Apply(
        [
            Entry(name: "Twice", version: "1.0", perMachine: true),
            Entry(name: "twice", version: "1.0", perMachine: false),
            Entry(name: "Twice", version: "2.0"),
        ]);

        Assert.Equal(2, apps.Count);
        Assert.Contains(apps, a => a.Version == "1.0" && a.IsPerMachine);
    }

    [Fact]
    public void Apply_ProtectedNames_AreHiddenByDefaultAndKeptOnRequest()
    {
        RawUninstallEntry[] entries = [Entry(name: "AnyDesk"), Entry(name: "Porchlight"), Entry(name: "Other")];

        Assert.Equal(["Other"], InstalledAppFilter.Apply(entries).Select(a => a.DisplayName));
        Assert.Equal(3, InstalledAppFilter.Apply(entries, includeProtectedNames: true).Count);
    }

    [Fact]
    public void Apply_IncludingProtectedNames_StillDropsHiddenAndUpdateEntries()
    {
        var apps = InstalledAppFilter.Apply(
        [
            Entry(name: "Hidden", systemComponent: 1),
            Entry(name: "Patch", releaseType: "Hotfix"),
            Entry(name: "Child", parentKey: "Parent"),
            Entry(name: "NoUninstall", uninstall: null),
            Entry(name: "Fine"),
        ],
        includeProtectedNames: true);

        Assert.Equal(["Fine"], apps.Select(a => a.DisplayName));
    }

    [Fact]
    public void Apply_SortsLargestFirst_UnknownSizeLast()
    {
        var apps = InstalledAppFilter.Apply(
        [
            Entry(name: "Small", sizeKb: 10),
            Entry(name: "Unknown", sizeKb: null),
            Entry(name: "Big", sizeKb: 99999),
        ]);

        Assert.Equal(["Big", "Small", "Unknown"], apps.Select(a => a.DisplayName));
    }
}
