using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using VRCFaceTracking.Core.Sandboxing;
using VRCFaceTracking.Core.Sandboxing.IPC;
using VRCFaceTracking.Core.Library;
using VRCFaceTracking.Core.Params.Expressions;

namespace VRCFaceTracking.ModuleHost;

class Program
{
    private static int _hostPort;
    private static string _modulePath = "";
    private static VrcftSandboxClient? _client;
    private static ModuleAssembly? _moduleAssembly;
    private static CancellationTokenSource _updateCts = new();
    private static Thread? _updateThread;
    private static Timer? _heartbeatTimer;
    private static bool _initialized = false;
    private static readonly ConcurrentQueue<IpcPacket> _packetsToSend = new();
    private static ILoggerFactory? _loggerFactory;

    static int Main(string[] args)
    {
        if (!ParseArgs(args))
        {
            Console.Error.WriteLine("Usage: VRCFaceTracking.ModuleHost --port <port> --module-path <path>");
            return 1;
        }

        try
        {
            // Set up logging
            _loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.AddProvider(new ProxyLoggerProvider());
            });

            var logger = _loggerFactory.CreateLogger("ModuleHost");
            logger.LogInformation("ModuleHost starting for: " + _modulePath);
            logger.LogInformation("Host port: " + _hostPort);

            // Load module
            _moduleAssembly = new ModuleAssembly(_modulePath, logger);
            if (!_moduleAssembly.TryLoadAssembly())
            {
                logger.LogError("Failed to load module assembly");
                return 2;
            }

            // Initialize tracking data to sentinel/invalid state
            for (int i = 0; i < UnifiedTracking.Data.Shapes.Length; i++)
                UnifiedTracking.Data.Shapes[i].Weight = float.NaN;

            // Create sandbox client
            _client = new VrcftSandboxClient(_hostPort, _loggerFactory);

            // Wire log forwarding
            ProxyLogger.OnLog += (level, message) =>
            {
                if (_client != null)
                {
                    var logPacket = new EventLogPacket { LogLevel = level, Message = message };
                    _client.SendData(logPacket);
                }
            };

            // Wire packet handler
            _client.OnPacketReceivedCallback += HandlePacket;

            // Connect to host
            _client.Connect(_modulePath);

            // Start heartbeat
            _heartbeatTimer = new Timer(_ =>
            {
                try
                {
                    var heartbeat = new HeartbeatPacket { ProcessId = Process.GetCurrentProcess().Id };
                    _client?.SendData(heartbeat);
                }
                catch { }
            }, null, 0, 2000);

            // Main loop
            var timeout = DateTime.UtcNow.AddSeconds(60);
            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
            AppDomain.CurrentDomain.ProcessExit += (_, _) => Shutdown();

            while (!cts.IsCancellationRequested)
            {
                // Send any queued packets
                while (_packetsToSend.TryDequeue(out var pkt))
                    _client.SendData(pkt);

                if (!_initialized && DateTime.UtcNow > timeout)
                {
                    logger.LogError("Initialization timeout - no init packet received within 60s");
                    return 3;
                }

                Thread.Sleep(1);
            }

            Shutdown();
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FATAL: " + ex.ToString());
            return 99;
        }
    }

    static void HandlePacket(in IpcPacket packet)
    {
        var module = _moduleAssembly?.TrackingModule;
        if (module == null) return;

        switch (packet.GetPacketType())
        {
            case IpcPacket.PacketType.EventGetSupported:
            {
                var reply = new ReplySupportedPacket
                {
                    eyeAvailable = module.Supported.SupportsEye,
                    expressionAvailable = module.Supported.SupportsExpression
                };
                _packetsToSend.Enqueue(reply);
                break;
            }

            case IpcPacket.PacketType.EventInit:
            {
                var initPkt = (EventInitPacket)packet;
                try
                {
                    var (eyeOk, exprOk) = module.Initialize(initPkt.eyeAvailable, initPkt.expressionAvailable);

                    // Start update thread
                    _updateCts = new CancellationTokenSource();
                    _updateThread = new Thread(() =>
                    {
                        while (!_updateCts.IsCancellationRequested)
                        {
                            try
                            {
                                module.Update();
                            }
                            catch (Exception ex)
                            {
                                Console.Error.WriteLine("Module.Update() error: " + ex.Message);
                            }
                            Thread.Sleep(10); // ~100Hz
                        }
                    })
                    { IsBackground = true, Name = "ModuleUpdateThread" };
                    _updateThread.Start();

                    _initialized = true;

                    var reply = new ReplyInitPacket
                    {
                        eyeSuccess = eyeOk,
                        expressionSuccess = exprOk,
                        ModuleInformationName = module.ModuleInformation.Name ?? "Unknown Module",
                        IconDataStreams = module.ModuleInformation.StaticImages ?? new List<Stream>()
                    };
                    _packetsToSend.Enqueue(reply);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("Module.Initialize() error: " + ex.Message);
                    var reply = new ReplyInitPacket
                    {
                        eyeSuccess = false,
                        expressionSuccess = false,
                        ModuleInformationName = "Error: " + ex.Message,
                        IconDataStreams = new List<Stream>()
                    };
                    _packetsToSend.Enqueue(reply);
                }
                break;
            }

            case IpcPacket.PacketType.EventUpdate:
            {
                var reply = new ReplyUpdatePacket();
                _packetsToSend.Enqueue(reply);
                break;
            }

            case IpcPacket.PacketType.EventTeardown:
            {
                Shutdown();
                var reply = new ReplyTeardownPacket();
                _packetsToSend.Enqueue(reply);
                // Give time to send the reply
                Thread.Sleep(100);
                Environment.Exit(0);
                break;
            }

            case IpcPacket.PacketType.EventUpdateStatus:
            {
                var statusPkt = (EventStatusUpdatePacket)packet;
                module.Status = statusPkt.ModuleState;
                break;
            }
        }
    }

    static void Shutdown()
    {
        _updateCts.Cancel();
        _heartbeatTimer?.Dispose();

        try
        {
            _moduleAssembly?.TrackingModule?.Teardown();
        }
        catch { }

        _client?.Dispose();
    }

    static bool ParseArgs(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--port" when i + 1 < args.Length:
                    if (!int.TryParse(args[++i], out _hostPort)) return false;
                    break;
                case "--module-path" when i + 1 < args.Length:
                    _modulePath = args[++i];
                    break;
            }
        }
        return _hostPort > 0 && !string.IsNullOrEmpty(_modulePath);
    }
}
