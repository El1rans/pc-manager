using Porchlight.App.Features.WebConsole;
using Porchlight.App.Tests.Features.Lighting;
using Porchlight.App.Tests.Features.RemoteSupport;
using Porchlight.Core.WebConsole;
using Xunit;

namespace Porchlight.App.Tests.Features.WebConsole;

public sealed class WebConsoleViewModelTests : IDisposable
{
    private readonly FakeServer _server = new();
    private readonly FakeSettingsStore _settings = new();
    private readonly FakeClipboardService _clipboard = new();
    private readonly FakeUrlLauncher _urlLauncher = new();
    private readonly WebConsoleViewModel _viewModel;

    public WebConsoleViewModelTests()
    {
        _viewModel = new WebConsoleViewModel(
            new WebConsoleController(_server, _settings),
            new FixedAddresses(),
            _clipboard,
            _urlLauncher);
    }

    public void Dispose() => _viewModel.Dispose();

    [Fact]
    public void Starts_off_with_no_link()
    {
        Assert.False(_viewModel.IsEnabled);
        Assert.False(_viewModel.IsRunning);
        Assert.Equal(string.Empty, _viewModel.PrimaryLink);
        Assert.Equal("8765", _viewModel.PortText);
    }

    [Fact]
    public void Turning_on_shows_links_carrying_the_access_key()
    {
        _viewModel.IsEnabled = true;

        Assert.True(_settings.Current.WebConsole.Enabled);
        Assert.True(_viewModel.IsRunning);
        var key = _settings.Current.WebConsole.AccessKey;
        Assert.Equal(key, _viewModel.AccessKey);
        Assert.Equal($"http://192.168.1.20:8765/#key={key}", _viewModel.PrimaryLink);
        Assert.Contains($"http://{Environment.MachineName}:8765/#key={key}", _viewModel.OtherLinks);
    }

    [Fact]
    public void Copy_link_puts_the_primary_link_on_the_clipboard()
    {
        _viewModel.IsEnabled = true;

        _viewModel.CopyLinkCommand.Execute(null);

        Assert.Equal(_viewModel.PrimaryLink, _clipboard.LastText);
        Assert.Equal("Link copied", _viewModel.CopyResult);
    }

    [Fact]
    public void Open_here_opens_the_local_link()
    {
        _viewModel.IsEnabled = true;

        _viewModel.OpenHereCommand.Execute(null);

        Assert.Equal($"http://localhost:8765/#key={_viewModel.AccessKey}", Assert.Single(_urlLauncher.OpenedUrls));
    }

    [Theory]
    [InlineData("80")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("-1")]
    public void An_invalid_port_is_explained_and_not_saved(string port)
    {
        _viewModel.PortText = port;

        _viewModel.ApplyPortCommand.Execute(null);

        Assert.False(string.IsNullOrEmpty(_viewModel.PortError));
        Assert.Equal(WebConsoleOptions.DefaultPort, _settings.Current.WebConsole.Port);
    }

    [Fact]
    public void A_valid_port_is_saved_and_used()
    {
        _viewModel.IsEnabled = true;
        _viewModel.PortText = "9000";

        _viewModel.ApplyPortCommand.Execute(null);

        Assert.Null(_viewModel.PortError);
        Assert.Equal(9000, _settings.Current.WebConsole.Port);
        Assert.StartsWith("http://192.168.1.20:9000/", _viewModel.PrimaryLink, StringComparison.Ordinal);
    }

    [Fact]
    public void A_new_key_updates_the_link()
    {
        _viewModel.IsEnabled = true;
        var oldLink = _viewModel.PrimaryLink;

        _viewModel.NewAccessKeyCommand.Execute(null);

        Assert.NotEqual(oldLink, _viewModel.PrimaryLink);
        Assert.EndsWith(_settings.Current.WebConsole.AccessKey, _viewModel.PrimaryLink, StringComparison.Ordinal);
    }

    [Fact]
    public void Dispose_twice_does_not_throw()
    {
        _viewModel.Dispose();

        Assert.Null(Record.Exception(_viewModel.Dispose));
    }

    private sealed class FixedAddresses : ILocalAddressProvider
    {
        public IReadOnlyList<string> GetAddresses() => ["192.168.1.20"];
    }

    private sealed class FakeServer : IWebConsoleServer
    {
        public WebConsoleState State { get; private set; }

        public int Port { get; private set; }

        public string? ErrorMessage => null;

        public event EventHandler? StateChanged;

        public void Start(int port, string accessKey)
        {
            State = WebConsoleState.Running;
            Port = port;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Stop()
        {
            State = WebConsoleState.Stopped;
            Port = 0;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
