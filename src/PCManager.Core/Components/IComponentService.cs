namespace PCManager.Core.Components;

/// <summary>
/// The one shared way features detect, install and start the third-party tools they depend on
/// (AnyDesk, OpenRGB, the PawnIO driver). Backed by <see cref="ComponentCatalog"/>.
/// </summary>
public interface IComponentService
{
    /// <summary>Every component this service knows about.</summary>
    IReadOnlyList<ComponentDefinition> Definitions { get; }

    /// <summary>
    /// Raised whenever a call to this service (from any page, including first-run setup) changes
    /// a component's status, so every page showing a <c>ComponentCard</c> for it can update
    /// without polling or restarting.
    /// </summary>
    event EventHandler<ComponentStatusChangeEventInfo>? StatusChanged;

    /// <summary>Detects the current status of the component identified by <paramref name="id"/>
    /// (one of <see cref="ComponentIds"/>). Runs off the calling thread.</summary>
    Task<ComponentStatus> GetStatusAsync(string id, CancellationToken cancellationToken);

    /// <summary>
    /// Installs the component via <c>winget install --id &lt;id&gt; --exact --source winget --silent
    /// --accept-package-agreements --accept-source-agreements --disable-interactivity</c>,
    /// streaming winget's output to <paramref name="log"/> and <paramref name="progress"/>. An
    /// "already installed" exit code counts as success; a "reboot required to finish" exit code
    /// also counts as success, with a status message saying so. Re-detects the component
    /// afterwards and raises <see cref="StatusChanged"/> with the result either way.
    /// </summary>
    /// <param name="cancellationToken">
    /// Only checked <i>before</i> winget is launched - once the installer has actually started,
    /// this call runs it to completion regardless of cancellation. Installers (especially a kernel
    /// driver's) must not be killed mid-write; see <c>IProcessRunner.RunAsync</c>. Cancel before
    /// calling this method (or before it has had a chance to start winget) to skip the install
    /// entirely.
    /// </param>
    Task<ComponentStatus> InstallAsync(
        string id, IProgress<string> log, IProgress<string> progress, CancellationToken cancellationToken);

    /// <summary>
    /// Starts the component for components that need to be running in the background (OpenRGB
    /// with its SDK server; AnyDesk normally). A no-op - returning the current status unchanged -
    /// for components with no such notion (e.g. the PawnIO driver). Raises
    /// <see cref="StatusChanged"/> when it starts something. If PC Manager is running elevated and
    /// the resolved executable is not one only an administrator could have placed there (i.e. not
    /// under Program Files or an HKLM-registered install location), this refuses to start it and
    /// returns <see cref="ComponentState.Error"/> instead, to avoid silently elevating a
    /// user-writable executable.
    /// </summary>
    Task<ComponentStatus> StartAsync(string id, CancellationToken cancellationToken);
}

/// <param name="ComponentId">One of <see cref="ComponentIds"/>.</param>
/// <param name="Status">The component's new status.</param>
public sealed record ComponentStatusChangeEventInfo(string ComponentId, ComponentStatus Status);
