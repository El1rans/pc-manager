using Porchlight.Core.Winget;

namespace Porchlight.Core.RemoveApps;

/// <summary>Result of <see cref="IRemoveAppsService.RemoveAsync"/>.</summary>
/// <param name="Result">What happened.</param>
/// <param name="WingetOutcome">winget's plain-language outcome when winget ran; otherwise null.</param>
public sealed record RemoveAppOutcome(RemoveAppResult Result, WingetOutcome? WingetOutcome = null);
