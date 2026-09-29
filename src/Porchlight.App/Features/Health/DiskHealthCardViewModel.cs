using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Porchlight.Core.Health;

namespace Porchlight.App.Features.Health;

/// <summary>The Disk health card.</summary>
public sealed partial class DiskHealthCardViewModel(IDiskHealthService service) : HealthCardViewModelBase
{
    [ObservableProperty]
    private string? _predictWarning;

    public ObservableCollection<DiskRowViewModel> Disks { get; } = [];

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        IsChecking = true;
        try
        {
            var result = await service.GetAsync(cancellationToken);
            Disks.Clear();
            if (!result.Succeeded || result.Value is null)
            {
                PredictWarning = null;
                SetFailure(result.Error);
                return;
            }

            ErrorText = null;
            foreach (var disk in result.Value.Disks)
            {
                Disks.Add(new DiskRowViewModel(disk));
            }

            PredictWarning = result.Value.PredictFailureDetected
                ? "Windows expects one of your drives to fail. Back up your files soon."
                : null;
        }
        finally
        {
            IsChecking = false;
        }
    }
}
