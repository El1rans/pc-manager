namespace Porchlight.Core.Browsers;

/// <summary>Already-classified start and search settings of one browser profile. A null
/// <paramref name="NewTabPage"/> means that setting is not checked for this browser.</summary>
internal sealed record ProfileSettings(
    string ProfileName,
    HijackClassification HomePage,
    HijackClassification StartupPages,
    HijackClassification? NewTabPage,
    HijackClassification SearchEngine);
