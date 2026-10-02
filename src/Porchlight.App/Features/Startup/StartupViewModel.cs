using System.IO;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Porchlight.App.Shell;
using Porchlight.Core.Elevation;
using Porchlight.Core.Startup;

namespace Porchlight.App.Features.Startup;

/// <summary>"Startup apps" page: lists what starts at sign-in and turns items on/off the way Task
/// Manager does. See docs/specs/13-startup-apps.md.</summary>
public sealed partial class StartupViewModel : PageViewModelBase
{
    private const string LoadFailedMessage = "Couldn't read the startup list. Try Refresh.";
    private const string ChangeFailedMessage = "Couldn't change this one. Try again, or restart Porchlight as administrator.";
    private const string NeedsAdminMessage = "This one needs administrator rights. Use Restart as administrator above.";
    private const string TurnedOffMessage = "{0} is turned off. It takes effect the next time you sign in, and you can turn it back on here.";
    private const string TurnedOnMessage = "{0} is turned on. It will start the next time you sign in.";

    private readonly IStartupService _service;
    private readonly IElevationService _elevation;
    private readonly ILogger<StartupViewModel> _logger;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    private bool _isLoading;

    [ObservableProperty]
    private string? _message;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Summary), nameof(ShowEmptyState), nameof(ShowAdminBanner), nameof(ShowImpactHint))]
    private bool _hasLoaded;

    [ObservableProperty]
    private bool _sortByImpact;

    private IReadOnlyList<StartupEntryViewModel> _loaded = [];
    private bool _impactNeedsAdmin;

    public StartupViewModel(IStartupService service, IElevationService elevation, ILogger<StartupViewModel> logger)
    {
        _service = service;
        _elevation = elevation;
        _logger = logger;
    }

    public override string Title => "Startup apps";

    // Segoe Fluent Icons "PowerButton".
    public override string Glyph => "";

    public override int Order => 2;

    public override PageCategory Category => PageCategory.TuneUp;

    public ObservableCollection<StartupEntryViewModel> Items { get; } = [];

    public bool ShowAdminBanner => !_elevation.IsElevated && Items.Any(i => i.Entry.RequiresAdmin);

    /// <summary>One plain line (not the full banner): impact can't be read without administrator rights.</summary>
    public bool ShowImpactHint => HasLoaded && _impactNeedsAdmin && !_elevation.IsElevated;

    public bool ShowEmptyState => HasLoaded && !IsLoading && Items.Count == 0 && ErrorMessage is null;

    public string Summary
    {
        get
        {
            if (!HasLoaded)
            {
                return string.Empty;
            }

            var on = Items.Count(i => i.IsEnabled);
            var noun = Items.Count == 1 ? "app starts" : "apps start";
            var text = $"{Items.Count} {noun} with Windows: {on} on, {Items.Count - on} off";

            // Only items that are still on can slow the next sign-in.
            var high = Items.Count(i => i.IsEnabled && i.IsHighImpact);
            if (high > 0)
            {
                text += $", {high} {(high == 1 ? "has" : "have")} high impact";
            }

            return text + ".";
        }
    }

    public override Task OnNavigatedToAsync(CancellationToken cancellationToken) => LoadAsync(cancellationToken);

    partial void OnSortByImpactChanged(bool value) => ApplyOrder();

    private void ApplyOrder()
    {
        IEnumerable<StartupEntryViewModel> ordered = _loaded;
        if (SortByImpact)
        {
            // Highest impact first; the service's name order is kept within a rating (stable sort).
            ordered = _loaded.OrderByDescending(i => i.Impact);
        }

        Items.Clear();
        foreach (var item in ordered)
        {
            Items.Add(item);
        }
    }

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync(CancellationToken.None);

    [RelayCommand]
    private async Task ToggleAsync(StartupEntryViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        var enable = !item.IsEnabled;
        Message = null;
        ErrorMessage = null;

        StartupChangeResult result;
        try
        {
            result = await _service.SetEnabledAsync(item.Id, enable, CancellationToken.None);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Changing a startup item failed unexpectedly.");
            ErrorMessage = ChangeFailedMessage;
            return;
        }

        switch (result)
        {
            case StartupChangeResult.Changed:
                item.IsEnabled = enable;
                Message = string.Format(System.Globalization.CultureInfo.CurrentCulture, enable ? TurnedOnMessage : TurnedOffMessage, item.Name);
                OnPropertyChanged(nameof(Summary));
                break;
            case StartupChangeResult.NeedsAdmin:
                ErrorMessage = NeedsAdminMessage;
                break;
            default:
                ErrorMessage = ChangeFailedMessage;
                break;
        }
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var entries = await _service.ListAsync(cancellationToken);
            _loaded = [.. entries.Select(e => new StartupEntryViewModel(e, _elevation.IsElevated))];
            _impactNeedsAdmin = _service.ImpactNeedsAdmin;
            ApplyOrder();

            HasLoaded = true;
        }
        catch (OperationCanceledException)
        {
            // Navigated away or shutting down; nothing to show.
            return;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Reading the startup list failed.");
            ErrorMessage = LoadFailedMessage;
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(Summary));
            OnPropertyChanged(nameof(ShowAdminBanner));
            OnPropertyChanged(nameof(ShowEmptyState));
            OnPropertyChanged(nameof(ShowImpactHint));
        }
    }
}
