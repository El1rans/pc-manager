using CommunityToolkit.Mvvm.ComponentModel;
using Porchlight.App.Shell;
using Porchlight.Core.Settings;
using Porchlight.Core.Startup;

namespace Porchlight.App.Features.Settings;

/// <summary>The Settings > General page: theme, start at sign-in and keep-in-tray. Every change is
/// written straight to <see cref="ISettingsStore.Update"/> (no Save button), except start at
/// sign-in, which registers or removes a scheduled task and reflects that task's real state.</summary>
public sealed partial class GeneralSettingsViewModel : PageViewModelBase
{
    private readonly ISettingsStore _settingsStore;
    private readonly ILoginLaunchService _loginLaunch;
    private readonly IThemeService? _themeService;

    [ObservableProperty]
    private bool _keepRunningInTray;

    [ObservableProperty]
    private ThemeOption _selectedTheme;

    /// <summary>True while the sign-in checkbox's change is being applied, and until its real state
    /// has been read.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanChangeStartAtLogin))]
    private bool _isStartAtLoginBusy = true;

    /// <summary>Friendly text shown under the checkbox when enabling/disabling failed.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStartAtLoginError))]
    private string? _startAtLoginError;

    /// <summary>Reflects the real scheduled-task state, not a saved setting. Changing it (by the user)
    /// registers or removes the task.</summary>
    [ObservableProperty]
    private bool _startAtLogin;

    /// <summary>Set while the property is changed by code (loading, reverting) so it does not re-run the task.</summary>
    private bool _suppressStartAtLogin;

    public GeneralSettingsViewModel(ISettingsStore settingsStore, ILoginLaunchService loginLaunch, IThemeService? themeService = null)
    {
        _settingsStore = settingsStore;
        _loginLaunch = loginLaunch;
        _themeService = themeService;

        // Assigned to the fields, not the properties: loading must not write settings back.
        _keepRunningInTray = settingsStore.Current.Notifications.KeepRunningInTray;
        _selectedTheme = ThemeOptions.FirstOrDefault(o => o.Value == settingsStore.Current.Appearance.Theme) ?? ThemeOptions[0];
    }

    public override string Title => "General";

    // Segoe Fluent Icons "Settings" (gear). The rail shows the category's glyph; this one is unused.
    public override string Glyph => "";

    public override int Order => 0;

    public override PageCategory Category => PageCategory.Settings;

    public IReadOnlyList<ThemeOption> ThemeOptions { get; } =
    [
        new(AppTheme.System, "Match Windows"),
        new(AppTheme.Light, "Light"),
        new(AppTheme.Dark, "Dark"),
    ];

    public bool CanChangeStartAtLogin => !IsStartAtLoginBusy;

    public bool HasStartAtLoginError => !string.IsNullOrEmpty(StartAtLoginError);

    /// <summary>Reads the real task state every time the page is shown, so the box is right even if
    /// the task was changed elsewhere (or the app moved).</summary>
    public override Task OnNavigatedToAsync(CancellationToken cancellationToken) => LoadStartAtLoginAsync();

    /// <summary>Reads the real task state. If it cannot be read the box stays off and is enabled so
    /// the user can still try.</summary>
    public async Task LoadStartAtLoginAsync()
    {
        try
        {
            var state = await Task.Run(() => _loginLaunch.GetStateAsync(CancellationToken.None)).ConfigureAwait(true);
            SetStartAtLoginQuietly(state.Exists);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Reading is best-effort: the checkbox simply starts unchecked.
            SetStartAtLoginQuietly(false);
        }
        finally
        {
            IsStartAtLoginBusy = false;
        }
    }

    partial void OnStartAtLoginChanged(bool value)
    {
        if (_suppressStartAtLogin)
        {
            return;
        }

        _ = ApplyStartAtLoginAsync(value);
    }

    private async Task ApplyStartAtLoginAsync(bool enable)
    {
        StartAtLoginError = null;
        IsStartAtLoginBusy = true;
        try
        {
            var result = await Task.Run(
                () => enable ? _loginLaunch.EnableAsync(CancellationToken.None) : _loginLaunch.DisableAsync(CancellationToken.None))
                .ConfigureAwait(true);
            if (!result.Succeeded)
            {
                SetStartAtLoginQuietly(!enable);
                StartAtLoginError = result.Message ?? "Porchlight could not change this setting.";
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            SetStartAtLoginQuietly(!enable);
            StartAtLoginError = "Porchlight could not change this setting.";
        }
        finally
        {
            IsStartAtLoginBusy = false;
        }
    }

    private void SetStartAtLoginQuietly(bool value)
    {
        _suppressStartAtLogin = true;
        try
        {
            StartAtLogin = value;
        }
        finally
        {
            _suppressStartAtLogin = false;
        }
    }

    partial void OnKeepRunningInTrayChanged(bool value) => _settingsStore.Update(s => s.Notifications.KeepRunningInTray = value);

    partial void OnSelectedThemeChanged(ThemeOption value)
    {
        _settingsStore.Update(s => s.Appearance.Theme = value.Value);
        _themeService?.Apply(value.Value);
    }
}
