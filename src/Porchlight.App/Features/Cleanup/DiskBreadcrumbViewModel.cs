using Porchlight.Core.Cleanup;

namespace Porchlight.App.Features.Cleanup;

/// <summary>One step of the breadcrumb above the disk map list ("Your files", "Videos", ...).</summary>
public sealed class DiskBreadcrumbViewModel
{
    public DiskBreadcrumbViewModel(string label, DiskNode node, bool isFirst, bool isCurrent)
    {
        Label = label;
        Node = node;
        IsFirst = isFirst;
        IsCurrent = isCurrent;
    }

    public string Label { get; }

    public DiskNode Node { get; }

    /// <summary>The start of the trail; it has no separator in front of it.</summary>
    public bool IsFirst { get; }

    /// <summary>The folder being shown; not clickable.</summary>
    public bool IsCurrent { get; }

    public bool IsLink => !IsCurrent;
}
