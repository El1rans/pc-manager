namespace Porchlight.Core.Safety;

/// <summary>Result of the "Who can connect to this PC?" card.</summary>
/// <param name="Tools">Remote-control tools found (empty when none).</param>
public sealed record RemoteAccessStatus(IReadOnlyList<RemoteToolFinding> Tools)
{
    /// <summary>Shown under any tool that Porchlight did not set up.</summary>
    public const string UnknownToolWarning =
        "If you didn't set this up, someone may be able to control this PC. Ask your family helper.";

    public const string PorchlightLabel = "Set up by Porchlight";

    /// <summary>Tools that need a second look (everything except Porchlight's own AnyDesk).</summary>
    public IEnumerable<RemoteToolFinding> OtherTools => Tools.Where(t => !t.SetUpByPorchlight);

    public SafetyLevel Level => OtherTools.Any() ? SafetyLevel.Attention : SafetyLevel.Good;

    public string Verdict
    {
        get
        {
            var others = OtherTools.Count();
            if (others > 0)
            {
                return others == 1
                    ? "1 remote-control program found"
                    : $"{others} remote-control programs found";
            }

            return Tools.Count > 0
                ? "Only the remote support set up by Porchlight"
                : "No remote-control programs found";
        }
    }
}
