using System.Globalization;
using System.Text;

namespace Porchlight.Core.Checkup;

/// <summary>Renders a <see cref="CheckupReport"/> as plain text (clipboard, email body, .txt file).</summary>
public static class CheckupTextRenderer
{
    public static string Render(CheckupReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var text = new StringBuilder();
        text.AppendLine("Porchlight check-up");
        text.AppendLine(CultureInfo.InvariantCulture, $"Computer: {report.ComputerName}");
        text.AppendLine(CultureInfo.InvariantCulture, $"Date: {report.GeneratedAt:yyyy-MM-dd HH:mm}");
        text.AppendLine(CultureInfo.InvariantCulture, $"Overall: {CheckupSeverityText.Label(report.OverallSeverity)}");

        foreach (var section in report.Sections)
        {
            text.AppendLine();
            text.AppendLine(CultureInfo.InvariantCulture, $"{section.Title} - {CheckupSeverityText.Label(section.Severity)}");
            foreach (var line in section.Lines)
            {
                text.AppendLine(CultureInfo.InvariantCulture, $"  - {line}");
            }
        }

        return text.ToString().TrimEnd();
    }
}
