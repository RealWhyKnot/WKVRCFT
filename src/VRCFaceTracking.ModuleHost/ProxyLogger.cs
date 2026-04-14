using Microsoft.Extensions.Logging;

namespace VRCFaceTracking.ModuleHost;

public class ProxyLogger : ILogger
{
    private readonly string _categoryName;

    public static event Action<LogLevel, string>? OnLog;

    public ProxyLogger(string categoryName)
    {
        _categoryName = categoryName;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Debug;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        var message = formatter(state, exception);
        if (exception != null)
            message = message + " | " + exception.ToString();

        // Error/Critical → stderr so the parent process captures them at Warning level.
        // Everything else → stdout (captured at Debug in parent, filtered in prod — that's fine
        // since normal module operation sends logs back via UDP once the client is connected).
        var target = logLevel >= LogLevel.Error ? Console.Error : Console.Out;
        target.WriteLine("[" + logLevel + "] [" + _categoryName + "] " + message);
        OnLog?.Invoke(logLevel, message);
    }
}
