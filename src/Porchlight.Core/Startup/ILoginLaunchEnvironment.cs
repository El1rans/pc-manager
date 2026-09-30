using System.Security.Principal;

namespace Porchlight.Core.Startup;

/// <summary>What <see cref="LoginLaunchService"/> needs to know about the current process, behind an
/// interface so tests do not depend on the real user or executable path.</summary>
public interface ILoginLaunchEnvironment
{
    /// <summary>The signed-in user as <c>DOMAIN\user</c>.</summary>
    string UserId { get; }

    /// <summary>Full path of the running Porchlight executable, or null if it cannot be determined.</summary>
    string? ExePath { get; }
}

/// <inheritdoc cref="ILoginLaunchEnvironment"/>
public sealed class LoginLaunchEnvironment : ILoginLaunchEnvironment
{
    public string UserId
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            return identity.Name;
        }
    }

    public string? ExePath => Environment.ProcessPath;
}
