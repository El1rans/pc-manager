namespace Porchlight.Core.SelfUpdate;

/// <summary>A self-update step failed. <see cref="Exception.Message"/> is already a friendly,
/// plain-language sentence that can be shown to the user as is.</summary>
public sealed class SelfUpdateException : Exception
{
    public SelfUpdateException(string friendlyMessage) : base(friendlyMessage)
    {
    }

    public SelfUpdateException(string friendlyMessage, Exception innerException) : base(friendlyMessage, innerException)
    {
    }

    public SelfUpdateException()
    {
    }
}
