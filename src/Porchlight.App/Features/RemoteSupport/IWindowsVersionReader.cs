namespace Porchlight.App.Features.RemoteSupport;

/// <summary>Reads a friendly Windows version string for "Copy support info", behind an interface so
/// it is unit-testable. Deliberately not <c>Environment.OSVersion</c> - that reports "Microsoft
/// Windows NT 10.0.xxxxx" even on Windows 11, which would confuse a non-technical reader.</summary>
public interface IWindowsVersionReader
{
    /// <summary>A friendly version string, e.g. "Windows 11 Pro (build 26200)". Falls back to a
    /// generic string if the registry values are unavailable.</summary>
    string GetFriendlyVersion();
}
