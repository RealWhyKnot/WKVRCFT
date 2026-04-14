using Microsoft.Extensions.Logging;

namespace VRCFaceTracking.ModuleHost;

public class ProxyLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new ProxyLogger(categoryName);
    public void Dispose() { }
}
