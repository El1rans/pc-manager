using System.Text;

namespace PCManager.Core.Processes;

/// <summary>
/// Splits winget's raw console output into real lines and progress/spinner redraws.
/// </summary>
/// <remarks>
/// <para>
/// winget writes a real line ended by <c>"\r\n"</c> or <c>"\n"</c>, but redraws its progress bar
/// and spinner in place using a lone <c>"\r"</c> with no following <c>"\n"</c>. Telling these apart
/// requires looking one character past the <c>"\r"</c>: if it is <c>"\n"</c>, the <c>"\r"</c> was
/// just part of a normal line ending; anything else means the <c>"\r"</c> started a redraw and the
/// text collected so far is a progress update, not a line.
/// </para>
/// <para>
/// Ported from the <c>Invoke-Winget</c> function in <c>prototype/WingetUpdater.ps1</c>, which
/// proved this logic against real winget output. This type is push-based (<see cref="Feed"/> is
/// called with whatever characters the caller has read so far) so it works whether the caller
/// reads a stream one buffer at a time or one character at a time, and so a <c>"\r\n"</c> split
/// across two separate reads (and therefore two separate <see cref="Feed"/> calls) is still
/// handled correctly - the "did we just see a lone CR" state is a field, not a local.
/// </para>
/// </remarks>
public sealed class WingetOutputReader
{
    private readonly StringBuilder _current = new();
    private readonly List<string> _lines = [];
    private readonly IProgress<string>? _onLine;
    private readonly IProgress<string>? _onProgress;
    private bool _pendingCarriageReturn;

    public WingetOutputReader(IProgress<string>? onLine = null, IProgress<string>? onProgress = null)
    {
        _onLine = onLine;
        _onProgress = onProgress;
    }

    /// <summary>Every complete line seen so far, in order.</summary>
    public IReadOnlyList<string> Lines => _lines;

    /// <summary>Feeds the next chunk of raw characters read from the process's stdout.</summary>
    public void Feed(ReadOnlySpan<char> chunk)
    {
        foreach (var c in chunk)
        {
            if (_pendingCarriageReturn)
            {
                _pendingCarriageReturn = false;
                if (c == '\n')
                {
                    EmitLine();
                    continue;
                }

                EmitProgress();
            }

            if (c == '\r')
            {
                _pendingCarriageReturn = true;
            }
            else if (c == '\n')
            {
                EmitLine();
            }
            else
            {
                _current.Append(c);
            }
        }
    }

    /// <summary>
    /// Call once after the stream has ended. A trailing lone <c>"\r"</c> with nothing read after
    /// it is treated as ending a line (there is no more input that could turn it into a redraw),
    /// and any text left in the buffer becomes a final line.
    /// </summary>
    public void Complete()
    {
        if (_pendingCarriageReturn)
        {
            _pendingCarriageReturn = false;
            EmitLine();
        }

        if (_current.Length > 0)
        {
            EmitLine();
        }
    }

    private void EmitLine()
    {
        var text = _current.ToString();
        _current.Clear();
        _lines.Add(text);
        if (text.Trim().Length > 0)
        {
            _onLine?.Report(text);
        }
    }

    private void EmitProgress()
    {
        var text = _current.ToString().Trim();
        _current.Clear();
        if (text.Length > 0 && !IsSpinnerFrame(text))
        {
            _onProgress?.Report(text);
        }
    }

    /// <summary>winget's spinner is a single character cycling through these frames.</summary>
    private static bool IsSpinnerFrame(string text) =>
        text.Length == 1 && text[0] is '-' or '\\' or '|' or '/';
}
