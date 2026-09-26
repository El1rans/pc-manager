using PCManager.App.Shell;

namespace PCManager.App.Features.Hardware;

public sealed partial class HardwareViewModel : PageViewModelBase
{
    public override string Title => "Hardware";

    public override string Glyph => "";

    public override int Order => 2;
}
