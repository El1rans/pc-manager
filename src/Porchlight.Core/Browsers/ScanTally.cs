namespace Porchlight.Core.Browsers;

/// <summary>Counts the files and entries a scan had to skip (unreadable, oversized, malformed).</summary>
internal sealed class ScanTally
{
    public int Skipped { get; private set; }

    public void CountSkipped() => Skipped++;
}
