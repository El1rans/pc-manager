namespace Porchlight.Core.Printing;

/// <summary>Which files in the spool folder may be deleted when the spooler is stopped: only the
/// queued-job data (<c>.SPL</c>) and job settings (<c>.SHD</c>) files, nothing else.</summary>
public static class SpoolFileFilter
{
    private static readonly string[] SpoolExtensions = [".spl", ".shd"];

    public static bool IsSpoolFile(string fileName) =>
        SpoolExtensions.Contains(Path.GetExtension(fileName), StringComparer.OrdinalIgnoreCase);
}
