using Porchlight.Core.Startup;

namespace Porchlight.Core.Tests.Startup;

internal sealed class FakeStartupInfoReader : IStartupInfoReader
{
    public List<StartupInfoRecord> Records { get; } = [];

    public bool AccessDenied { get; set; }

    public StartupInfoReadResult Read() => new([.. Records], AccessDenied);
}
