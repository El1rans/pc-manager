using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Porchlight.App.Features.GetApps;

/// <summary>One row on the Get apps page: a search result or a curated popular app, with its
/// Install / Installed / installing state.</summary>
public sealed partial class AppResultViewModel : ObservableObject
{
    /// <summary>Shown (with a check icon) once the app is installed.</summary>
    public const string InstalledText = "Installed";

    private readonly Func<AppResultViewModel, Task> _install;
    private readonly Func<bool> _isAnotherInstallRunning;

    public AppResultViewModel(
        string name, string id, string version, Func<AppResultViewModel, Task> install, Func<bool> isAnotherInstallRunning)
    {
        Name = name;
        Id = id;
        Version = version;
        _install = install;
        _isAnotherInstallRunning = isAnotherInstallRunning;
    }

    public string Name { get; }

    /// <summary>The exact winget package id, shown as secondary text and passed to winget.</summary>
    public string Id { get; }

    /// <summary>Latest version, or empty for the curated popular list (which is not searched).</summary>
    public string Version { get; }

    public string VersionText => string.IsNullOrEmpty(Version) ? string.Empty : "Version " + Version;

    public string InstallAutomationName => "Install " + Name;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowInstallButton))]
    [NotifyPropertyChangedFor(nameof(ShowResultText))]
    [NotifyCanExecuteChangedFor(nameof(InstallCommand))]
    private bool _isInstalled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowInstallButton))]
    [NotifyCanExecuteChangedFor(nameof(InstallCommand))]
    private bool _isInstalling;

    /// <summary>winget's live progress text while installing.</summary>
    [ObservableProperty]
    private string? _progressText;

    /// <summary>A plain sentence about how the last install turned out; null before one has run.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowResultText))]
    private string? _resultText;

    /// <summary>True when <see cref="ResultText"/> describes a problem (shown with a warning icon).</summary>
    [ObservableProperty]
    private bool _resultIsError;

    public bool ShowInstallButton => !IsInstalled && !IsInstalling;

    /// <summary>The result line is redundant next to the "Installed" label, so it only shows for a
    /// failure or for extra information such as "restart needed".</summary>
    public bool ShowResultText =>
        !string.IsNullOrEmpty(ResultText) && !(IsInstalled && ResultText == InstalledText);

    /// <summary>Re-evaluates <see cref="InstallCommand"/> after another row started or finished installing.</summary>
    public void RefreshCanInstall() => InstallCommand.NotifyCanExecuteChanged();

    private bool CanInstall() => !IsInstalled && !IsInstalling && !_isAnotherInstallRunning();

    [RelayCommand(CanExecute = nameof(CanInstall))]
    private Task InstallAsync() => _install(this);
}
