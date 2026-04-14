using System.Diagnostics;

namespace VRCFaceTracking.Core.Library;

public class ModuleRuntimeInfo
{
    public string ModuleId { get; set; } = "";
    public string ModulePath { get; set; } = "";
    public string ModuleName { get; set; } = "";
    public int SandboxProcessPID { get; set; }
    public int SandboxProcessPort { get; set; }
    public Process? Process { get; set; }
    public ModuleState Status { get; set; } = ModuleState.Uninitialized;
    public bool SupportsEyeTracking { get; set; }
    public bool SupportsExpressionTracking { get; set; }
    public bool Active { get; set; }
    public int CrashCount { get; set; }
    public int RetryCount { get; set; }
    public string LastMessage { get; set; } = "";
    public bool IsBuiltIn { get; set; }
    public bool Enabled { get; set; } = true;
    // V2 modules register a ConfigSchema during InitializeAsync; stored here as raw JSON
    public string? ConfigSchemaJson { get; set; }
    public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;
    public List<Stream> IconData { get; set; } = new();
    public CancellationTokenSource? UpdateCancellationToken { get; set; }
    public Thread? UpdateThread { get; set; }

    /// <summary>
    /// UTC time when <see cref="LaunchProcess"/> last started this module's host process.
    /// Used to distinguish startup failures (exit within a few seconds) from runtime crashes.
    /// </summary>
    public DateTime LaunchTime { get; set; } = DateTime.MinValue;

    /// <summary>
    /// Atomic flag used by <c>HandleModuleCrash</c> to prevent the process-exit
    /// event and the pipe OnDisconnected event from both processing a crash simultaneously.
    /// 0 = idle, 1 = handling in progress.  Use <see cref="System.Threading.Interlocked"/>.
    /// </summary>
    public int CrashHandling;
}
