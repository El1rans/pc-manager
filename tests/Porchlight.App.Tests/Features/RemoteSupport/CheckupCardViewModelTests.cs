using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Porchlight.App.Features.RemoteSupport;
using Porchlight.App.Tests.Features.Updates;
using Porchlight.Core.Settings;
using Xunit;

namespace Porchlight.App.Tests.Features.RemoteSupport;

public sealed class CheckupCardViewModelTests : IDisposable
{
    private readonly string _directory;
    private readonly SettingsStore _settingsStore;
    private readonly FakeClipboardService _clipboard = new();
    private readonly FakeUrlLauncher _urlLauncher = new();
    private readonly FakeFileDialogService _dialogs = new();

    public CheckupCardViewModelTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "PorchlightAppTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _settingsStore = new SettingsStore(NullLogger<SettingsStore>.Instance, Path.Combine(_directory, "settings.json"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private CheckupCardViewModel Create() =>
        new(new FakeCheckupReportBuilder(), _clipboard, _urlLauncher, _dialogs, _settingsStore, TimeProvider.System,
            NullLogger<CheckupCardViewModel>.Instance);

    [Fact]
    public void Title_UsesHelperNameOrFallback()
    {
        var viewModel = Create();
        Assert.Equal("Send a check-up to your helper", viewModel.Title);

        viewModel.SetHelperName("Dana");

        Assert.Equal("Send a check-up to Dana", viewModel.Title);
    }

    [Fact]
    public async Task ActionsDisabledUntilReportCreated()
    {
        var viewModel = Create();
        Assert.False(viewModel.CopyReportCommand.CanExecute(null));

        await viewModel.CreateCheckupCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasReport);
        Assert.True(viewModel.CopyReportCommand.CanExecute(null));
        Assert.Contains("Drive space", viewModel.PreviewText);
    }

    [Fact]
    public async Task Copy_PutsPreviewOnClipboard()
    {
        var viewModel = Create();
        await viewModel.CreateCheckupCommand.ExecuteAsync(null);

        viewModel.CopyReportCommand.Execute(null);

        Assert.Equal(viewModel.PreviewText, _clipboard.LastText);
        Assert.False(viewModel.StatusIsError);
    }

    [Fact]
    public async Task Email_OpensMailtoWithHelperEmailAndPersistsIt()
    {
        var viewModel = Create();
        viewModel.HelperEmail = "helper@example.com";
        await viewModel.CreateCheckupCommand.ExecuteAsync(null);

        viewModel.EmailReportCommand.Execute(null);

        Assert.StartsWith("mailto:helper@example.com?", _urlLauncher.OpenedUrls.Single());
        Assert.Equal("helper@example.com", _settingsStore.Current.RemoteSupport.HelperEmail);
    }

    [Theory]
    [InlineData(".html", "<!DOCTYPE html>")]
    [InlineData(".txt", "Porchlight check-up")]
    public async Task Save_WritesFormatMatchingExtension(string extension, string expectedStart)
    {
        var path = Path.Combine(_directory, "report" + extension);
        _dialogs.SavePath = path;
        var viewModel = Create();
        await viewModel.CreateCheckupCommand.ExecuteAsync(null);

        await viewModel.SaveReportCommand.ExecuteAsync(null);

        Assert.StartsWith(expectedStart, await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
        Assert.EndsWith(".html", _dialogs.LastDefaultFileName);
    }

    [Fact]
    public async Task Save_WriteFailure_ShowsPlainError()
    {
        _dialogs.SavePath = Path.Combine(_directory, "missing-folder", "r.html");
        var viewModel = Create();
        await viewModel.CreateCheckupCommand.ExecuteAsync(null);

        await viewModel.SaveReportCommand.ExecuteAsync(null);

        Assert.True(viewModel.StatusIsError);
    }

    [Fact]
    public async Task Save_Cancelled_DoesNothing()
    {
        _dialogs.SavePath = null;
        var viewModel = Create();
        await viewModel.CreateCheckupCommand.ExecuteAsync(null);

        await viewModel.SaveReportCommand.ExecuteAsync(null);

        Assert.Equal(string.Empty, viewModel.StatusMessage);
    }
}
