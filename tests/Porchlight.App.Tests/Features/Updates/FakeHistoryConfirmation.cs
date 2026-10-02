using Porchlight.App.Features.Cleanup;

namespace Porchlight.App.Tests.Features.Updates;

/// <summary>Confirmation dialog double: answers with <see cref="Answer"/> and records the last message.</summary>
internal sealed class FakeHistoryConfirmation : IConfirmationDialog
{
    public bool Answer { get; set; } = true;

    public int Calls { get; private set; }

    public string? LastMessage { get; private set; }

    public bool Confirm(string title, string message)
    {
        Calls++;
        LastMessage = message;
        return Answer;
    }
}
