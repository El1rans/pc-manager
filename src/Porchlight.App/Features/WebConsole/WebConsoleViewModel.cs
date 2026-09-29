using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Porchlight.App.Features.RemoteSupport;
using Porchlight.App.Shell;
using Porchlight.Core.WebConsole;

namespace Porchlight.App.Features.WebConsole;

/// <summary>
/// "Web console" page: turns the read-only web console on or off, and shows the link (with its
/// access key) to open on a phone or another computer to watch this PC's stats. See
/// docs/specs/21-web-console.md.
/// </summary>
public sealed partial class WebConsoleViewModel : PageViewModelBase, IDisposable
{
    private readonly WebConsoleController _controller;
    private readonly ILocalAddressProvider _addressProvider;
    private readonly IClipboardService _clipboard;
    private readonly IUrlLauncher _urlLauncher;
    private readonly Dispatcher _dispatcher;

    /// <summary>Guards <see cref="OnIsEnabledChanged"/> while <see cref="Refresh"/> (not the user) is
    /// setting <see cref="IsEnabled"/>.</summary>
    private bool _refreshing;
    private bool _disposed;

    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    private string _portText;

    [ObservableProperty]
    private string? _portError;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private bool _hasFailed;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string _primaryLink = string.Empty;

    [ObservableProperty]
    private string _accessKey = string.Empty;

    [ObservableProperty]
    private string? _copyResult;

    [ObservableProperty]
    private bool _copyFailed;

    public WebConsoleViewModel(
        WebConsoleController controller,
        ILocalAddressProvider addressProvider,
        IClipboardService clipboard,
        IUrlLauncher urlLauncher)
    {
        _controller = controller;
        _addressProvider = addressProvider;
        _clipboard = clipboard;
        _urlLauncher = urlLauncher;
        // Same reasoning as ComponentCardViewModel: always the one UI dispatcher, but still
        // constructible outside a running WPF Application (e.g. unit tests).
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

        _portText = controller.ConfiguredPort.ToString(CultureInfo.InvariantCulture);
        _controller.Server.StateChanged += OnServerStateChanged;
        Refresh();
    }

    public override string Title => "Web console";

    // Segoe Fluent Icons "Globe".
    public override string Glyph => "";

    public override int Order => 2;

    public override PageCategory Category => PageCategory.Help;

    /// <summary>Other addresses this PC answers on (e.g. Wi-Fi and Ethernet both connected), for
    /// when the first link does not work from the viewing device's network.</summary>
    public ObservableCollection<string> OtherLinks { get; } = [];

    public bool HasOtherLinks => OtherLinks.Count > 0;

    public string PortHint { get; } = string.Create(
        CultureInfo.InvariantCulture,
        $"A number from {WebConsoleOptions.LowestAllowedPort} to {WebConsoleOptions.HighestAllowedPort}. The default is {WebConsoleOptions.DefaultPort}.");

    public override Task OnNavigatedToAsync(CancellationToken cancellationToken)
    {
        // Network adapters may have changed (Wi-Fi joined, cable plugged in) since last time.
        Refresh();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _controller.Server.StateChanged -= OnServerStateChanged;
    }

    partial void OnIsEnabledChanged(bool value)
    {
        if (_refreshing)
        {
            return;
        }

        _controller.SetEnabled(value);
        Refresh();
    }

    [RelayCommand]
    private void ApplyPort()
    {
        if (!int.TryParse(PortText.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var port) ||
            !WebConsoleOptions.IsAllowedPort(port))
        {
            PortError = string.Create(
                CultureInfo.InvariantCulture,
                $"Enter a number from {WebConsoleOptions.LowestAllowedPort} to {WebConsoleOptions.HighestAllowedPort}.");
            return;
        }

        PortError = null;
        _controller.SetPort(port);
        Refresh();
    }

    [RelayCommand]
    private void NewAccessKey()
    {
        _controller.RegenerateAccessKey();
        Refresh();
    }

    [RelayCommand]
    private void CopyLink(string? link) => Copy(link ?? PrimaryLink, "Link copied");

    [RelayCommand]
    private void CopyAccessKey() => Copy(AccessKey, "Access key copied");

    /// <summary>Opens the console in this PC's own browser - a quick way to see exactly what the
    /// other device will see.</summary>
    [RelayCommand]
    private void OpenHere()
    {
        if (IsRunning)
        {
            _urlLauncher.Open(WebConsoleLinks.Format("localhost", _controller.Server.Port, AccessKey));
        }
    }

    private void Copy(string text, string confirmation)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        CopyFailed = !_clipboard.SetText(text);
        CopyResult = CopyFailed ? "Couldn't copy. Please try again." : confirmation;
    }

    private void OnServerStateChanged(object? sender, EventArgs e) =>
        _ = _dispatcher.InvokeAsync(Refresh);

    private void Refresh()
    {
        if (_disposed)
        {
            return;
        }

        var server = _controller.Server;
        _refreshing = true;
        try
        {
            IsEnabled = _controller.IsEnabled;
        }
        finally
        {
            _refreshing = false;
        }

        IsRunning = server.State == WebConsoleState.Running;
        HasFailed = server.State == WebConsoleState.Failed;
        AccessKey = _controller.AccessKey;
        CopyResult = null;
        CopyFailed = false;

        StatusText = server.State switch
        {
            WebConsoleState.Running => "On. Open the link below on your phone or another computer on the same network.",
            WebConsoleState.Failed => server.ErrorMessage ?? "The web console could not start.",
            _ => "Off. Nothing is shared.",
        };

        OtherLinks.Clear();
        if (!IsRunning)
        {
            PrimaryLink = string.Empty;
            OnPropertyChanged(nameof(HasOtherLinks));
            return;
        }

        var hosts = _addressProvider.GetAddresses().ToList();
        hosts.Add(Environment.MachineName);
        PrimaryLink = WebConsoleLinks.Format(hosts[0], server.Port, AccessKey);
        for (var i = 1; i < hosts.Count; i++)
        {
            OtherLinks.Add(WebConsoleLinks.Format(hosts[i], server.Port, AccessKey));
        }

        OnPropertyChanged(nameof(HasOtherLinks));
    }
}
