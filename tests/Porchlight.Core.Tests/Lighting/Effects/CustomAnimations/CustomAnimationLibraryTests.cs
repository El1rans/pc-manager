using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Porchlight.Core.Lighting;
using Porchlight.Core.Lighting.Effects;
using Porchlight.Core.Lighting.Effects.CustomAnimations;
using Porchlight.Core.Tests.Hardware;
using Xunit;

namespace Porchlight.Core.Tests.Lighting.Effects.CustomAnimations;

public sealed class CustomAnimationLibraryTests : IDisposable
{
    private const string Valid = """{ "name": "Police Lights!", "frames": [ { "fill": "#FF0000" } ] }""";

    private readonly string _directory;
    private readonly CustomAnimationLibrary _library;

    public CustomAnimationLibraryTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "PorchlightTests_" + Guid.NewGuid().ToString("N"));
        _library = new CustomAnimationLibrary(Path.Combine(_directory, "Animations"), NullLogger<CustomAnimationLibrary>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void List_NoFolderYet_IsEmpty() => Assert.Empty(_library.List());

    [Fact]
    public void ImportText_Valid_StoresUnderIdDerivedFromName()
    {
        var result = _library.ImportText(Valid);

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal("police-lights", result.Imported!.Id);
        Assert.True(File.Exists(Path.Combine(_directory, "Animations", "police-lights.json")));
        var listed = Assert.Single(_library.List());
        Assert.Equal("Police Lights!", listed.Name);
        Assert.NotNull(_library.Load("police-lights"));
    }

    [Fact]
    public void ImportText_Invalid_ReturnsErrorAndStoresNothing()
    {
        var result = _library.ImportText("""{ "name": "x" }""");

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Error);
        Assert.Empty(_library.List());
    }

    [Fact]
    public void ImportText_SameNameTwice_ReplacesExisting()
    {
        _library.ImportText(Valid);
        _library.ImportText("""{ "name": "Police Lights!", "frames": [ { "fill": "#0000FF" }, { "fill": "#FF0000" } ] }""");

        var listed = Assert.Single(_library.List());
        Assert.Equal(2, listed.FrameCount);
    }

    [Fact]
    public void ImportFile_ReadsAndImports()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "download.json");
        File.WriteAllText(path, Valid);

        var result = _library.ImportFile(path);

        Assert.True(result.Succeeded, result.Error);
    }

    [Fact]
    public void ImportFile_Missing_ReturnsError()
    {
        var result = _library.ImportFile(Path.Combine(_directory, "nope.json"));

        Assert.False(result.Succeeded);
        Assert.Equal("That file doesn't exist.", result.Error);
    }

    [Fact]
    public void ImportFile_TooLarge_RejectedBeforeReading()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "big.json");
        File.WriteAllText(path, new string(' ', CustomAnimationParser.MaxDocumentLength + 1));

        var result = _library.ImportFile(path);

        Assert.Contains("too large", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Remove_DeletesAnimation()
    {
        _library.ImportText(Valid);

        Assert.True(_library.Remove("police-lights"));
        Assert.Empty(_library.List());
        Assert.Null(_library.Load("police-lights"));
        Assert.False(_library.Remove("police-lights"));
    }

    [Theory]
    [InlineData("../settings")]
    [InlineData("..\\settings")]
    [InlineData("C:\\Windows\\win")]
    [InlineData("Upper")]
    [InlineData("-lead")]
    [InlineData("trail-")]
    [InlineData("double--dash")]
    [InlineData("")]
    public void IsValidId_RejectsAnythingThatCouldEscapeTheFolder(string id)
    {
        Assert.False(CustomAnimationLibrary.IsValidId(id));
        Assert.Null(_library.Load(id));
        Assert.False(_library.Remove(id));
    }

    [Theory]
    [InlineData("Police Lights!", "police-lights")]
    [InlineData("  --Rainbow   2000--  ", "rainbow-2000")]
    [InlineData("Café", "caf")]
    public void IdFor_Slugifies(string name, string expected)
    {
        Assert.Equal(expected, CustomAnimationLibrary.IdFor(name));
        Assert.True(CustomAnimationLibrary.IsValidId(expected));
    }

    [Fact]
    public void IdFor_NonLatinNames_GetDistinctValidIds()
    {
        var first = CustomAnimationLibrary.IdFor("שקיעה");
        var second = CustomAnimationLibrary.IdFor("גשם");

        Assert.NotEqual(first, second);
        Assert.True(CustomAnimationLibrary.IsValidId(first));
        Assert.Equal(first, CustomAnimationLibrary.IdFor("שקיעה"));
    }

    [Fact]
    public void List_SkipsFilesThatNoLongerParse()
    {
        _library.ImportText(Valid);
        File.WriteAllText(Path.Combine(_directory, "Animations", "broken.json"), "{ not json");

        Assert.Single(_library.List());
    }

    [Fact]
    public void EffectRegistry_CustomAnimation_LoadsFromLibrary()
    {
        _library.ImportText(Valid);
        var settings = new Dictionary<string, string> { ["animationId"] = "police-lights", ["speed"] = "2" };

        var effect = Assert.IsType<CustomAnimationEffect>(
            EffectRegistry.Create(EffectRegistry.CustomAnimationEffectName, settings, _library));

        Assert.Equal(2, effect.Speed);
        Assert.Equal("Police Lights!", effect.Animation.Name);
    }

    [Fact]
    public void EffectRegistry_CustomAnimation_MissingAnimationOrLibrary_ReturnsNull()
    {
        var settings = new Dictionary<string, string> { ["animationId"] = "does-not-exist" };

        Assert.Null(EffectRegistry.Create(EffectRegistry.CustomAnimationEffectName, settings, _library));
        Assert.Null(EffectRegistry.Create(EffectRegistry.CustomAnimationEffectName, settings));
        Assert.Null(EffectRegistry.Create(EffectRegistry.CustomAnimationEffectName, null, _library));
    }

    [Fact]
    public void EffectEngine_CustomAnimationAssignment_RendersImportedAnimation()
    {
        _library.ImportText(Valid);
        var client = new FakeEffectDeviceClient();
        client.Devices.Add(new EffectDeviceInfo(
            0,
            "Strip",
            3,
            [.. Enumerable.Range(0, 3).Select(i => new EffectLedInfo(i, $"LED {i}"))],
            [new EffectZoneInfo(0, "Zone", EffectZoneType.Linear, 3, 0, null, null, null)],
            [new EffectModeInfo(0, "Direct", IsPerLed: true)],
            0));
        var timeProvider = new FakeTimeProvider();
        using var engine = new EffectEngine(
            client, new FakeHardwareService(), new FakePendingUpdateCountProvider(), new FakeDeviceExclusionProvider(),
            new FakeKeyPressSource(), NullLogger<EffectEngine>.Instance, timeProvider, _library);
        engine.SetAssignments(
        [
            new EffectAssignment
            {
                DeviceKey = "Strip",
                EffectName = EffectRegistry.CustomAnimationEffectName,
                Settings = new() { [EffectRegistry.CustomAnimationIdSetting] = "police-lights" },
            },
        ]);

        engine.Start(fps: 10);
        timeProvider.Advance(TimeSpan.FromSeconds(0.1));

        var (_, colors) = Assert.Single(client.UpdateLedsCalls);
        Assert.All(colors, c => Assert.Equal(new RgbColor(255, 0, 0), c));
    }
}
