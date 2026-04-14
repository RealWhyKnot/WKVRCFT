using System.Net;
using System.Net.Sockets;
using VRCFaceTracking.Core.OSC;
using Microsoft.Extensions.Logging;

namespace VRCFaceTracking.Core.Services;

public class OscRecvService : IDisposable
{
    private UdpClient? _udpClient;
    private readonly ILogger<OscRecvService> _logger;
    private CancellationTokenSource _cts = new();
    private Task? _listenTask;

    public event Action<OscMessage>? OnMessageReceived;

    public OscRecvService(ILoggerFactory loggerFactory, int port = 9001)
    {
        _logger = loggerFactory.CreateLogger<OscRecvService>();

        try
        {
            _udpClient = new UdpClient(port);
            _logger.LogInformation("OSC receiver listening on port " + port);
            _listenTask = Task.Run(ListenLoop);
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to create OSC recv UDP client on port " + port + ": " + ex.Message);
        }
    }

    private async Task ListenLoop()
    {
        var buffer = new byte[4096];
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                if (_udpClient == null) break;
                var result = await _udpClient.ReceiveAsync(_cts.Token);
                if (result.Buffer.Length > 0)
                {
                    int index = 0;
                    var msg = OscMessage.TryParseOsc(result.Buffer, result.Buffer.Length, ref index);
                    if (msg != null)
                    {
                        OnMessageReceived?.Invoke(msg);
                    }
                }
            }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (SocketException) { /* VRChat not running */ }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _udpClient?.Dispose();
        _udpClient = null;
    }
}
