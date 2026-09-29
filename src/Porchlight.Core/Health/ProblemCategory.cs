namespace Porchlight.Core.Health;

/// <summary>Kinds of recent problem. The declaration order is the display order (most serious first).</summary>
public enum ProblemCategory
{
    BlueScreen,
    DiskError,
    UnexpectedShutdown,
    FailedUpdate,
    AppCrash,
}
