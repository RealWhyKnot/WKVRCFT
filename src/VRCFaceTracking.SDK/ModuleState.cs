namespace VRCFaceTracking.Core.Library;

public enum ModuleState
{
    InitFailed    = -2, // Module's InitializeAsync returned false (required device/service unavailable)
    Uninitialized = -1, // Not yet started, or being respawned
    Idle = 0,   // Idle and above we can assume the module in question is or has been in use
    Active = 1  // We're actively getting tracking data from the module
}