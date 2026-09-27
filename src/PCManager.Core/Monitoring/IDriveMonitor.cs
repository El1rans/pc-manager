namespace PCManager.Core.Monitoring;

/// <summary>Reports fixed and ready removable drives. See <see cref="DriveSnapshot"/>.</summary>
public interface IDriveMonitor
{
    IReadOnlyList<DriveSnapshot> GetDrives();
}
