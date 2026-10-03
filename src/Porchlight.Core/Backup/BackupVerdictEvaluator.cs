using System.Globalization;

namespace Porchlight.Core.Backup;

/// <summary>Turns what the backup sources said into one verdict and plain-language text. Pure.</summary>
public static class BackupVerdictEvaluator
{
    /// <summary>A backup that ran within this many days counts as recent.</summary>
    public const int RecentBackupDays = 7;

    private const int DaysPerWeek = 7;
    private const int DaysPerMonth = 30;
    private const int WeeksShownUntilDays = 60;

    private const string NothingBackingUp = "Nothing is backing up your files.";

    public static BackupAssessment Evaluate(BackupSnapshot snapshot, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var fileHistory = snapshot.FileHistory;
        var oneDrive = snapshot.OneDrive;

        var lines = new List<string> { DescribeFileHistory(fileHistory, now), DescribeOneDrive(oneDrive) };
        var otherNote = snapshot.OtherTools.Count == 0 ? null : $"Also found: {string.Join(", ", snapshot.OtherTools)}.";

        var fileHistoryOn = fileHistory is { IsConfigured: true, IsEnabled: true };
        var offerFileHistory = fileHistory is not null && !fileHistoryOn;

        var recent = fileHistory?.LastBackup is { } last && IsRecent(now - last);
        if (recent || oneDrive?.CoversDocumentsAndDesktop == true)
        {
            return new BackupAssessment(BackupVerdict.Good, "Your files are being backed up.", lines, null, false, otherNote);
        }

        if (fileHistory is { IsConfigured: true })
        {
            var headline = fileHistory.LastBackup is { } lastRun
                ? $"Your last backup was {DescribeAge(now - lastRun)}."
                : "Your backup has not run yet.";
            var nudge = fileHistoryOn
                ? "Make sure your backup drive is plugged in so it can catch up."
                : "File History is turned off. Turn it on so your files are copied again.";
            return new BackupAssessment(BackupVerdict.Warning, headline, lines, nudge, offerFileHistory, otherNote);
        }

        if (oneDrive?.CoversAnything == true)
        {
            return new BackupAssessment(
                BackupVerdict.Warning,
                "OneDrive only protects some of your files.",
                lines,
                "Open OneDrive and turn on backup for Desktop and Documents too.",
                offerFileHistory,
                otherNote);
        }

        if (fileHistory is null || oneDrive is null)
        {
            return new BackupAssessment(
                BackupVerdict.Warning,
                "Porchlight couldn't fully check your backups.",
                lines,
                "Open backup settings to see for yourself.",
                offerFileHistory,
                otherNote);
        }

        return new BackupAssessment(
            BackupVerdict.Problem,
            NothingBackingUp,
            lines,
            "Turn on File History with an external drive, or sign in to OneDrive and back up Documents and Desktop.",
            offerFileHistory,
            otherNote is null ? null : $"{otherNote} Porchlight can't tell whether they are working.");
    }

    /// <summary>Plain relative time, e.g. "today", "3 days ago", "2 weeks ago", "4 months ago".</summary>
    public static string DescribeAge(TimeSpan age)
    {
        var days = (int)Math.Max(0, Math.Floor(age.TotalDays));
        return days switch
        {
            0 => "today",
            1 => "yesterday",
            < DaysPerWeek * 2 => string.Create(CultureInfo.InvariantCulture, $"{days} days ago"),
            < WeeksShownUntilDays => string.Create(CultureInfo.InvariantCulture, $"{days / DaysPerWeek} weeks ago"),
            _ => string.Create(CultureInfo.InvariantCulture, $"{days / DaysPerMonth} months ago"),
        };
    }

    private static bool IsRecent(TimeSpan age) => age < TimeSpan.FromDays(RecentBackupDays);

    private static string DescribeFileHistory(FileHistoryStatus? status, DateTimeOffset now)
    {
        if (status is null)
        {
            return "File History: couldn't check.";
        }

        if (!status.IsConfigured)
        {
            return "File History is not set up.";
        }

        var last = status.LastBackup is { } lastRun ? $"Last backup {DescribeAge(now - lastRun)}." : "It has not backed up yet.";
        return status.IsEnabled ? $"File History is on. {last}" : $"File History is turned off. {last}";
    }

    private static string DescribeOneDrive(OneDriveStatus? status)
    {
        if (status is null)
        {
            return "OneDrive: couldn't check.";
        }

        if (!status.IsInstalled)
        {
            return "OneDrive is not installed.";
        }

        if (!status.IsSignedIn)
        {
            return "OneDrive is installed but not signed in.";
        }

        var protectedFolders = new List<string>();
        var unprotected = new List<string>();
        (status.DesktopProtected ? protectedFolders : unprotected).Add("Desktop");
        (status.DocumentsProtected ? protectedFolders : unprotected).Add("Documents");
        (status.PicturesProtected ? protectedFolders : unprotected).Add("Pictures");

        if (unprotected.Count == 0)
        {
            return "OneDrive is signed in and protects Desktop, Documents and Pictures.";
        }

        return protectedFolders.Count == 0
            ? "OneDrive is signed in but does not protect Desktop, Documents or Pictures."
            : $"OneDrive is signed in and protects {string.Join(" and ", protectedFolders)}, but not {string.Join(" or ", unprotected)}.";
    }
}
