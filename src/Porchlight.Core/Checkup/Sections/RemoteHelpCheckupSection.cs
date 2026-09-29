using Porchlight.Core.RemoteSupport;

namespace Porchlight.Core.Checkup.Sections;

/// <summary>Whether AnyDesk (remote help) is ready. Deliberately does not include the AnyDesk address.</summary>
public sealed class RemoteHelpCheckupSection : ICheckupSection
{
    private readonly IAnyDeskService _anyDesk;

    public RemoteHelpCheckupSection(IAnyDeskService anyDesk)
    {
        _anyDesk = anyDesk;
    }

    public string Title => "Remote help";

    public int Order => 600;

    public async Task<CheckupSectionResult?> BuildAsync(CancellationToken cancellationToken)
    {
        var state = await _anyDesk.GetStateAsync(cancellationToken).ConfigureAwait(false);

        if (!state.IsInstalled)
        {
            return new CheckupSectionResult(Title, CheckupSeverity.NeedsAttention, ["AnyDesk (remote help) is not installed."]);
        }

        return state.IsRunning
            ? new CheckupSectionResult(Title, CheckupSeverity.Ok, ["AnyDesk is installed and running."])
            : new CheckupSectionResult(Title, CheckupSeverity.NeedsAttention, ["AnyDesk is installed but not running."]);
    }
}
