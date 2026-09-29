using Porchlight.Core.WebConsole;
using Xunit;

namespace Porchlight.Core.Tests.WebConsole;

public class WebConsoleControllerTests
{
    private readonly FakeWebConsoleServer _server = new();
    private readonly FakeWebConsoleSettingsStore _settings = new();
    private readonly WebConsoleController _controller;

    public WebConsoleControllerTests()
    {
        _controller = new WebConsoleController(_server, _settings);
    }

    [Fact]
    public void Console_is_off_by_default()
    {
        _controller.ApplySettings();

        Assert.False(_controller.IsEnabled);
        Assert.Equal(WebConsoleState.Stopped, _server.State);
        Assert.Equal(0, _server.StartCount);
    }

    [Fact]
    public void Turning_on_creates_a_key_and_starts_on_the_default_port()
    {
        _controller.SetEnabled(true);

        Assert.True(_settings.Current.WebConsole.Enabled);
        Assert.Equal(32, _settings.Current.WebConsole.AccessKey.Length);
        Assert.Equal(WebConsoleState.Running, _server.State);
        Assert.Equal(WebConsoleOptions.DefaultPort, _server.Port);
        Assert.Equal(_settings.Current.WebConsole.AccessKey, _server.LastAccessKey);
    }

    [Fact]
    public void Turning_on_again_keeps_the_existing_key()
    {
        _settings.Current.WebConsole.AccessKey = "existing";

        _controller.SetEnabled(true);

        Assert.Equal("existing", _server.LastAccessKey);
    }

    [Fact]
    public void Turning_off_stops_the_server()
    {
        _controller.SetEnabled(true);

        _controller.SetEnabled(false);

        Assert.False(_settings.Current.WebConsole.Enabled);
        Assert.Equal(WebConsoleState.Stopped, _server.State);
    }

    [Fact]
    public void Changing_the_port_restarts_a_running_console_there()
    {
        _controller.SetEnabled(true);

        _controller.SetPort(9000);

        Assert.Equal(9000, _settings.Current.WebConsole.Port);
        Assert.Equal(9000, _server.Port);
        Assert.Equal(2, _server.StartCount);
    }

    [Fact]
    public void Changing_the_port_while_off_only_saves_it()
    {
        _controller.SetPort(9000);

        Assert.Equal(9000, _settings.Current.WebConsole.Port);
        Assert.Equal(0, _server.StartCount);
    }

    [Theory]
    [InlineData(80)]
    [InlineData(70000)]
    public void A_port_outside_the_allowed_range_is_refused(int port)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _controller.SetPort(port));
        Assert.Equal(WebConsoleOptions.DefaultPort, _settings.Current.WebConsole.Port);
    }

    [Fact]
    public void An_out_of_range_saved_port_falls_back_to_the_default()
    {
        _settings.Current.WebConsole.Port = 5;

        _controller.SetEnabled(true);

        Assert.Equal(WebConsoleOptions.DefaultPort, _server.Port);
    }

    [Fact]
    public void A_new_access_key_replaces_the_old_one_on_the_running_server()
    {
        _controller.SetEnabled(true);
        var oldKey = _server.LastAccessKey;

        _controller.RegenerateAccessKey();

        Assert.NotEqual(oldKey, _server.LastAccessKey);
        Assert.Equal(_settings.Current.WebConsole.AccessKey, _server.LastAccessKey);
    }
}
