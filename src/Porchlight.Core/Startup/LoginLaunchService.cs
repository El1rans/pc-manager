using System.Text;
using Microsoft.Extensions.Logging;
using Porchlight.Core.Elevation;
using Porchlight.Core.Processes;

namespace Porchlight.Core.Startup;

/// <inheritdoc cref="ILoginLaunchService"/>
public sealed class LoginLaunchService(
    IProcessRunner processRunner,
    IElevatedCommandRunner elevatedRunner,
    IElevationService elevation,
    ILoginLaunchEnvironment environment,
    ILogger<LoginLaunchService> logger) : ILoginLaunchService
{
    private const string SchTasks = "schtasks.exe";

    public async Task<LoginLaunchState> GetStateAsync(CancellationToken cancellationToken)
    {
        var result = await processRunner
            .RunAsync(SchTasks, ["/Query", "/TN", LoginLaunchTaskXml.TaskName, "/XML"], null, null, cancellationToken)
            .ConfigureAwait(false);

        // A non-zero exit code is how schtasks reports "no such task".
        if (result.ExitCode != 0)
        {
            return new LoginLaunchState(false, null, null);
        }

        var action = LoginLaunchTaskXml.TryParseAction(string.Join(Environment.NewLine, result.StandardOutputLines));
        return new LoginLaunchState(true, action?.Command, action?.Arguments);
    }

    public async Task<LoginLaunchResult> EnableAsync(CancellationToken cancellationToken)
    {
        var exePath = environment.ExePath;
        if (string.IsNullOrEmpty(exePath))
        {
            logger.LogWarning("Could not determine the Porchlight path; cannot register the sign-in task.");
            return Failed("Porchlight could not work out where it is installed.");
        }

        var xmlPath = Path.Combine(Path.GetTempPath(), $"porchlight-task-{Guid.NewGuid():N}.xml");
        try
        {
            await File.WriteAllTextAsync(xmlPath, LoginLaunchTaskXml.Build(environment.UserId, exePath), Encoding.Unicode, cancellationToken)
                .ConfigureAwait(false);
            return await RunSchTasksAsync(["/Create", "/TN", LoginLaunchTaskXml.TaskName, "/XML", xmlPath, "/F"], cancellationToken)
                .ConfigureAwait(false);
        }
        catch (IOException ex)
        {
            logger.LogError(ex, "Could not write the sign-in task definition.");
            return Failed("Porchlight could not prepare the sign-in setting.");
        }
        finally
        {
            TryDelete(xmlPath);
        }
    }

    public async Task<LoginLaunchResult> DisableAsync(CancellationToken cancellationToken)
    {
        // Nothing to remove (and no reason to show a UAC prompt) if the task is already gone.
        if (!(await GetStateAsync(cancellationToken).ConfigureAwait(false)).Exists)
        {
            return LoginLaunchResult.Success;
        }

        return await RunSchTasksAsync(["/Delete", "/TN", LoginLaunchTaskXml.TaskName, "/F"], cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> RefreshStaleRegistrationAsync(CancellationToken cancellationToken)
    {
        var exePath = environment.ExePath;
        if (string.IsNullOrEmpty(exePath))
        {
            return false;
        }

        var state = await GetStateAsync(cancellationToken).ConfigureAwait(false);
        if (!state.Exists || SamePath(state.CommandPath, exePath))
        {
            return false;
        }

        if (!elevation.IsElevated)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(
                    "The sign-in task points at {Old} but Porchlight is running from {New}; run Porchlight as administrator or toggle the setting to fix it.",
                    state.CommandPath,
                    exePath);
            }

            return false;
        }

        var result = await EnableAsync(cancellationToken).ConfigureAwait(false);
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Re-registered the sign-in task for {Path}: {Outcome}.", exePath, result.Outcome);
        }

        return result.Succeeded;
    }

    private static bool SamePath(string? a, string b) =>
        a is not null && string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);

    private async Task<LoginLaunchResult> RunSchTasksAsync(string[] arguments, CancellationToken cancellationToken)
    {
        try
        {
            if (elevation.IsElevated)
            {
                var result = await processRunner.RunAsync(SchTasks, arguments, null, null, cancellationToken).ConfigureAwait(false);
                return FromExitCode(result.ExitCode, string.Join(" ", result.StandardErrorLines));
            }

            var elevated = await elevatedRunner.RunAsync(SchTasks, arguments, cancellationToken).ConfigureAwait(false);
            if (elevated.Declined)
            {
                logger.LogInformation("The user declined the admin prompt for the sign-in task.");
                return new LoginLaunchResult(
                    LoginLaunchOutcome.Declined,
                    "Windows didn't get permission, so nothing was changed. Try again and choose Yes when asked.");
            }

            return FromExitCode(elevated.ExitCode, string.Empty);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Running schtasks failed.");
            return Failed("Windows could not change the sign-in setting.");
        }
    }

    private static LoginLaunchResult Failed(string message) => new(LoginLaunchOutcome.Failed, message);

    private LoginLaunchResult FromExitCode(int exitCode, string detail)
    {
        if (exitCode == 0)
        {
            return LoginLaunchResult.Success;
        }

        logger.LogWarning("schtasks exited with code {ExitCode}: {Detail}", exitCode, detail);
        return Failed("Windows could not change the sign-in setting.");
    }

    private void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogDebug(ex, "Could not delete the temporary task file.");
        }
    }
}
