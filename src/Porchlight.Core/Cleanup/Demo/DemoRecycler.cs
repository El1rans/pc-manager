#if DEBUG
namespace Porchlight.Core.Cleanup.Demo;

/// <summary>DEBUG-only fake <see cref="IRecycler"/> that touches no file - see
/// <see cref="Monitoring.Demo.DemoDataMode"/>.</summary>
internal sealed class DemoRecycler : IRecycler
{
    public bool MoveToRecycleBin(string path) => true;
}
#endif
