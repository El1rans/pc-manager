namespace Porchlight.Core.Lighting;

/// <summary>
/// One detected source of lighting conflict (see docs/specs/05-lighting.md addendum, "Lighting
/// conflict warning"): something other than Porchlight/OpenRGB that can also claim control of a
/// device's RGB lighting, so the two fight over it (often seen as the lighting periodically
/// turning off). Purely informational - detecting one of these never changes any setting or stops
/// any process.
/// </summary>
/// <param name="Id">Stable id for the source, e.g. <c>"windows-dynamic-lighting"</c> or
/// <c>"vendor-lghub"</c>. Used as the dismiss key and in tests.</param>
/// <param name="Title">Short name shown as the warning's heading, e.g. "Windows Dynamic Lighting".</param>
/// <param name="Message">Plain-words explanation and advice, e.g. "...it can take over your
/// keyboard/mouse lighting. Turn it off in Settings &gt; Personalization &gt; Dynamic Lighting."</param>
/// <param name="ActionUri">
/// A URI a button can open to help resolve this (e.g. <c>ms-settings:personalization-lighting</c>),
/// or null if there is nothing to open (the advice is just "close the app").
/// </param>
/// <param name="ActionLabel">Label for the button that opens <see cref="ActionUri"/>; null when
/// <see cref="ActionUri"/> is null.</param>
public sealed record LightingConflictWarning(
    string Id,
    string Title,
    string Message,
    string? ActionUri = null,
    string? ActionLabel = null);
