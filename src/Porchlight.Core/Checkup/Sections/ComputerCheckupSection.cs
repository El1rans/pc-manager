using System.Globalization;
using Porchlight.Core.Monitoring;

namespace Porchlight.Core.Checkup.Sections;

/// <summary>Which PC this is: name, Windows version, make/model and memory. No serial numbers.</summary>
public sealed class ComputerCheckupSection : ICheckupSection
{
    private readonly ISystemInfoProvider _systemInfo;

    public ComputerCheckupSection(ISystemInfoProvider systemInfo)
    {
        _systemInfo = systemInfo;
    }

    public string Title => "Computer and Windows";

    public int Order => 100;

    public async Task<CheckupSectionResult?> BuildAsync(CancellationToken cancellationToken)
    {
        var info = await _systemInfo.GetAsync(cancellationToken).ConfigureAwait(false);

        List<string> lines = [$"Computer name: {info.ComputerName}"];

        var windows = string.Join(' ', new[] { info.OsCaption, string.IsNullOrWhiteSpace(info.OsBuild) ? null : $"(build {info.OsBuild})" }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
        if (windows.Length > 0)
        {
            lines.Add($"Windows: {windows}");
        }

        var makeAndModel = $"{info.Manufacturer} {info.Model}".Trim();
        if (makeAndModel.Length > 0)
        {
            lines.Add($"Make and model: {makeAndModel}");
        }

        if (info.TotalRamBytes > 0)
        {
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"Memory: {ByteFormatter.FormatBytes(info.TotalRamBytes)}"));
        }

        return new CheckupSectionResult(Title, CheckupSeverity.Ok, lines);
    }
}
