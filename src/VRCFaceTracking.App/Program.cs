using System;
using System.IO;
using System.Diagnostics;
using System.Text.Json;
using System.Runtime.Versioning;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Photino.NET;
using VRCFaceTracking.Core.Library;
using VRCFaceTracking.Core.Models;
using VRCFaceTracking.Core.Params.Data;
using VRCFaceTracking.Core.Services;
using VRCFaceTracking.SDKv2.Expressions;

namespace VRCFaceTracking.App;

[SupportedOSPlatform("windows")]
class Program
{
    private static PhotinoWindow? _window;
    private static SettingsService? _settings;
    private static ILoggerFactory? _loggerFactory;
    private static UnifiedLibManager? _libManager;
    private static ModuleRegistryService? _registryService;
    private static ModuleInstaller? _installer;
    private static OscSendService? _oscSendService;
    private static TrackingDataBroadcaster? _broadcaster;
    private static CancellationTokenSource _appCts = new();
    private static bool _isWindowReady = false;

    // Calibration state
    private static bool _calActive = false;
    private static readonly object _calLock = new();
    private static float[]? _calMin;
    private static float[]? _calMax;
    private static System.Threading.Timer? _calTimer;

    // Outgoing messages to frontend: serialize C# PascalCase props as camelCase
    private static readonly JsonSerializerOptions _jsonSendOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    // Incoming messages from frontend: accept either case
    private static readonly JsonSerializerOptions _jsonReadOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly string AppDataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VRCFaceTracking");

    // Resolved once in RunApp so helper methods don't need to re-derive it
    private static string _appBaseDir = "";

    [STAThread]
    static void Main(string[] args)
    {
        // For single-file self-contained publish, BaseDirectory is the temp extraction folder.
        // ProcessPath gives the real exe location so we can find wwwroot next to it.
        string baseDir = Path.GetDirectoryName(Environment.ProcessPath)
                         ?? AppDomain.CurrentDomain.BaseDirectory;

        try
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                string crashLog = Path.Combine(baseDir, "crash.log");
                File.WriteAllText(crashLog, "FATAL: " + e.ExceptionObject.ToString());
            };

