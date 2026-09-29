using System.Globalization;
using Porchlight.Core.Health;

namespace Porchlight.App.Features.Health;

/// <summary>One existing restore point.</summary>
public sealed class RestorePointRowViewModel(RestorePointInfo info)
{
    public string Description { get; } = info.Description;

    public string WhenText { get; } =
        info.CreatedAt.LocalDateTime.ToString("d MMM yyyy, h:mm tt", CultureInfo.CurrentCulture);
}
