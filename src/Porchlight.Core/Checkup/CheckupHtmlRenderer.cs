using System.Globalization;
using System.Net;
using System.Text;

namespace Porchlight.Core.Checkup;

/// <summary>Renders a <see cref="CheckupReport"/> as one self-contained HTML page: inline CSS, no
/// scripts, no external requests. Every piece of text is HTML-encoded.</summary>
public static class CheckupHtmlRenderer
{
    private const string Style = """
        :root{color-scheme:light dark;--bg:#fff;--fg:#1b1b1b;--card:#f5f5f5;--muted:#5c5c5c;--ok:#0f7b0f;--warn:#9d5d00;--bad:#c42b1c}
        @media (prefers-color-scheme:dark){:root{--bg:#202020;--fg:#f3f3f3;--card:#2b2b2b;--muted:#b5b5b5;--ok:#6ccb5f;--warn:#fce100;--bad:#ff99a4}}
        body{font-family:"Segoe UI",sans-serif;background:var(--bg);color:var(--fg);margin:0;padding:24px;line-height:1.5}
        main{max-width:720px;margin:0 auto}
        h1{margin:0 0 4px}
        .meta{color:var(--muted);margin:0 0 20px}
        section{background:var(--card);border-radius:8px;padding:12px 16px;margin:0 0 12px}
        h2{font-size:1.1em;margin:0 0 6px}
        .status{font-weight:600}
        .ok{color:var(--ok)}.warn{color:var(--warn)}.bad{color:var(--bad)}
        ul{margin:4px 0 0;padding-left:20px}
        """;

    public static string Render(CheckupReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var html = new StringBuilder();
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html lang=\"en\"><head><meta charset=\"utf-8\">");
        html.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        html.AppendLine("<title>Porchlight check-up</title>");
        html.AppendLine(CultureInfo.InvariantCulture, $"<style>{Style}</style></head><body><main>");
        html.AppendLine("<h1>Porchlight check-up</h1>");
        var when = report.GeneratedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        html.AppendLine(CultureInfo.InvariantCulture,
            $"<p class=\"meta\">Computer: {Encode(report.ComputerName)} &middot; {Encode(when)}</p>");
        html.AppendLine(CultureInfo.InvariantCulture,
            $"<p>Overall: <span class=\"status {CssClass(report.OverallSeverity)}\">{Encode(CheckupSeverityText.Label(report.OverallSeverity))}</span></p>");

        foreach (var section in report.Sections)
        {
            html.AppendLine(CultureInfo.InvariantCulture,
                $"<section><h2>{Encode(section.Title)} - <span class=\"status {CssClass(section.Severity)}\">{Encode(CheckupSeverityText.Label(section.Severity))}</span></h2><ul>");
            foreach (var line in section.Lines)
            {
                html.AppendLine(CultureInfo.InvariantCulture, $"<li>{Encode(line)}</li>");
            }

            html.AppendLine("</ul></section>");
        }

        html.AppendLine("</main></body></html>");
        return html.ToString();
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);

    private static string CssClass(CheckupSeverity severity) => severity switch
    {
        CheckupSeverity.Ok => "ok",
        CheckupSeverity.NeedsAttention => "warn",
        _ => "bad",
    };
}
