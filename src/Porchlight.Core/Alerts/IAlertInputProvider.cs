namespace Porchlight.Core.Alerts;

/// <summary>Gathers the current <see cref="AlertInputs"/>. Blocking (drive and registry reads), so
/// call it from a background thread.</summary>
public interface IAlertInputProvider
{
    AlertInputs GetInputs();
}
