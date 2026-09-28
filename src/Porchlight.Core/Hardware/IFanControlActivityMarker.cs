namespace Porchlight.Core.Hardware;

/// <summary>
/// S8: persists a marker file for as long as at least one fan is under software control this
/// session, so a later launch can tell whether the previous session ended without a chance to
/// restore fans to BIOS control (forced kill, crash to desktop, BSOD, or power loss all skip every
/// normal restore path). Porchlight cannot itself put a fan back under BIOS control after such an
/// event - only a PC restart re-initializes the EC/SuperIO - so the only honest thing to do is warn
/// the user plainly.
/// </summary>
public interface IFanControlActivityMarker
{
    /// <summary>True if the marker was left behind by a previous run that never cleared it.</summary>
    bool Exists();

    /// <summary>Written when software control starts actively driving at least one fan.</summary>
    void Create();

    /// <summary>Cleared whenever no fan is under software control any more (including every rule-5
    /// restore path). Safe to call when no marker exists.</summary>
    void Delete();
}
