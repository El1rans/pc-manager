using Porchlight.Core.Changes;

namespace Porchlight.Core.WindowsServices;

/// <summary>Plain sentences for a failed service undo.</summary>
internal static class ServiceUndoMessages
{
    public static ChangeUndoResult From(ServiceChangeOutcome outcome, string successMessage) =>
        outcome.Result switch
        {
            ServiceChangeResult.Changed => ChangeUndoResult.Ok(successMessage),
            ServiceChangeResult.NeedsAdmin => ChangeUndoResult.Fail(
                "Changing a service needs administrator rights. Use Restart as administrator, then try again."),
            ServiceChangeResult.NotFound => ChangeUndoResult.Fail("This service is gone."),
            ServiceChangeResult.Refused => ChangeUndoResult.Fail("Porchlight doesn't change this service."),
            ServiceChangeResult.HasDependents => ChangeUndoResult.Fail(
                $"Other services still need it: {string.Join(", ", outcome.Dependents)}. Stop those first."),
            ServiceChangeResult.TimedOut => ChangeUndoResult.Fail("The service didn't respond in time. Try again."),
            _ => ChangeUndoResult.Fail("Couldn't undo this. Try again, or ask for help."),
        };
}
