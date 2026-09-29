namespace Porchlight.Core.Health;

/// <summary>Whether a restore point can be created right now, and the plain reason.</summary>
public sealed record RestorePointAvailability(bool CanCreate, string Message);
