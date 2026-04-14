using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using VRCFaceTracking.Core.Sandboxing;
using VRCFaceTracking.Core.Sandboxing.IPC;
using VRCFaceTracking.Core.Sandboxing.V2;
using VRCFaceTracking.Core.Services;

namespace VRCFaceTracking.Core.Library;

public class UnifiedLibManager : IDisposable
{
    private readonly ILogger<UnifiedLibManager> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ModuleCapabilityManager _capabilityManager;
    private readonly SettingsService? _settings;
    private VrcftSandboxServer? _sandboxServer;

    // V1 (UDP) collections
    private readonly ConcurrentDictionary<string, ModuleRuntimeInfo> _modules = new();
    private readonly ConcurrentDictionary<int, string> _portToModuleId = new();

    // V2 (Named Pipe) collections
    private readonly ConcurrentDictionary<string, V2PipeServer> _v2Pipes = new();

    private Thread? _updateThread;
    private Timer? _heartbeatCheckTimer;
    private readonly CancellationTokenSource _cts = new();

    private const int MaxRetries = 3;
    private const int HeartbeatTimeoutSeconds = 10;

    public static readonly string ModulesDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VRCFaceTracking", "modules");

    public event Action<List<ModuleRuntimeInfo>>? OnModuleListChanged;

    public bool IsEyeTrackingActive => _capabilityManager.GetOwner(TrackingCapability.Eye) != null;
    public bool IsExpressionTrackingActive => _capabilityManager.GetOwner(TrackingCapability.Expression) != null;

    public UnifiedLibManager(ILoggerFactory loggerFactory, ModuleCapabilityManager capabilityManager, SettingsService? settings = null)
    {
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<UnifiedLibManager>();
        _capabilityManager = capabilityManager;
        _settings = settings;
    }

    public void Initialize()
    {
        Directory.CreateDirectory(ModulesDir);
        _logger.LogInformation($"Modules directory: {ModulesDir}");

        _sandboxServer = new VrcftSandboxServer(_loggerFactory, Array.Empty<int>());
        _sandboxServer.OnPacketReceived += HandlePacketFromModule;
        _logger.LogInformation($"V1 sandbox UDP server listening on port {_sandboxServer.Port}");

        var discovered = DiscoverModules().ToList();
        _logger.LogInformation($"Found {discovered.Count} module(s) to load");

        if (discovered.Count == 0)
            _logger.LogWarning("No modules found. Install modules via the Module Registry page.");

        foreach (var (dllPath, isBuiltIn, dirKey) in discovered)
        {
            if (isBuiltIn && !IsBuiltInEnabled(dirKey))
            {
                // Register disabled built-in so it appears in the UI without spawning a process
                var stableId = "builtin_" + dirKey;
                var info = new ModuleRuntimeInfo
                {
                    ModuleId   = stableId,
                    ModulePath = dllPath,
                    ModuleName = GetManifestDisplayName(dllPath),
                    Status     = ModuleState.Uninitialized,
                    IsBuiltIn  = true,
                    Enabled    = false
                };
                _modules[stableId] = info;
                _logger.LogInformation($"Built-in module '{info.ModuleName}' is disabled — not starting");
            }
            else
            {
                SpawnNewModule(dllPath, isBuiltIn ? "builtin_" + dirKey : null, isBuiltIn);
            }
        }

        _updateThread = new Thread(UpdateLoop) { IsBackground = true, Name = "LibManagerUpdateThread" };
        _updateThread.Start();

        // Begin heartbeat checking after a grace period
        _heartbeatCheckTimer = new Timer(_ => CheckHeartbeats(), null, 10000, 5000);
    }

