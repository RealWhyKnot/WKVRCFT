using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace VRCFaceTracking.Core.Services;

public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = "";
    public string Source { get; set; } = "";
    public string Message { get; set; } = "";
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

        var entry = new LogEntry
        {
            Timestamp = DateTime.UtcNow,
            Level = logLevel.ToString(),
            Source = _source,
            Message = formatter(state, exception)
        };

        if (exception != null)
            entry.Message = entry.Message + " | " + exception.ToString();

        LogService.AddEntry(entry);
    }
}

public class VrcftLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new VrcftLogger(categoryName);
    public void Dispose() { }
}

public static class LogService
{
    private static readonly ConcurrentQueue<LogEntry> _history = new();
    private static readonly object _fileLock = new();
    private static StreamWriter? _fileWriter;
    private static string? _logDir;

    public static event Action<LogEntry>? OnLog;

    public static int MaxHistory { get; set; } = 1000;

    public static void Initialize(string logDirectory)
    {
        _logDir = logDirectory;
        Directory.CreateDirectory(logDirectory);

        var logFile = Path.Combine(logDirectory, "vrcft_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".log");
        _fileWriter = new StreamWriter(logFile, append: true) { AutoFlush = true };
    }

    public static void AddEntry(LogEntry entry)
    {
        _history.Enqueue(entry);
        while (_history.Count > MaxHistory)
            _history.TryDequeue(out _);

        var line = "[" + entry.Timestamp.ToString("HH:mm:ss.fff") + "] [" + entry.Level + "] [" + entry.Source + "] " + entry.Message;
        lock (_fileLock)
        {
            _fileWriter?.WriteLine(line);
        }

        OnLog?.Invoke(entry);
    }

    public static LogEntry[] GetHistory() => _history.ToArray();

    public static void Shutdown()
    {
        lock (_fileLock)
        {
            _fileWriter?.Flush();
            _fileWriter?.Dispose();
            _fileWriter = null;
        }
    }
}