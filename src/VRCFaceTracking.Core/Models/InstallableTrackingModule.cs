namespace VRCFaceTracking.Core.Models;

public enum InstallState
{
    NotInstalled,
    Installing,
    Installed,
    UpdateAvailable,
    Error
}

public class InstallableTrackingModule
{
    public TrackingModuleMetadata Metadata { get; set; } = new();
    public InstallState InstallState { get; set; } = InstallState.NotInstalled;
    public string? InstalledVersion { get; set; }
    public string? InstallPath { get; set; }
    public string? ErrorMessage { get; set; }
    public float InstallProgress { get; set; }

    public bool IsInstalled => InstallState is InstallState.Installed or InstallState.UpdateAvailable;

    public bool HasUpdate => InstallState == InstallState.UpdateAvailable;
}