    private IEnumerable<(string DllPath, bool IsBuiltIn, string DirKey)> DiscoverModules()
    {
        if (!Directory.Exists(ModulesDir))
        {
            _logger.LogInformation($"Modules directory does not exist yet: {ModulesDir}");
            yield break;
        }

        foreach (var dir in Directory.GetDirectories(ModulesDir))
        {
            var manifestPath = Path.Combine(dir, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                _logger.LogDebug($"Skipping {Path.GetFileName(dir)}: no manifest.json");
                continue;
            }

            // Parse manifest once: read "dll" (explicit dll name) and "builtIn" flag
            string? explicitDll = null;
            bool isBuiltIn = false;
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
                if (doc.RootElement.TryGetProperty("dll", out var dllEl))
                    explicitDll = dllEl.GetString();
                if (doc.RootElement.TryGetProperty("builtIn", out var biEl))
                    isBuiltIn = biEl.GetBoolean();
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Could not parse manifest.json in {Path.GetFileName(dir)}: {ex.Message}");
            }

            string dllPath;
            if (explicitDll != null)
            {
                dllPath = Path.Combine(dir, explicitDll);
                if (!File.Exists(dllPath))
                {
                    _logger.LogError($"manifest.json in {Path.GetFileName(dir)} specifies dll '{explicitDll}' but it was not found");
                    continue;
                }
            }
            else
            {
                // Convention: DLL whose name matches the directory name
                var dirName = Path.GetFileName(dir);
                dllPath = Path.Combine(dir, dirName + ".dll");
                if (!File.Exists(dllPath))
                {
                    // Fall back to first DLL directly in the directory
                    var dlls = Directory.GetFiles(dir, "*.dll", SearchOption.TopDirectoryOnly);
                    if (dlls.Length == 0)
                    {
                        _logger.LogWarning($"No DLL found in module directory {Path.GetFileName(dir)}");
                        continue;
                    }
                    dllPath = dlls[0];
                    _logger.LogDebug($"Module {Path.GetFileName(dir)}: using {Path.GetFileName(dllPath)} (no name match)");
                }
            }

            _logger.LogInformation($"Discovered module: {Path.GetFileName(dllPath)} in {Path.GetFileName(dir)} (builtIn={isBuiltIn})");
            yield return (dllPath, isBuiltIn, Path.GetFileName(dir));
        }
    }

    // Returns true if the DLL is a V2 module (has a manifest.json with "sdk":2 in its directory)
    private static bool IsV2Module(string dllPath)
    {
        var dir = Path.GetDirectoryName(dllPath) ?? "";
        var manifestPath = Path.Combine(dir, "manifest.json");
        if (!File.Exists(manifestPath)) return false;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
            if (doc.RootElement.TryGetProperty("sdk", out var sdkEl) && sdkEl.GetInt32() == 2)
                return true;
        }
        catch { }
        return false;
    }

    // Spawn a brand-new module (creates a new ModuleRuntimeInfo entry).
    // stableId: optional fixed ID (used for built-in modules so the ID survives enable/disable cycles).
    private void SpawnNewModule(string dllPath, string? stableId = null, bool isBuiltIn = false)
    {
        var moduleId = stableId ?? Guid.NewGuid().ToString("N");
        var info = new ModuleRuntimeInfo
        {
            ModuleId   = moduleId,
            ModulePath = dllPath,
            ModuleName = isBuiltIn
                ? GetManifestDisplayName(dllPath)
                : Path.GetFileNameWithoutExtension(dllPath),
            Status    = ModuleState.Uninitialized,
            IsBuiltIn = isBuiltIn,
            Enabled   = true
        };
        _modules[moduleId] = info;
        LaunchProcess(info);
    }

    // Relaunch an existing ModuleRuntimeInfo (used for crash recovery — keeps same moduleId)
    private void RespawnModule(ModuleRuntimeInfo module)
    {
        module.Status = ModuleState.Uninitialized;
        module.SandboxProcessPort = 0;
        module.SandboxProcessPID = 0;
        // Clean up old V2 pipe if any
        if (_v2Pipes.TryRemove(module.ModuleId, out var oldPipe))
            oldPipe.Dispose();
        LaunchProcess(module);
    }

    private void LaunchProcess(ModuleRuntimeInfo module)
    {
        bool isV2 = IsV2Module(module.ModulePath);
        string? hostExe = isV2 ? FindModuleHostV2Exe() : FindModuleHostExe();

        if (hostExe == null)
        {
            string exeName = isV2
                ? "VRCFaceTracking.ModuleHostV2.exe"
                : "VRCFaceTracking.ModuleHost.exe";
            _logger.LogError($"Cannot load {Path.GetFileName(module.ModulePath)}: {exeName} not found. " +
                             $"Searched in: {AppDomain.CurrentDomain.BaseDirectory} and sibling directories. " +
                             $"Make sure {exeName} is deployed alongside VRCFaceTracking.App.exe.");
            module.Status = ModuleState.Uninitialized;
            OnModuleListChanged?.Invoke(GetModuleList());
            return;
        }

        _logger.LogInformation($"Launching {(isV2 ? "V2" : "V1")} module: {Path.GetFileName(module.ModulePath)}");
        _logger.LogDebug($"  Host exe: {hostExe}");
        _logger.LogDebug($"  Module path: {module.ModulePath}");

        try
        {
            ProcessStartInfo psi;

            if (isV2)
            {
                // V2: Named Pipe IPC
                var pipeServer = new V2PipeServer(module.ModuleId, _loggerFactory);
                _v2Pipes[module.ModuleId] = pipeServer;

                psi = new ProcessStartInfo(hostExe)
                {
                    Arguments = $"--pipe-name {pipeServer.PipeName} --module-path \"{module.ModulePath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                _logger.LogDebug($"  Pipe name: {pipeServer.PipeName}");

                // Start the pipe server and await connection asynchronously.
                // Observe the task so unhandled exceptions surface in the log rather
                // than silently disappearing into the finaliser queue.
                var connectTask = Task.Run(async () =>
                {
                    bool connected = await pipeServer.StartAndWaitForConnectionAsync(_cts.Token);
                    if (connected)
                        await HandleV2ModuleConnectedAsync(module, pipeServer);
                }, _cts.Token);
                connectTask.ContinueWith(t =>
                {
                    if (t.IsFaulted)
                        _logger.LogError(t.Exception!,
                            $"V2 pipe task for {Path.GetFileName(module.ModulePath)} threw an unhandled exception");
                }, TaskContinuationOptions.OnlyOnFaulted);
            }
            else
            {
                // V1: UDP IPC
                int serverPort = _sandboxServer!.Port;
                psi = new ProcessStartInfo(hostExe)
                {
                    Arguments = $"--port {serverPort} --module-path \"{module.ModulePath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                _logger.LogDebug($"  UDP server port: {serverPort}");
            }

            var process = Process.Start(psi);
            if (process == null)
            {
                _logger.LogError($"Process.Start returned null for {Path.GetFileName(module.ModulePath)}");
                _modules.TryRemove(module.ModuleId, out _);
                return;
            }

            module.Process    = process;
            module.SandboxProcessPID = process.Id;
            module.LaunchTime = DateTime.UtcNow;
            _logger.LogInformation($"Module host PID {process.Id} started for {Path.GetFileName(module.ModulePath)}");

            // Capture stdout/stderr from the module host.
            // stdout → Info (module load errors, init messages — visible in all builds)
            // stderr → Warning (critical failures — always visible)
            process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    _logger.LogInformation($"[ModuleHost {process.Id}] {e.Data}");
                    module.LastMessage = e.Data;
                    OnModuleListChanged?.Invoke(GetModuleList());
                }
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    _logger.LogWarning($"[ModuleHost {process.Id}] {e.Data}");
                    module.LastMessage = e.Data;
                    OnModuleListChanged?.Invoke(GetModuleList());
                }
            };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Watch for unexpected exits
            process.EnableRaisingEvents = true;
            process.Exited += (_, _) =>
            {
                int exitCode = -1;
                try { exitCode = process.ExitCode; } catch { }

                if (_cts.IsCancellationRequested) return; // Intentional shutdown

                // Distinguish startup failures (exit within 5 s of launch) from runtime crashes
                bool isStartupFailure = module.LaunchTime != DateTime.MinValue
                    && (DateTime.UtcNow - module.LaunchTime) < TimeSpan.FromSeconds(5)
                    && exitCode != 0;

                if (isStartupFailure)
                    _logger.LogError(
                        $"STARTUP FAILURE: {Path.GetFileName(module.ModulePath)} exited {(DateTime.UtcNow - module.LaunchTime).TotalMilliseconds:F0}ms after launch (exit code {exitCode}). " +
                        $"Check native dependencies, crash log, and that all required services are running.");
                else
                    _logger.LogWarning($"Module host PID {process.Id} ({Path.GetFileName(module.ModulePath)}) exited with code {exitCode}");

                if (exitCode == 2)
                    _logger.LogError($"  [{Path.GetFileName(module.ModulePath)}] Failed to load DLL — check native dependencies (device software, Visual C++ runtimes, etc.)");
                else if (exitCode == 3)
                    _logger.LogError($"  [{Path.GetFileName(module.ModulePath)}] Initialization timed out — module did not respond within 60 seconds.");
                else if (exitCode != 0 && !isStartupFailure)
                    _logger.LogError($"  [{Path.GetFileName(module.ModulePath)}] Module host exited abnormally (code {exitCode}).");

                HandleModuleCrash(module);
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to spawn module host for {Path.GetFileName(module.ModulePath)}: {ex.Message}\n{ex}");
            _modules.TryRemove(module.ModuleId, out _);
        }
    }

    private async Task HandleV2ModuleConnectedAsync(ModuleRuntimeInfo module, V2PipeServer pipe)
    {
        try
        {
            _logger.LogInformation($"V2 module {Path.GetFileName(module.ModulePath)} connected to pipe, sending handshake");

            // Wire message handler before sending handshake
            pipe.OnMessageReceived += msg => HandleV2Message(module, msg);
            pipe.OnDisconnected += () =>
            {
                _logger.LogWarning($"V2 module {module.ModuleName} pipe disconnected");
                HandleModuleCrash(module);
            };

            // Handshake
            await pipe.SendHandshakeAsync(_cts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError($"V2 connect handler error for {Path.GetFileName(module.ModulePath)}: {ex.Message}\n{ex}");
        }
    }

    private void HandleV2Message(ModuleRuntimeInfo module, V2Message msg)
    {
        switch (msg.Type)
        {
            case V2MessageType.HandshakeAck:
            {
                var ack = msg.DeserializePayload<V2HandshakeAckPayload>();
                if (ack == null) break;

                module.ModuleName = ack.Name;
                module.SupportsEyeTracking = (ack.Capabilities & 1) != 0;
                module.SupportsExpressionTracking = (ack.Capabilities & 2) != 0;
                bool supportsHead = (ack.Capabilities & 4) != 0;

                _logger.LogInformation($"V2 module ack: {ack.Name} eye={module.SupportsEyeTracking} expr={module.SupportsExpressionTracking} head={supportsHead}");

                if (module.SupportsEyeTracking)
                    _capabilityManager.TryClaim(module.ModuleId, TrackingCapability.Eye);
                if (module.SupportsExpressionTracking)
                    _capabilityManager.TryClaim(module.ModuleId, TrackingCapability.Expression);
                if (supportsHead)
                    _capabilityManager.TryClaim(module.ModuleId, TrackingCapability.Head);

                // Send Init
                if (_v2Pipes.TryGetValue(module.ModuleId, out var pipe))
                    _ = pipe.SendInitAsync(module.SupportsEyeTracking, module.SupportsExpressionTracking, _cts.Token);
                break;
            }

            case V2MessageType.InitResult:
            {
                var result = msg.DeserializePayload<V2InitResultPayload>();
                bool success = result?.Success ?? false;

                // InitFailed means the module explicitly declined to start (device not connected,
                // service not running, etc.). The crash handler will see this and skip auto-restart.
                module.Status = success ? ModuleState.Active : ModuleState.InitFailed;
                module.Active = success;
                module.RetryCount = 0;

                _logger.LogInformation($"V2 module {module.ModuleName} init: success={success}");
                OnModuleListChanged?.Invoke(GetModuleList());
                break;
            }

            case V2MessageType.TrackingData:
            {
                var data = msg.DeserializePayload<V2TrackingDataPayload>();
                if (data == null) break;

                // Only merge data for capabilities this module owns
                bool ownsEye = _capabilityManager.IsOwner(module.ModuleId, TrackingCapability.Eye);
                bool ownsExpr = _capabilityManager.IsOwner(module.ModuleId, TrackingCapability.Expression);
                bool ownsHead = _capabilityManager.IsOwner(module.ModuleId, TrackingCapability.Head);

                if (ownsEye || ownsExpr || ownsHead)
                {
                    // Selectively apply based on ownership
                    var filtered = data with
                    {
                        EyeLeft = ownsEye ? data.EyeLeft : null,
                        EyeRight = ownsEye ? data.EyeRight : null,
                        HeadRot = ownsHead ? data.HeadRot : null,
                        HeadPos = ownsHead ? data.HeadPos : null,
                        Shapes = ownsExpr ? data.Shapes : null
                    };
                    V2PipeServer.ApplyTrackingData(filtered);
                }
                break;
            }

            case V2MessageType.Log:
            {
                var logData = msg.DeserializePayload<V2LogPayload>();
                if (logData == null) break;
                var level = (Microsoft.Extensions.Logging.LogLevel)logData.Level;
                LogService.AddEntry(new LogEntry
                {
                    Timestamp = DateTime.UtcNow,
                    Level = level.ToString(),
                    Source = module.ModuleName,
                    Message = logData.Message
                });
                break;
            }

            case V2MessageType.ConfigSchema:
                // Raw JSON sent by module.RegisterConfigSchema(schema); stored for the UI
                module.ConfigSchemaJson = msg.Payload;
                _logger.LogInformation($"V2 module {module.ModuleName} registered config schema");
                OnModuleListChanged?.Invoke(GetModuleList());
                break;

            case V2MessageType.ShutdownAck:
                module.Status = ModuleState.Uninitialized;
                module.Active = false;
                _capabilityManager.Release(module.ModuleId);
                _logger.LogInformation($"V2 module {module.ModuleName} shut down cleanly");
                break;
        }
    }

    private string? FindModuleHostExe() => FindHostExe("VRCFaceTracking.ModuleHost.exe");
    private string? FindModuleHostV2Exe() => FindHostExe("VRCFaceTracking.ModuleHostV2.exe");

    private string? FindHostExe(string exeName)
    {
        // Check beside the app exe first (standard deployed layout)
        string appDir = Path.GetDirectoryName(Environment.ProcessPath)
                        ?? AppDomain.CurrentDomain.BaseDirectory;
        var candidate = Path.Combine(appDir, exeName);
        _logger.LogDebug($"Looking for {exeName} at: {candidate}");
        if (File.Exists(candidate)) return candidate;

        // Check sibling directory (dev layout: bin/Debug/net10.0-windows/VRCFaceTracking.ModuleHost/...)
        string siblingDir = Path.GetFileNameWithoutExtension(exeName);
        candidate = Path.GetFullPath(Path.Combine(appDir, "..", siblingDir, exeName));
        _logger.LogDebug($"Looking for {exeName} at: {candidate}");
        if (File.Exists(candidate)) return candidate;

        // Also check AppDomain base (handles non-single-file debug runs)
        candidate = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, exeName);
        _logger.LogDebug($"Looking for {exeName} at: {candidate}");
        if (File.Exists(candidate)) return candidate;

        _logger.LogError($"{exeName} not found in any search path");
        return null;
    }

    private void HandlePacketFromModule(in IpcPacket packet, in int clientPort)
    {
        switch (packet.GetPacketType())
        {
            case IpcPacket.PacketType.Handshake:
            {
                var hp = (HandshakePacket)packet;

                // Match by path + PID (most specific, handles restarts correctly)
                var module = _modules.Values.FirstOrDefault(m =>
                    string.Equals(m.ModulePath, hp.ModulePath, StringComparison.OrdinalIgnoreCase)
                    && m.SandboxProcessPID == hp.PID);

                // Fallback: match by path with no port yet assigned (handles race condition
                // where PID was not yet stored before the handshake arrived)
                module ??= _modules.Values.FirstOrDefault(m =>
                    string.Equals(m.ModulePath, hp.ModulePath, StringComparison.OrdinalIgnoreCase)
                    && m.SandboxProcessPort == 0);

                if (module == null)
                {
                    _logger.LogWarning($"Handshake from unknown module: {hp.ModulePath}");
                    break;
                }

                module.SandboxProcessPort = clientPort;
                module.SandboxProcessPID = hp.PID; // Ensure PID is correct
                module.LastHeartbeat = DateTime.UtcNow;
                _portToModuleId[clientPort] = module.ModuleId;

                _logger.LogInformation($"Module {Path.GetFileName(module.ModulePath)} connected (client port {clientPort})");

                // Ask what capabilities the module supports
                var getSupportedPkt = new EventInitGetSupported();
                _sandboxServer!.SendData(getSupportedPkt, clientPort);
                break;
            }

            case IpcPacket.PacketType.ReplyGetSupported:
            {
                if (!_portToModuleId.TryGetValue(clientPort, out var moduleId)) break;
                if (!_modules.TryGetValue(moduleId, out var module)) break;

                var reply = (ReplySupportedPacket)packet;
                module.SupportsEyeTracking = reply.eyeAvailable;
                module.SupportsExpressionTracking = reply.expressionAvailable;

                _logger.LogInformation(
                    $"{Path.GetFileName(module.ModulePath)}: eye={reply.eyeAvailable}, expr={reply.expressionAvailable}");

                // Attempt to claim capabilities; later modules won't steal from earlier ones
                if (reply.eyeAvailable)
                    _capabilityManager.TryClaim(moduleId, TrackingCapability.Eye);
                if (reply.expressionAvailable)
                    _capabilityManager.TryClaim(moduleId, TrackingCapability.Expression);

                var initPkt = new EventInitPacket
                {
                    eyeAvailable = reply.eyeAvailable,
                    expressionAvailable = reply.expressionAvailable
                };
                _sandboxServer!.SendData(initPkt, clientPort);
                break;
            }

            case IpcPacket.PacketType.ReplyInit:
            {
                if (!_portToModuleId.TryGetValue(clientPort, out var moduleId)) break;
                if (!_modules.TryGetValue(moduleId, out var module)) break;

                var reply = (ReplyInitPacket)packet;
                module.ModuleName = reply.ModuleInformationName;
                module.IconData = reply.IconDataStreams;
                module.Status = (reply.eyeSuccess || reply.expressionSuccess)
                    ? ModuleState.Active
                    : ModuleState.Idle;
                module.Active = reply.eyeSuccess || reply.expressionSuccess;
                module.RetryCount = 0; // Reset on successful init

                _logger.LogInformation($"Module initialized: {module.ModuleName}");
                OnModuleListChanged?.Invoke(GetModuleList());
                break;
            }

            case IpcPacket.PacketType.ReplyUpdate:
            {
                if (!_portToModuleId.TryGetValue(clientPort, out var moduleId)) break;
                if (!_modules.TryGetValue(moduleId, out var module)) break;

                var reply = (ReplyUpdatePacket)packet;

                // Only merge data for capabilities this module actually owns
                if (_capabilityManager.IsOwner(moduleId, TrackingCapability.Eye))
                    reply.UpdateGlobalEyeState();
                if (_capabilityManager.IsOwner(moduleId, TrackingCapability.Expression))
                    reply.UpdateGlobalExpressionState();
                if (_capabilityManager.IsOwner(moduleId, TrackingCapability.Head))
                    reply.UpdateHeadState();
                break;
            }

            case IpcPacket.PacketType.ReplyTeardown:
            {
                if (!_portToModuleId.TryGetValue(clientPort, out var moduleId)) break;
                if (!_modules.TryGetValue(moduleId, out var module)) break;

                module.Status = ModuleState.Uninitialized;
                module.Active = false;
                _capabilityManager.Release(moduleId);
                _portToModuleId.TryRemove(clientPort, out _);
                _logger.LogInformation($"Module {Path.GetFileName(module.ModulePath)} torn down cleanly");
                break;
            }

            case IpcPacket.PacketType.EventLog:
            {
                var logPkt = (EventLogPacket)packet;
                string source = "Module";
                if (_portToModuleId.TryGetValue(clientPort, out var modId) &&
                    _modules.TryGetValue(modId, out var logModule))
                {
                    source = Path.GetFileNameWithoutExtension(logModule.ModulePath);
                }
                LogService.AddEntry(new LogEntry
                {
                    Timestamp = DateTime.UtcNow,
                    Level = logPkt.LogLevel.ToString(),
                    Source = source,
                    Message = logPkt.Message
                });
                break;
            }
        }
    }

    // ~100Hz update loop: poll each active module for its latest tracking data
    private void UpdateLoop()
    {
        while (!_cts.IsCancellationRequested)
        {
            foreach (var module in _modules.Values)
            {
                if (module.SandboxProcessPort <= 0) continue;
                if (module.Status != ModuleState.Active && module.Status != ModuleState.Idle) continue;

                try
                {
                    var pkt = new EventUpdatePacket();
                    _sandboxServer?.SendData(pkt, module.SandboxProcessPort);
                }
                catch { /* module may have just crashed */ }
            }
            Thread.Sleep(10);
        }
    }

    private void CheckHeartbeats()
    {
        if (_sandboxServer == null) return;

        foreach (var module in _modules.Values.ToList())
        {
            if (module.SandboxProcessPID <= 0) continue;
            if (module.Status == ModuleState.Uninitialized) continue;

            if (_sandboxServer.IsModuleTimedOut(module.SandboxProcessPID,
                    TimeSpan.FromSeconds(HeartbeatTimeoutSeconds)))
            {
                _logger.LogWarning(
                    $"Module {Path.GetFileName(module.ModulePath)} heartbeat timeout (PID {module.SandboxProcessPID})");
                HandleModuleCrash(module);
            }
        }
    }

    private void HandleModuleCrash(ModuleRuntimeInfo module)
    {
        // Double-event guard: the process exit can fire both the Exited event and the
        // pipe OnDisconnected handler concurrently.  Only the first caller proceeds.
        if (Interlocked.CompareExchange(ref module.CrashHandling, 1, 0) != 0)
            return;

        // Capture status before we reset it — used below to decide whether to restart.
        var statusBeforeCrash = module.Status;

        _capabilityManager.Release(module.ModuleId);

        if (module.SandboxProcessPort > 0)
        {
            _portToModuleId.TryRemove(module.SandboxProcessPort, out _);
            module.SandboxProcessPort = 0;
        }

        try { module.Process?.Kill(); } catch { }
        module.Process?.Dispose();
        module.Process = null;
        module.SandboxProcessPID = 0;
        module.Status = ModuleState.Uninitialized;
        module.Active = false;

        // Intentionally disabled built-in modules shouldn't have their crash count incremented
        bool intentionalDisable = module.IsBuiltIn && !module.Enabled;
        if (!intentionalDisable)
            module.CrashCount++;

        OnModuleListChanged?.Invoke(GetModuleList());

        // Don't restart: built-in module was explicitly disabled
        if (intentionalDisable)
        {
            _logger.LogInformation($"Built-in module '{module.ModuleName}' was disabled — not restarting");
            Interlocked.Exchange(ref module.CrashHandling, 0);
            return;
        }

        // Don't restart: InitializeAsync returned false (device/service unavailable).
        // Auto-retrying would just spam failures. The user can re-enable to retry.
        if (statusBeforeCrash == ModuleState.InitFailed)
        {
            _logger.LogWarning($"Module {module.ModuleName} init returned false — not restarting automatically");
            OnModuleListChanged?.Invoke(GetModuleList());
            Interlocked.Exchange(ref module.CrashHandling, 0);
            return;
        }

        // Don't restart: max retries exceeded
        if (module.RetryCount >= MaxRetries)
        {
            _logger.LogError(
                $"Module {Path.GetFileName(module.ModulePath)} exceeded max retries ({MaxRetries}), disabled");
            Interlocked.Exchange(ref module.CrashHandling, 0);
            return;
        }

        module.RetryCount++;
        int delayMs = Math.Min((int)Math.Pow(2, module.RetryCount) * 1000, 30_000); // 2s, 4s, 8s … cap at 30s
        _logger.LogInformation(
            $"Restarting {Path.GetFileName(module.ModulePath)} in {delayMs}ms (attempt {module.RetryCount}/{MaxRetries})");

        Task.Delay(delayMs, _cts.Token)
            .ContinueWith(_ =>
            {
                if (!_cts.IsCancellationRequested)
                {
                    // Reset the atomic guard before respawn so the next crash can be handled
                    Interlocked.Exchange(ref module.CrashHandling, 0);
                    RespawnModule(module);
                }
            }, TaskContinuationOptions.OnlyOnRanToCompletion);
    }

    // --- Built-in module helpers ---

    private bool IsBuiltInEnabled(string dirKey)
    {
        if (_settings == null) return true; // No settings → always enable
        if (_settings.Modules.EnabledModules.TryGetValue(dirKey, out bool enabled))
            return enabled;
        return false; // Default: built-ins start disabled until user enables them
    }

    private static string GetManifestDisplayName(string dllPath)
    {
        var dir = Path.GetDirectoryName(dllPath) ?? "";
        var manifestPath = Path.Combine(dir, "manifest.json");
        if (!File.Exists(manifestPath)) return Path.GetFileNameWithoutExtension(dllPath);
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
            if (doc.RootElement.TryGetProperty("name", out var nameEl))
                return nameEl.GetString() ?? Path.GetFileNameWithoutExtension(dllPath);
        }
        catch { }
        return Path.GetFileNameWithoutExtension(dllPath);
    }

    public void EnableModule(string moduleId)
    {
        if (!_modules.TryGetValue(moduleId, out var module))
        {
            _logger.LogWarning($"EnableModule: module '{moduleId}' not found");
            return;
        }
        if (!module.IsBuiltIn)
        {
            _logger.LogWarning($"EnableModule: '{module.ModuleName}' is not a built-in module");
            return;
        }

        string dirKey = Path.GetFileName(Path.GetDirectoryName(module.ModulePath) ?? "");
        if (_settings != null)
        {
            _settings.Modules.EnabledModules[dirKey] = true;
            _settings.SaveModules();
        }

        module.Enabled = true;
        module.RetryCount = 0;
        module.CrashCount = 0;
        module.LastMessage = "";
        _logger.LogInformation($"Enabling built-in module: {module.ModuleName}");
        RespawnModule(module);
    }

    public void DisableModule(string moduleId)
    {
        if (!_modules.TryGetValue(moduleId, out var module))
        {
            _logger.LogWarning($"DisableModule: module '{moduleId}' not found");
            return;
        }
        if (!module.IsBuiltIn)
        {
            _logger.LogWarning($"DisableModule: '{module.ModuleName}' is not a built-in module");
            return;
        }

        string dirKey = Path.GetFileName(Path.GetDirectoryName(module.ModulePath) ?? "");
        if (_settings != null)
        {
            _settings.Modules.EnabledModules[dirKey] = false;
            _settings.SaveModules();
        }

        // Set Enabled = false BEFORE killing so HandleModuleCrash (fired by Exited event) skips restart
        module.Enabled = false;

        _capabilityManager.Release(module.ModuleId);
        if (module.SandboxProcessPort > 0)
        {
            _portToModuleId.TryRemove(module.SandboxProcessPort, out _);
            module.SandboxProcessPort = 0;
        }
        if (_v2Pipes.TryRemove(module.ModuleId, out var pipe))
            pipe.Dispose();

        try { module.Process?.Kill(); } catch { }
        module.Process?.Dispose();
        module.Process = null;
        module.SandboxProcessPID = 0;
        module.Status = ModuleState.Uninitialized;
        module.Active = false;
        module.LastMessage = "";

        _logger.LogInformation($"Disabled built-in module: {module.ModuleName}");
        OnModuleListChanged?.Invoke(GetModuleList());
    }

    public void RestartModule(string moduleId)
    {
        if (!_modules.TryGetValue(moduleId, out var module))
        {
            _logger.LogWarning($"RestartModule: module {moduleId} not found");
            return;
        }

        _logger.LogInformation($"Restarting module {module.ModuleName} by user request");
        _capabilityManager.Release(module.ModuleId);

        if (module.SandboxProcessPort > 0)
        {
            _portToModuleId.TryRemove(module.SandboxProcessPort, out _);
            module.SandboxProcessPort = 0;
        }

        try { module.Process?.Kill(); } catch { }
        module.Process?.Dispose();
        module.Process = null;
        module.SandboxProcessPID = 0;
        module.Status = ModuleState.Uninitialized;
        module.Active = false;
        module.RetryCount = 0;
        module.CrashCount = 0;
        module.LastMessage = "";
        OnModuleListChanged?.Invoke(GetModuleList());

        Task.Delay(500, _cts.Token).ContinueWith(_ =>
        {
            if (!_cts.IsCancellationRequested)
                RespawnModule(module);
        }, TaskContinuationOptions.OnlyOnRanToCompletion);
    }

    public List<ModuleRuntimeInfo> GetModuleList() => _modules.Values.ToList();

    public void Shutdown()
    {
        _cts.Cancel();
        _heartbeatCheckTimer?.Dispose();

        // Request graceful teardown from all V2 modules
        foreach (var (id, pipe) in _v2Pipes)
        {
            try { _ = pipe.SendShutdownAsync(CancellationToken.None); } catch { }
        }

        // Request graceful teardown from all V1 modules
        foreach (var module in _modules.Values)
        {
            if (module.SandboxProcessPort <= 0) continue;
            if (module.Status == ModuleState.Uninitialized) continue;
            try
            {
                var pkt = new EventTeardownPacket();
                _sandboxServer?.SendData(pkt, module.SandboxProcessPort);
            }
            catch { }
        }

        Thread.Sleep(300); // Brief window for graceful exits

        // Kill any processes that didn't exit
        foreach (var module in _modules.Values)
        {
            try { module.Process?.Kill(); } catch { }
            module.Process?.Dispose();
            module.Process = null;
        }

        // Clean up V2 pipe servers
        foreach (var pipe in _v2Pipes.Values)
            pipe.Dispose();
        _v2Pipes.Clear();

        _sandboxServer?.Dispose();
    }

    public void Dispose() => Shutdown();
}
