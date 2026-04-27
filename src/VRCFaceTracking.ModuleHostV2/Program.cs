using VRCFaceTracking.Core.Sandboxing.V2;
using VRCFaceTracking.ModuleHostV2;

class Program
{
    private static string _pipeName = "";
    private static string _modulePath = "";

    static async Task<int> Main(string[] args)
    {
        if (!ParseArgs(args))
        {
            Console.Error.WriteLine("Usage: VRCFaceTracking.ModuleHostV2 --pipe-name <name> --module-path <path>");
            return 1;
        }

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
        AppDomain.CurrentDomain.ProcessExit += (_, _) => { try { cts.Cancel(); } catch (ObjectDisposedException) { } };

        // Load module
        using var assembly = new ModuleAssemblyV2();
        if (!assembly.TryLoad(_modulePath))
        {
            Console.Error.WriteLine("Failed to load module assembly");
            return 2;
        }

        var module = assembly.Module!;
        var meta = assembly.Metadata;

        string moduleName = meta?.Name ?? module.GetType().Name;
        string moduleAuthor = meta?.Author ?? "";
        string moduleVersion = meta?.Version ?? "1.0.0";
        int capabilities = (int)module.Capabilities;

        Console.WriteLine($"Loaded V2 module: {moduleName} v{moduleVersion}");

        // Connect to host pipe
        using var pipe = new V2PipeClient(_pipeName);
        if (!await pipe.ConnectAsync(cts.Token))
        {
            Console.Error.WriteLine("Failed to connect to host pipe");
            return 3;
        }

        string moduleDirPath = Path.GetDirectoryName(_modulePath) ?? Path.GetTempPath();
        var context = new V2ModuleContext(pipe, moduleDirPath, cts.Token);

        bool initCompleted = false;
        bool shutdownRequested = false;
        Exception? initException = null;

        // Message handler: runs concurrently with the read loop
        pipe.OnMessageReceived += async msg =>
        {
            try
            {
                switch (msg.Type)
                {
                    case V2MessageType.Handshake:
                        // Reply with our metadata and capabilities
                        await pipe.SendHandshakeAckAsync(moduleName, moduleAuthor, moduleVersion, capabilities, cts.Token);
                        break;

                    case V2MessageType.Init:
                        try
                        {
                            bool success = await module.InitializeAsync(context);
                            await pipe.SendInitResultAsync(success, cts.Token);
                            initCompleted = success;
                        }
                        catch (Exception ex)
                        {
                            Console.Error.WriteLine($"InitializeAsync error: {ex.Message}");
                            await pipe.SendInitResultAsync(false, cts.Token);
                            initException = ex;
                        }
                        break;

                    case V2MessageType.Shutdown:
                        shutdownRequested = true;
                        try { await module.ShutdownAsync(); } catch { }
                        await pipe.SendShutdownAckAsync(cts.Token);
                        cts.Cancel();
                        break;

                    case V2MessageType.Settings:
                        if (msg.Payload is { } settingsJson)
                        {
                            try
                            {
                                var dict = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, System.Text.Json.JsonElement>>(settingsJson);
                                if (dict != null) context.ApplyHostSettings(dict);
                            }
                            catch (Exception ex)
                            {
                                Console.Error.WriteLine($"Settings push parse error: {ex.Message}");
                            }
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Message handler error: {ex.Message}");
            }
        };

        // Start pipe read loop in background
        var readTask = Task.Run(() => pipe.ReadLoopAsync(cts.Token), cts.Token);

        // Wait for init to complete (with timeout)
        var initTimeout = DateTime.UtcNow.AddSeconds(60);
        while (!initCompleted && initException == null && !cts.IsCancellationRequested)
        {
            if (DateTime.UtcNow > initTimeout)
            {
                Console.Error.WriteLine("Init timeout (60s)");
                return 4;
            }
            await Task.Delay(10, cts.Token).ContinueWith(_ => { }); // swallow cancellation
        }

        if (initException != null || !initCompleted)
            return 5;

        // Update loop: calls UpdateAsync, then flushes tracking data
        try
        {
            while (!cts.IsCancellationRequested && !shutdownRequested)
            {
                try
                {
                    await module.UpdateAsync(cts.Token);
                    await context.Writer.FlushAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"UpdateAsync error: {ex.Message}");
                }

                // ~100Hz
                await Task.Delay(10, cts.Token).ContinueWith(_ => { });
            }
        }
        finally
        {
            if (!shutdownRequested)
            {
                try { await module.ShutdownAsync(); } catch { }
            }
        }

        await readTask.ContinueWith(_ => { });
        return 0;
    }

    static bool ParseArgs(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--pipe-name" when i + 1 < args.Length:
                    _pipeName = args[++i];
                    break;
                case "--module-path" when i + 1 < args.Length:
                    _modulePath = args[++i];
                    break;
            }
        }
        return !string.IsNullOrEmpty(_pipeName) && !string.IsNullOrEmpty(_modulePath);
    }
}
