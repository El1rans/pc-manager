using Porchlight.Core.Cleanup;
using Porchlight.Core.RemoveApps;
using Xunit;

namespace Porchlight.Core.Tests.RemoveApps;

public sealed class AppProtectionRulesTests
{
    internal static InstalledApp App(string name, string? publisher = "Some Publisher", string? version = "1.0") =>
        new(name, publisher, version, null, null, @"""C:\x\unins000.exe""", true);

    [Theory]
    [InlineData("Porchlight")]
    [InlineData("Porchlight 0.2.0")]
    [InlineData("porchlight")]
    public void Classify_Porchlight_IsNeverListed(string name) =>
        Assert.Equal(RemovableAppKind.Porchlight, AppProtectionRules.Classify(App(name)));

    [Theory]
    [InlineData("AnyDesk")]
    [InlineData("OpenRGB")]
    [InlineData("PawnIO")]
    public void Classify_ManagedComponents_AreManagedByPorchlight(string name) =>
        Assert.Equal(RemovableAppKind.ManagedByPorchlight, AppProtectionRules.Classify(App(name)));

    [Theory]
    [InlineData("Microsoft Visual C++ 2015-2022 Redistributable (x64) - 14.40.33810", "Microsoft Corporation")]
    [InlineData("Microsoft .NET Runtime - 8.0.8 (x64)", "Microsoft Corporation")]
    [InlineData("Microsoft Windows Desktop Runtime - 8.0.8 (x64)", "Microsoft Corporation")]
    [InlineData("Microsoft ASP.NET Core 8.0.8 Shared Framework (x64)", "Microsoft Corporation")]
    [InlineData("Windows App SDK Runtime 1.5", "Microsoft Corporation")]
    [InlineData("Microsoft Edge WebView2 Runtime", "Microsoft Corporation")]
    [InlineData("Microsoft Edge", "Microsoft Corporation")]
    [InlineData("NVIDIA Graphics Driver 551.23", "NVIDIA Corporation")]
    [InlineData("Intel(R) Chipset Device Software", "Intel(R) Corporation")]
    [InlineData("Intel(R) Management Engine Components", "Intel Corporation")]
    [InlineData("Realtek High Definition Audio", "Realtek Semiconductor Corp.")]
    [InlineData("Windows PC Health Check", "Microsoft Corporation")]
    public void Classify_RuntimesAndDrivers_AreSystemParts(string name, string publisher) =>
        Assert.Equal(RemovableAppKind.SystemPart, AppProtectionRules.Classify(App(name, publisher)));

    [Theory]
    [InlineData("VLC media player", "VideoLAN")]
    [InlineData("Google Chrome", "Google LLC")]
    [InlineData("7-Zip 23.01 (x64)", "Igor Pavlov")]
    [InlineData("NVIDIA GeForce Experience", "NVIDIA Corporation")]
    [InlineData("McAfee LiveSafe", "McAfee, LLC")]
    public void Classify_OrdinaryApps_AreNormal(string name, string publisher) =>
        Assert.Equal(RemovableAppKind.Normal, AppProtectionRules.Classify(App(name, publisher)));

    [Fact]
    public void Classify_WindowsPrefixFromAnotherPublisher_IsNotASystemPart() =>
        Assert.Equal(RemovableAppKind.Normal, AppProtectionRules.Classify(App("Windows Movie Tools", "Acme")));

    [Fact]
    public void CanRemove_OnlyForNormalAndSystemParts()
    {
        var app = App("X");
        Assert.True(new RemovableApp(app, RemovableAppKind.Normal, false, null).CanRemove);
        Assert.True(new RemovableApp(app, RemovableAppKind.SystemPart, false, null).CanRemove);
        Assert.False(new RemovableApp(app, RemovableAppKind.ManagedByPorchlight, false, null).CanRemove);
        Assert.False(new RemovableApp(app, RemovableAppKind.Porchlight, false, null).CanRemove);
    }

    [Theory]
    [InlineData("McAfee LiveSafe", true)]
    [InlineData("Norton 360", true)]
    [InlineData("WildTangent Games App", true)]
    [InlineData("HP JumpStart Bridge", true)]
    [InlineData("Dell SupportAssist", true)]
    [InlineData("Candy Crush Saga", true)]
    [InlineData("VLC media player", false)]
    [InlineData("Notepad++", false)]
    public void OftenPreinstalled_MatchesTheTable(string name, bool expected) =>
        Assert.Equal(expected, OftenPreinstalledCatalog.IsOftenPreinstalled(name));
}
