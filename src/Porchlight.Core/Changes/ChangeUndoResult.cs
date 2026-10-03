namespace Porchlight.Core.Changes;

/// <summary>Result of an undo, with the plain sentence to show.</summary>
public sealed record ChangeUndoResult(bool Succeeded, string Message)
{
    public static ChangeUndoResult Ok(string message) => new(true, message);

    public static ChangeUndoResult Fail(string message) => new(false, message);
}
