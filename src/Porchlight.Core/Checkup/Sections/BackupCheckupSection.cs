using Porchlight.Core.Backup;

namespace Porchlight.Core.Checkup.Sections;

/// <summary>Whether anything is backing up the person's files. Never names backup programs or shows
/// account details (docs/specs/16-checkup-report.md privacy rules).</summary>
public sealed class BackupCheckupSection(IBackupStatusService backupStatus, TimeProvider timeProvider) : ICheckupSection
{
    public string Title => "Backups";

    public int Order => 350;

    public async Task<CheckupSectionResult?> BuildAsync(CancellationToken cancellationToken)
    {
        var result = await backupStatus.GetAsync(cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded || result.Value is null)
        {
            return new CheckupSectionResult(Title, CheckupSeverity.NeedsAttention, ["Porchlight could not check the backups."]);
        }

        var assessment = BackupVerdictEvaluator.Evaluate(result.Value, timeProvider.GetUtcNow());
        var severity = assessment.Verdict switch
        {
            BackupVerdict.Good => CheckupSeverity.Ok,
            BackupVerdict.Warning => CheckupSeverity.NeedsAttention,
            _ => CheckupSeverity.Problem,
        };

        List<string> lines = [assessment.Headline, .. assessment.Lines];
        if (result.Value.OtherTools.Count > 0)
        {
            lines.Add("Another backup program is installed.");
        }

        if (assessment.Nudge is { } nudge)
        {
            lines.Add(nudge);
        }

        return new CheckupSectionResult(Title, severity, lines);
    }
}
