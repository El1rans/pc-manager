namespace Porchlight.Core.Startup;

/// <summary>Reads a program's own name and publisher from its version resource without running it.</summary>
public interface IFileProductInfoReader
{
    /// <summary>Returns the info for <paramref name="path"/>, or null if the file is missing or unreadable.</summary>
    FileProductInfo? Read(string path);
}
