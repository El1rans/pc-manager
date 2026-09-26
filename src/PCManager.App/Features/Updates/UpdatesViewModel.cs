using PCManager.App.Shell;

namespace PCManager.App.Features.Updates;

public sealed partial class UpdatesViewModel : PageViewModelBase
{
    public override string Title => "Updates";

    public override string Glyph => "";

    public override int Order => 1;
}