            RunApp(baseDir);
        }
        catch (Exception ex)
        {
            string crashLog = Path.Combine(baseDir, "startup_crash.log");
            File.WriteAllText(crashLog, "STARTUP ERROR: " + ex.ToString());
            MessageBox.Show(
                "Fatal error during startup.\n\nSee startup_crash.log for details.",
                "VRCFaceTracking",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    static void RunApp(string baseDir)
    {
        _appBaseDir = baseDir;
        Directory.CreateDirectory(AppDataDir);

        LogService.Initialize(Path.Combine(AppDataDir, "logs"));
        _settings = new SettingsService(AppDataDir);

        // Logger factory — routes through VrcftLoggerProvider → LogService
        _loggerFactory = LoggerFactory.Create(b => b.AddProvider(new VrcftLoggerProvider()));

        LogService.AddEntry(new LogEntry
        {
            Timestamp = DateTime.UtcNow,
            Level = "Information",
            Source = "App",
            Message = "VRCFaceTracking starting..."
        });

        string webViewDataPath = Path.Combine(baseDir, "WebView2_Data");
        if (!Directory.Exists(webViewDataPath)) Directory.CreateDirectory(webViewDataPath);
        Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", webViewDataPath);

        _window = new PhotinoWindow()
            .SetTitle("VRCFaceTracking")
            .SetUseOsDefaultSize(false)
            .SetSize(1400, 900)
            .Center()
            .SetResizable(true)
            .RegisterWebMessageReceivedHandler((s, m) => HandleWebMessage(m))
            .SetLogVerbosity(0);

        // Forward new log entries to the frontend (goes through SendMessage → camelCase)
        LogService.OnLog += (entry) => SendMessage("LOG", entry);

        _window.RegisterWindowCreatedHandler((s, a) =>
        {
            Task.Run(async () =>
            {
                try
                {
                    await InitializeCoreServicesAsync();
                }
                catch (Exception ex)
                {
                    LogService.AddEntry(new LogEntry
                    {
                        Timestamp = DateTime.UtcNow,
                        Level = "Error",
                        Source = "App",
                        Message = "Core initialization failed: " + ex.Message
                    });
                }
            });
        });

        string indexPath = Path.Combine(baseDir, "wwwroot", "index.html");
        if (!File.Exists(indexPath))
            indexPath = Path.GetFullPath(Path.Combine(baseDir, "../../../../ui/dist/index.html"));

        if (File.Exists(indexPath))
            _window.Load(indexPath);
        else
            _window.LoadRawString(
                "<html><body style='background:#1a1a2e;color:#eee;font-family:sans-serif;" +
                "display:flex;align-items:center;justify-content:center;height:100vh'>" +
                "<div><h1>VRCFaceTracking</h1>" +
                "<p>UI build not found. Run <code>npm run build</code> in the ui/ directory.</p>" +
                "</div></body></html>");

        _window.WaitForClose();
        OnShutdown();
    }

    static async Task InitializeCoreServicesAsync()
    {
        var osc = _settings!.OscTarget;
        _oscSendService = new OscSendService(_loggerFactory!, osc.Ip, osc.SendPort);

        // Mutator — Load() auto-discovers all TrackingMutation subclasses via reflection
        var mutator = new UnifiedTrackingMutator(_loggerFactory!);
        mutator.Load();

        ParameterSenderService.Initialize(_oscSendService);

        var capabilityManager = new ModuleCapabilityManager(_loggerFactory!);
        _libManager = new UnifiedLibManager(_loggerFactory!, capabilityManager, _settings);

        _libManager.OnModuleListChanged += modules =>
        {
            SendMessage("MODULE_LIST", modules.Select(MapModuleInfo).ToList());
        };

        // Copy built-in modules from the app's builtin-modules/ folder to the user's modules dir
        CopyBuiltInModules(_appBaseDir);

        _libManager.Initialize();

        _registryService = new ModuleRegistryService(_loggerFactory!, _settings);
        _installer = new ModuleInstaller(_loggerFactory!);

        // Broadcaster for live tracking data at 30fps — started once the frontend sends its first message
        _broadcaster = new TrackingDataBroadcaster(_window!);

        // Start the OSC parameter send loop (100Hz)
        _ = ParameterSenderService.SendLoop(_appCts.Token);

        LogService.AddEntry(new LogEntry
        {
            Timestamp = DateTime.UtcNow,
            Level = "Information",
            Source = "App",
            Message = "Core services initialized"
        });
    }

    static void OnShutdown()
    {
        _appCts.Cancel();
        _broadcaster?.Dispose();
        _libManager?.Shutdown();
        _oscSendService?.Dispose();
        _settings?.Save();
        LogService.Shutdown();
    }

    static void HandleWebMessage(string message)
    {
        if (!_isWindowReady)
        {
            _isWindowReady = true;
            _broadcaster?.Start();
        }
        try
        {
            using var doc = JsonDocument.Parse(message);
            var root = doc.RootElement;
            string type = root.GetProperty("type").GetString() ?? "";

            switch (type)
            {
                case "EXIT":
                    _window?.Close();
                    break;

                case "GET_CONFIG":
                    SendMessage("CONFIG", _settings?.AppConfig);
                    break;

                case "SAVE_CONFIG":
                    if (root.TryGetProperty("data", out var configData) && _settings != null)
                    {
                        var newConfig = JsonSerializer.Deserialize<AppConfig>(configData.GetRawText(), _jsonReadOpts);
                        if (newConfig != null)
                        {
                            _settings.AppConfig.DebugMode = newConfig.DebugMode;
                            _settings.AppConfig.Theme = newConfig.Theme;
                            _settings.SaveAppConfig();
                        }
                    }
                    break;

                case "GET_OSC_CONFIG":
                    SendMessage("OSC_CONFIG", _settings?.OscTarget);
                    break;

                case "SAVE_OSC_CONFIG":
                    if (root.TryGetProperty("data", out var oscData) && _settings != null)
                    {
                        var newOsc = JsonSerializer.Deserialize<OscTargetConfig>(oscData.GetRawText(), _jsonReadOpts);
                        if (newOsc != null)
                        {
                            _settings.OscTarget.Ip = newOsc.Ip;
                            _settings.OscTarget.SendPort = newOsc.SendPort;
                            _settings.OscTarget.RecvPort = newOsc.RecvPort;
                            _settings.SaveOscTarget();
                            _oscSendService?.UpdateTarget(newOsc.Ip, newOsc.SendPort);
                        }
                    }
                    break;

                case "SYNC_LOGS":
                    foreach (var entry in LogService.GetHistory())
                        SendMessage("LOG", entry);
                    break;

                case "OPEN_BROWSER":
                    if (root.TryGetProperty("data", out var browserData))
                    {
                        string url = browserData.GetProperty("url").GetString() ?? "";
                        if (!string.IsNullOrEmpty(url))
                            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                    }
                    break;

                case "OPEN_MODULES_DIR":
                    Process.Start(new ProcessStartInfo { FileName = UnifiedLibManager.ModulesDir, UseShellExecute = true });
                    break;

                case "OPEN_LOGS_DIR":
                    Process.Start(new ProcessStartInfo { FileName = Path.Combine(AppDataDir, "logs"), UseShellExecute = true });
                    break;

                // --- Module management ---

                case "GET_MODULES":
                    var modules = _libManager?.GetModuleList() ?? new List<ModuleRuntimeInfo>();
                    SendMessage("MODULE_LIST", modules.Select(MapModuleInfo).ToList());
                    break;

                case "GET_REGISTRY":
                    _ = HandleGetRegistryAsync();
                    break;

                case "INSTALL_MODULE":
                    if (root.TryGetProperty("data", out var installData))
                    {
                        string packageId = installData.GetProperty("packageId").GetString() ?? "";
                        _ = HandleInstallModuleAsync(packageId);
                    }
                    break;

                case "UNINSTALL_MODULE":
                    if (root.TryGetProperty("data", out var uninstallData))
                    {
                        string packageId = uninstallData.GetProperty("packageId").GetString() ?? "";
                        HandleUninstallModule(packageId);
                    }
                    break;

                case "RESTART_MODULE":
                    if (root.TryGetProperty("data", out var restartData))
                    {
                        string moduleId = restartData.GetProperty("moduleId").GetString() ?? "";
                        _libManager?.RestartModule(moduleId);
                    }
                    break;

                case "ENABLE_MODULE":
                    if (root.TryGetProperty("data", out var enableData))
                    {
                        string moduleId = enableData.GetProperty("moduleId").GetString() ?? "";
                        _libManager?.EnableModule(moduleId);
                    }
                    break;

                case "DISABLE_MODULE":
                    if (root.TryGetProperty("data", out var disableData))
                    {
                        string moduleId = disableData.GetProperty("moduleId").GetString() ?? "";
                        _libManager?.DisableModule(moduleId);
                    }
                    break;

                case "GET_MODULE_CONFIG":
                    if (root.TryGetProperty("data", out var gmcData))
                    {
                        string moduleId = gmcData.GetProperty("moduleId").GetString() ?? "";
                        HandleGetModuleConfig(moduleId);
                    }
                    break;

                case "SAVE_MODULE_CONFIG":
                    if (root.TryGetProperty("data", out var smcData))
                    {
                        string moduleId = smcData.GetProperty("moduleId").GetString() ?? "";
                        if (smcData.TryGetProperty("values", out var valuesEl))
                            HandleSaveModuleConfig(moduleId, valuesEl);
                    }
                    break;

                case "START_CALIBRATION":
                    lock (_calLock)
                    {
                        int n = (int)UnifiedExpressions.Max;
                        _calMin = new float[n]; Array.Fill(_calMin, 1f);
                        _calMax = new float[n]; Array.Fill(_calMax, 0f);
                        _calActive = true;
                    }
                    _calTimer?.Dispose();
                    _calTimer = new System.Threading.Timer(_ => SendCalibrationState(), null, 0, 250);
                    break;

                case "STOP_CALIBRATION":
                    _calTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                    lock (_calLock) { _calActive = false; }
                    SendCalibrationState();
                    break;

                case "RESET_CALIBRATION":
                    lock (_calLock)
                    {
                        if (_calMin == null) break;
                        if (root.TryGetProperty("data", out var rcData)
                            && rcData.TryGetProperty("expressionIndex", out var idxEl)
                            && idxEl.ValueKind == JsonValueKind.Number)
                        {
                            int idx = idxEl.GetInt32();
                            if (idx >= 0 && idx < _calMin.Length)
                            {
                                _calMin[idx] = 1f;
                                _calMax![idx] = 0f;
                            }
                        }
                        else
                        {
                            Array.Fill(_calMin, 1f);
                            Array.Fill(_calMax!, 0f);
                        }
                    }
                    SendCalibrationState();
                    break;
            }
        }
        catch (Exception ex)
        {
            LogService.AddEntry(new LogEntry
            {
                Timestamp = DateTime.UtcNow,
                Level = "Error",
                Source = "App",
                Message = "Error handling web message: " + ex.Message
            });
        }
    }

    static async Task HandleGetRegistryAsync()
    {
        try
        {
            if (_registryService == null) return;
            var modules = await _registryService.GetModulesAsync();
            SendMessage("REGISTRY_MODULES", modules.Select(MapInstallableModule).ToList());
        }
        catch (Exception ex)
        {
            LogService.AddEntry(new LogEntry
            {
                Timestamp = DateTime.UtcNow,
                Level = "Error",
                Source = "App",
                Message = "Registry fetch failed: " + ex.Message
            });
            SendMessage("REGISTRY_MODULES", Array.Empty<object>());
        }
    }

    static async Task HandleInstallModuleAsync(string packageId)
    {
        if (_installer == null || _registryService == null || string.IsNullOrEmpty(packageId))
            return;

        try
        {
            // Find module in cached registry
            var cached = await _registryService.GetModulesAsync();
            var module = cached.FirstOrDefault(m => m.Metadata.PackageId == packageId);
            if (module == null)
            {
                SendMessage("INSTALL_RESULT", new { packageId, success = false, error = "Module not found in registry" });
                return;
            }

            var progress = new Progress<float>(p =>
                SendMessage("INSTALL_PROGRESS", new { packageId, progress = p }));

            bool ok = await _installer.InstallAsync(module, progress);

            if (ok)
            {
                // Write manifest so version + page URL + usage instructions survive restarts
                ModuleRegistryService.WriteManifest(packageId, module.Metadata);
                SendMessage("INSTALL_RESULT", new { packageId, success = true });

                // Notify the lib manager to pick up the new module
                if (_libManager != null && module.InstallPath != null)
                {
                    // Reload triggers dynamic module spawn (not implemented yet - requires restart)
                    LogService.AddEntry(new LogEntry
                    {
                        Timestamp = DateTime.UtcNow,
                        Level = "Information",
                        Source = "App",
                        Message = $"Module {module.Metadata.DisplayName} installed. Restart required to activate."
                    });
                }
            }
            else
            {
                SendMessage("INSTALL_RESULT", new { packageId, success = false, error = module.ErrorMessage });
            }
        }
        catch (Exception ex)
        {
            SendMessage("INSTALL_RESULT", new { packageId, success = false, error = ex.Message });
        }
    }

    static void HandleUninstallModule(string packageId)
    {
        if (_installer == null || _registryService == null) return;
        try
        {
            var local = _registryService.GetLocalModules();
            var module = local.FirstOrDefault(m => m.Metadata.PackageId == packageId);
            if (module == null)
            {
                // Try to uninstall by directory even if not in registry
                module = new InstallableTrackingModule
                {
                    Metadata = new TrackingModuleMetadata { PackageId = packageId }
                };
            }
            bool ok = _installer.Uninstall(module);
            SendMessage("UNINSTALL_RESULT", new { packageId, success = ok });
        }
        catch (Exception ex)
        {
            SendMessage("UNINSTALL_RESULT", new { packageId, success = false, error = ex.Message });
        }
    }

    // --- V2 Module Config ---

    static void HandleGetModuleConfig(string moduleId)
    {
        var mod = _libManager?.GetModuleList().FirstOrDefault(m => m.ModuleId == moduleId);
        if (mod == null || mod.ConfigSchemaJson == null) return;

        // Parse schema and current values so they serialize as real JSON objects (not escaped strings)
        object? schema = null;
        object? values = null;
        try { schema = JsonSerializer.Deserialize<object>(mod.ConfigSchemaJson); } catch { }

        var settingsPath = Path.Combine(Path.GetDirectoryName(mod.ModulePath) ?? "", "settings.json");
        if (File.Exists(settingsPath))
            try { values = JsonSerializer.Deserialize<object>(File.ReadAllText(settingsPath)); } catch { }

        SendMessage("MODULE_CONFIG", new { moduleId, schema, values });
    }

    static void HandleSaveModuleConfig(string moduleId, JsonElement valuesEl)
    {
        var mod = _libManager?.GetModuleList().FirstOrDefault(m => m.ModuleId == moduleId);
        if (mod == null) return;

        var dir = Path.GetDirectoryName(mod.ModulePath) ?? "";
        var settingsPath = Path.Combine(dir, "settings.json");
        try
        {
            var json = JsonSerializer.Serialize(valuesEl, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(settingsPath, json);

            // Push the new values to the running V2 module so it can rebind without a restart.
            // The module's IModuleContext.OnSettingChanged event fires for each changed key.
            // No-op for v1 modules or modules whose pipe isn't currently connected.
            _libManager?.PushSettingsToV2Module(moduleId, JsonSerializer.Serialize(valuesEl));

            SendMessage("MODULE_CONFIG_SAVED", new { moduleId, success = true });
            LogService.AddEntry(new LogEntry
            {
                Timestamp = DateTime.UtcNow,
                Level     = "Information",
                Source    = "App",
                Message   = $"Saved config for module '{mod.ModuleName}' → {settingsPath}"
            });
        }
        catch (Exception ex)
        {
            SendMessage("MODULE_CONFIG_SAVED", new { moduleId, success = false, error = ex.Message });
        }
    }

    // --- Built-in module deployment ---

    static void CopyBuiltInModules(string appDir)
    {
        var builtInDir = Path.Combine(appDir, "builtin-modules");
        if (!Directory.Exists(builtInDir)) return;

        Directory.CreateDirectory(UnifiedLibManager.ModulesDir);

        foreach (var srcDir in Directory.GetDirectories(builtInDir))
        {
            var dirName   = Path.GetFileName(srcDir);
            var destDir   = Path.Combine(UnifiedLibManager.ModulesDir, dirName);

            string srcVersion  = GetManifestVersion(Path.Combine(srcDir, "manifest.json"));
            string destVersion = GetManifestVersion(Path.Combine(destDir, "manifest.json"));

            // Skip if already up-to-date (compare semver; parse failures → always copy)
            if (Directory.Exists(destDir) &&
                !string.IsNullOrEmpty(destVersion) &&
                Version.TryParse(srcVersion, out var sv) &&
                Version.TryParse(destVersion, out var dv) &&
                sv <= dv)
            {
                // Ensure the builtIn flag is present even if not yet tagged
                InjectBuiltInFlag(Path.Combine(destDir, "manifest.json"));
                continue;
            }

            try
            {
                // Wipe and re-copy
                if (Directory.Exists(destDir)) Directory.Delete(destDir, true);
                Directory.CreateDirectory(destDir);

                foreach (var file in Directory.GetFiles(srcDir, "*", SearchOption.AllDirectories))
                {
                    var rel  = Path.GetRelativePath(srcDir, file);
                    var dest = Path.Combine(destDir, rel);
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    File.Copy(file, dest, true);
                }

                InjectBuiltInFlag(Path.Combine(destDir, "manifest.json"));

                LogService.AddEntry(new LogEntry
                {
                    Timestamp = DateTime.UtcNow,
                    Level     = "Information",
                    Source    = "App",
                    Message   = $"Built-in module '{dirName}' installed/updated (v{srcVersion})"
                });
            }
            catch (Exception ex)
            {
                LogService.AddEntry(new LogEntry
                {
                    Timestamp = DateTime.UtcNow,
                    Level     = "Error",
                    Source    = "App",
                    Message   = $"Failed to copy built-in module '{dirName}': {ex.Message}"
                });
            }
        }
    }

    static void InjectBuiltInFlag(string manifestPath)
    {
        if (!File.Exists(manifestPath)) return;
        try
        {
            var json = File.ReadAllText(manifestPath);
            using (var checkDoc = JsonDocument.Parse(json))
            {
                if (checkDoc.RootElement.TryGetProperty("builtIn", out var existing) && existing.GetBoolean())
                    return; // Already tagged
            }

            // Rebuild the JSON object with the builtIn flag appended
            using var ms = new System.IO.MemoryStream();
            var writerOpts = new JsonWriterOptions { Indented = true };
            using var writer = new Utf8JsonWriter(ms, writerOpts);
            writer.WriteStartObject();
            using var doc = JsonDocument.Parse(json);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                prop.WriteTo(writer);
            }
            writer.WriteBoolean("builtIn", true);
            writer.WriteEndObject();
            writer.Flush();
            File.WriteAllBytes(manifestPath, ms.ToArray());
        }
        catch { /* Non-fatal: module will still work, just won't be tagged as built-in */ }
    }

    static string GetManifestVersion(string manifestPath)
    {
        if (!File.Exists(manifestPath)) return "";
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
            if (doc.RootElement.TryGetProperty("version", out var v))
                return v.GetString() ?? "";
        }
        catch { }
        return "";
    }

    static void SendMessage(string type, object? data)
    {
        if (!_isWindowReady) return;
        try
        {
            // _jsonSendOpts uses camelCase so C# PascalCase props (Level, DebugMode, etc.)
            // arrive at the frontend as camelCase (level, debugMode, etc.)
            _window?.Invoke(() =>
                _window?.SendWebMessage(JsonSerializer.Serialize(new { type, data }, _jsonSendOpts)));
        }
        catch { }
    }

    // --- Calibration ---

    static void SendCalibrationState()
    {
        lock (_calLock)
        {
            if (_calMin == null || _calMax == null) return;
            var data = UnifiedTracking.Data;
            int n = Math.Min(_calMin.Length, data.Shapes.Length);

            if (_calActive)
            {
                for (int i = 0; i < n; i++)
                {
                    float w = data.Shapes[i].Weight;
                    if (w < _calMin[i]) _calMin[i] = w;
                    if (w > _calMax[i]) _calMax[i] = w;
                }
            }

            var exprNames = Enum.GetNames<UnifiedExpressions>();
            var progress = new List<object>(n);
            for (int i = 0; i < n && i < exprNames.Length; i++)
            {
                string name = exprNames[i];
                if (name == "Max") continue;
                float current = MathF.Round(data.Shapes[i].Weight, 3);
                float mn = MathF.Round(_calMin[i], 3);
                float mx = MathF.Round(_calMax[i], 3);
                if (_calActive || mx > mn + 0.01f)
                    progress.Add(new { name, min = mn, max = mx, current });
            }

            SendMessage("CALIBRATION_STATE", new { active = _calActive, progress });
        }
    }

    // DTO helpers — strip non-serializable fields before sending to frontend

    static object MapModuleInfo(ModuleRuntimeInfo m) => new
    {
        id = m.ModuleId,
        name = m.ModuleName,
        path = Path.GetFileName(m.ModulePath),
        packageId = Path.GetFileName(Path.GetDirectoryName(m.ModulePath) ?? "") ?? "",
        status = m.Status.ToString(),
        active = m.Active,
        supportsEye = m.SupportsEyeTracking,
        supportsExpression = m.SupportsExpressionTracking,
        crashCount = m.CrashCount,
        retryCount = m.RetryCount,
        lastMessage = m.LastMessage,
        isBuiltIn = m.IsBuiltIn,
        enabled = m.Enabled,
        hasConfig = m.ConfigSchemaJson != null
    };

    static object MapInstallableModule(InstallableTrackingModule m) => new
    {
        packageId = m.Metadata.PackageId,
        displayName = m.Metadata.DisplayName,
        author = m.Metadata.Author,
        description = m.Metadata.Description,
        version = m.Metadata.Version,
        downloadUrl = m.Metadata.DownloadUrl,
        installedVersion = m.InstalledVersion,
        installState = m.InstallState.ToString(),
        usesEye = m.Metadata.UsesEye,
        usesExpression = m.Metadata.UsesExpression,
        tags = m.Metadata.Tags,
        pageUrl = m.Metadata.PageUrl,
        usageInstructions = m.Metadata.UsageInstructions,
        iconUrl = m.Metadata.IconUrl
    };
}
