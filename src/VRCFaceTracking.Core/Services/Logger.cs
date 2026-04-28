using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace VRCFaceTracking.Core.Services;

public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = "";
    public string Source { get; set; } = "";
    public string Message { get; set; } = "";
    public string? Exception { get; set; }
}

public class VrcftLogger : ILogger
{
    private readonly string _source;

    public VrcftLogger(string source)
    {
        _source = source;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Debug;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        LogService.AddEntry(new LogEntry
        {
            Timestamp = DateTime.UtcNow,
            Level = logLevel.ToString(),
            Source = _source,
            Message = formatter(state, exception),
            Exception = exception?.ToString()
        });
    }
}

public class VrcftLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new VrcftLogger(categoryName);
    public void Dispose() { }
}

/// <summary>
/// Per-session log file under the configured directory. Files are kept across
/// the last <see cref="MaxSessionLogs"/> sessions; older ones are pruned at
/// <see cref="Initialize"/>. File writes happen on a dedicated background task
/// drained from a <see cref="BlockingCollection{T}"/>; each line is flushed
/// immediately so a hard kill loses at most the in-flight line.
/// </summary>
public static class LogService
{
    private static readonly ConcurrentQueue<LogEntry> _history = new();
    private static BlockingCollection<LogEntry>? _writeQueue;
    private static Task? _writeTask;
    private static StreamWriter? _fileWriter;
    private static string? _activeLogPath;
    private static int _shutdownStarted;

    public static event Action<LogEntry>? OnLog;

    public static int MaxHistory { get; set; } = 1000;
    public static int MaxSessionLogs { get; set; } = 10;

    /// <summary>Full path of the log file the current session is writing to.</summary>
    public static string? ActiveLogPath => _activeLogPath;

    public static void Initialize(string logDirectory)
    {
        Directory.CreateDirectory(logDirectory);
        PruneOldLogs(logDirectory);

        _activeLogPath = Path.Combine(
            logDirectory,
            "vrcft_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".log");

        _fileWriter = new StreamWriter(_activeLogPath, append: true);
        WriteSessionHeader();

        _writeQueue = new BlockingCollection<LogEntry>(boundedCapacity: 10000);
        _writeTask = Task.Run(ProcessQueue);

        // Best-effort drain on graceful exit even if Shutdown isn't called explicitly
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Shutdown();
        try { Console.CancelKeyPress += (_, _) => Shutdown(); } catch { /* no console */ }
    }

    private static void PruneOldLogs(string logDirectory)
    {
        try
        {
            foreach (var old in Directory.GetFiles(logDirectory, "vrcft_*.log")
                                         .OrderByDescending(File.GetCreationTimeUtc)
                                         .Skip(MaxSessionLogs))
            {
                try { File.Delete(old); } catch { /* in use, skip */ }
            }
        }
        catch { /* directory enum failed; non-fatal */ }
    }

    private static void WriteSessionHeader()
    {
        if (_fileWriter == null) return;
        try
        {
            string sep = new('=', 80);
            _fileWriter.WriteLine(sep);
            _fileWriter.WriteLine("VRCFaceTracking " + VersionInfo.Version);
            _fileWriter.WriteLine("Started:  " + DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"));
            _fileWriter.WriteLine("Machine:  " + Environment.MachineName);
            _fileWriter.WriteLine("OS:       " + Environment.OSVersion.VersionString
                                    + " (" + (Environment.Is64BitOperatingSystem ? "x64" : "x86") + ")");
            _fileWriter.WriteLine(".NET:     " + RuntimeInformation.FrameworkDescription);
            _fileWriter.WriteLine("Process:  " + (Environment.ProcessPath ?? "<unknown>")
                                    + " (PID " + Environment.ProcessId + ")");
            _fileWriter.WriteLine("Log file: " + _activeLogPath);
            _fileWriter.WriteLine(sep);
            _fileWriter.Flush();
        }
        catch { /* header is informational; never block startup */ }
    }

    public static void AddEntry(LogEntry entry)
    {
        _history.Enqueue(entry);
        while (_history.Count > MaxHistory)
            _history.TryDequeue(out _);

        // UI subscribers get the entry on the caller's thread so updates aren't
        // delayed by file I/O on the drain task.
        try { OnLog?.Invoke(entry); } catch { }

        try { _writeQueue?.Add(entry); }
        catch (InvalidOperationException) { /* queue completed during shutdown */ }
        catch (NullReferenceException)    { /* Initialize not called yet */ }
    }

    private static void ProcessQueue()
    {
        if (_writeQueue == null) return;
        foreach (var entry in _writeQueue.GetConsumingEnumerable())
        {
            try { WriteEntry(entry); }
            catch { /* never let the logger crash the drain task */ }
        }
    }

    private static void WriteEntry(LogEntry entry)
    {
        if (_fileWriter == null) return;

        var line = "[" + entry.Timestamp.ToString("HH:mm:ss.fff") + "] ["
                 + entry.Level + "] [" + entry.Source + "] " + entry.Message;
        _fileWriter.WriteLine(line);

        if (!string.IsNullOrEmpty(entry.Exception))
        {
            foreach (var raw in entry.Exception.Split('\n'))
            {
                var trimmed = raw.TrimEnd('\r');
                if (trimmed.Length == 0) continue;
                _fileWriter.WriteLine("    " + trimmed);
            }
        }

        _fileWriter.Flush();
    }

    public static LogEntry[] GetHistory() => _history.ToArray();

    public static void Shutdown()
    {
        if (Interlocked.Exchange(ref _shutdownStarted, 1) != 0) return;

        try
        {
            _writeQueue?.CompleteAdding();
            _writeTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch { }

        try
        {
            _fileWriter?.Flush();
            _fileWriter?.Dispose();
            _fileWriter = null;
        }
        catch { }
    }
}
