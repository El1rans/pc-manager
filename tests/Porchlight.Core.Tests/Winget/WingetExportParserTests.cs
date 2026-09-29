using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.Core.Processes;
using Porchlight.Core.Winget;
using Xunit;

namespace Porchlight.Core.Tests.Winget;

public sealed class WingetExportParserTests
{
    private const string ValidExport = """
        {"$schema":"https://aka.ms/winget-packages.schema.2.0.json","CreationDate":"2026-01-01",
         "Sources":[{"Packages":[{"PackageIdentifier":"VideoLAN.VLC"},{"PackageIdentifier":"7zip.7zip"},{"PackageIdentifier":"videolan.vlc"}],
                     "SourceDetails":{"Name":"winget"}}],"WinGetVersion":"1.9"}
        """;

    [Fact]
    public void Parse_ValidExport_ListsDistinctApps()
    {
        var result = WingetExportParser.Parse(ValidExport);

        Assert.True(result.IsSuccess);
        Assert.Equal(["VideoLAN.VLC", "7zip.7zip"], result.Apps.Select(a => a.Id));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{\"foo\":1}")]
    [InlineData("{\"Sources\":[]}")]
    [InlineData("{\"Sources\":[{\"Packages\":[{\"PackageIdentifier\":\"has space\"}]}]}")]
    public void Parse_NotAnExportOrNoApps_Fails(string json)
    {
        var result = WingetExportParser.Parse(json);

        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Error!);
    }

    [Fact]
    public void Parse_InvalidIdsAreSkippedAndCounted()
    {
        var result = WingetExportParser.Parse(
            "{\"Sources\":[{\"Packages\":[{\"PackageIdentifier\":\"Good.App\"},{\"PackageIdentifier\":\"bad id\"},{\"x\":1}]}]}");

        Assert.True(result.IsSuccess);
        Assert.Single(result.Apps);
        Assert.Equal(2, result.SkippedCount);
    }

    [Fact]
    public void Parse_TooManyApps_Fails()
    {
        var packages = string.Join(",", Enumerable.Range(0, WingetExportParser.MaxApps + 1).Select(i => $"{{\"PackageIdentifier\":\"App.N{i}\"}}"));

        var result = WingetExportParser.Parse($"{{\"Sources\":[{{\"Packages\":[{packages}]}}]}}");

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task LoadAsync_OversizedFile_IsRejectedWithoutReading()
    {
        var path = Path.Combine(Path.GetTempPath(), "porchlight-export-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            await File.WriteAllTextAsync(path, new string(' ', WingetExportParser.MaxFileBytes + 1), TestContext.Current.CancellationToken);

            var result = await WingetExportFile.LoadAsync(path, NullLogger.Instance, TestContext.Current.CancellationToken);

            Assert.False(result.IsSuccess);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task LoadAsync_ValidFile_Parses()
    {
        var path = Path.Combine(Path.GetTempPath(), "porchlight-export-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            await File.WriteAllTextAsync(path, ValidExport, TestContext.Current.CancellationToken);

            var result = await WingetExportFile.LoadAsync(path, NullLogger.Instance, TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private sealed class RecordingRunner : IProcessRunner
    {
        public List<IReadOnlyList<string>> Calls { get; } = [];

        public Task<ProcessRunResult> RunAsync(
            string fileName, IReadOnlyList<string> arguments, IProgress<string>? onLine, IProgress<string>? onProgress,
            CancellationToken cancellationToken)
        {
            Calls.Add(arguments);
            return Task.FromResult(new ProcessRunResult(0, [], []));
        }

        public void StartDetached(string fileName, IReadOnlyList<string> arguments) => throw new NotSupportedException();
    }

    [Fact]
    public async Task Client_ExportAndImport_BuildExpectedArguments()
    {
        var runner = new RecordingRunner();
        var client = new WingetClient(runner, NullLogger<WingetClient>.Instance);

        await client.ExportAsync("apps.json", null, null, CancellationToken.None);
        await client.ImportAsync("apps.json", null, null, CancellationToken.None);

        Assert.Equal(["export", "-o", "apps.json", "--accept-source-agreements", "--disable-interactivity"], runner.Calls[0]);
        Assert.Equal(
            [
                "import", "-i", "apps.json", "--accept-package-agreements", "--accept-source-agreements",
                "--ignore-unavailable", "--disable-interactivity",
            ],
            runner.Calls[1]);
    }
}
