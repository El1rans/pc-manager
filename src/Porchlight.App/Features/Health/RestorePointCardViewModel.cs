using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Porchlight.Core.Elevation;
using Porchlight.Core.Health;
using Porchlight.Core.Processes;

namespace Porchlight.App.Features.Health;

/// <summary>The Restore point card.</summary>
public sealed partial class RestorePointCardViewModel : HealthCardViewModelBase
{
    private const string RestorePointDescription = "Porchlight restore point";
    private const string SystemProtectionExe = "SystemPropertiesProtection.exe";

    private readonly IRestorePointService _service;
    private readonly IElevationService _elevation;
    private readonly IProcessRunner _processRunner;
    private readonly ILogger<RestorePointCardViewModel> _logger;

    private RestorePointAvailability? _availability;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private bool _isCreating;

    [ObservableProperty]
    private string? _availabilityText;

    [ObservableProperty]
    private string? _resultText;

    [ObservableProperty]
    private bool _protectionOff;

    [ObservableProperty]
    private string? _recentNote;

    public RestorePointCardViewModel(
        IRestorePointService service, IElevationService elevation, IProcessRunner processRunner,
        ILogger<RestorePointCardViewModel> logger)
    {
        _service = service;
        _elevation = elevation;
        _processRunner = processRunner;
        _logger = logger;
    }

    public ObservableCollection<RestorePointRowViewModel> Recent { get; } = [];

    public bool IsElevated => _elevation.IsElevated;

    public bool HasResult => !string.IsNullOrEmpty(ResultText);

    partial void OnResultTextChanged(string? value) => OnPropertyChanged(nameof(HasResult));

    private bool CanCreate() => IsElevated && !IsCreating && _availability?.CanCreate == true;

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        IsChecking = true;
        try
        {
            var result = await _service.GetStatusAsync(cancellationToken);
            Recent.Clear();
            if (!result.Succeeded || result.Value is null)
            {
                _availability = null;
                AvailabilityText = null;
                SetFailure(result.Error);
                CreateCommand.NotifyCanExecuteChanged();
                return;
            }

            ErrorText = null;
            var status = result.Value;
            _availability = RestorePointRules.Evaluate(status, DateTimeOffset.Now);
            AvailabilityText = _availability.Message;
            ProtectionOff = status.ProtectionEnabled == false;
            if (status.Recent is null)
            {
                RecentNote = "Couldn't read the list of restore points. This usually needs administrator rights.";
            }
            else
            {
                RecentNote = status.Recent.Count == 0 ? "There are no restore points yet." : null;
                foreach (var point in status.Recent)
                {
                    Recent.Add(new RestorePointRowViewModel(point));
                }
            }

            CreateCommand.NotifyCanExecuteChanged();
        }
        finally
        {
            IsChecking = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCreate))]
    private async Task CreateAsync()
    {
        IsCreating = true;
        ResultText = "Creating a restore point. This can take a minute...";
        try
        {
            var result = await _service.CreateAsync(
                RestorePointDescription, RestorePointKind.ApplicationInstall, CancellationToken.None);
            ResultText = result.Message;
            await RefreshAsync(CancellationToken.None);
        }
        finally
        {
            IsCreating = false;
        }
    }

    [RelayCommand]
    private void OpenSystemProtection()
    {
        try
        {
            _processRunner.StartDetached(Path.Combine(Environment.SystemDirectory, SystemProtectionExe), []);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Could not open System Protection.");
            ResultText = "Couldn't open System Protection. Search for \"Create a restore point\" in the Start menu.";
        }
    }
}
