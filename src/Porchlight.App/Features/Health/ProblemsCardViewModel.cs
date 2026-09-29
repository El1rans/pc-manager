using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Porchlight.Core.Health;

namespace Porchlight.App.Features.Health;

/// <summary>The Recent problems card.</summary>
public sealed partial class ProblemsCardViewModel(IProblemEventReader reader) : HealthCardViewModelBase
{
    [ObservableProperty]
    private bool _showNoProblems;

    public ObservableCollection<ProblemRowViewModel> Problems { get; } = [];

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822", Justification = "Bound from XAML, needs an instance property.")]
    public string NoProblemsText => ProblemSummarizer.NoProblemsText;

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        IsChecking = true;
        try
        {
            var result = await reader.ReadAsync(cancellationToken);
            Problems.Clear();
            ShowNoProblems = false;
            if (!result.Succeeded || result.Value is null)
            {
                SetFailure(result.Error);
                return;
            }

            ErrorText = null;
            foreach (var summary in ProblemSummarizer.Summarize(result.Value, DateTimeOffset.Now))
            {
                Problems.Add(new ProblemRowViewModel(summary));
            }

            ShowNoProblems = Problems.Count == 0;
        }
        finally
        {
            IsChecking = false;
        }
    }
}
