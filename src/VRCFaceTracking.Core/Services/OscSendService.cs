using System.Net;
using System.Net.Sockets;
using VRCFaceTracking.Core.OSC;
using Microsoft.Extensions.Logging;

namespace VRCFaceTracking.Core.Services;

public class OscSendService : IDisposable
{
    private UdpClient? _udpClient;
    private IPEndPoint _remoteEndPoint;
    private readonly ILogger<OscSendService> _logger;
    private readonly byte[] _sendBuffer = new byte[4096];
    private int _messagesSentThisSecond;
    private DateTime _lastCountReset = DateTime.UtcNow;

    public int MessagesPerSecond { get; private set; }
    public bool IsConnected => _udpClient != null;
    public string TargetIp => _remoteEndPoint.Address.ToString();
    public int TargetPort => _remoteEndPoint.Port;

    public OscSendService(ILoggerFactory loggerFactory, string ip = "127.0.0.1", int port = 9000)
    {
        _logger = loggerFactory.CreateLogger<OscSendService>();
        _remoteEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);

        try
        {
            _udpClient = new UdpClient();
            _logger.LogInformation("OSC sender initialized targeting " + ip + ":" + port);
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to create OSC UDP client: " + ex.Message);
        }
    }

    public void UpdateTarget(string ip, int port)
    {
        _remoteEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
        _logger.LogInformation("OSC target updated to " + ip + ":" + port);
    }

    public void Send(OscMessageMeta message)
    {
        if (_udpClient == null) return;

        try
        {
            int bytesWritten = fti_osc.create_osc_message(_sendBuffer, ref message);
            if (bytesWritten > 0)
            {
                _udpClient.Send(_sendBuffer, bytesWritten, _remoteEndPoint);
                _messagesSentThisSecond++;
            }

            UpdateMessageRate();
        }
        catch (SocketException)
        {
            // Silently handle - VRChat may not be running
        }
    }

    public void SendBundle(OscMessageMeta[] messages)
    {
        if (_udpClient == null || messages.Length == 0) return;

        try
        {
            int messageIndex = 0;
            while (messageIndex < messages.Length)
            {
                int bytesWritten = fti_osc.create_osc_bundle(_sendBuffer, messages, messages.Length, ref messageIndex);
                if (bytesWritten > 0)
                {
                    _udpClient.Send(_sendBuffer, bytesWritten, _remoteEndPoint);
                    _messagesSentThisSecond++;
                }
            }

            UpdateMessageRate();
        }
        catch (SocketException)
        {
            // Silently handle - VRChat may not be running
        }
    }

    private void UpdateMessageRate()
    {
        var now = DateTime.UtcNow;
        if ((now - _lastCountReset).TotalSeconds >= 1.0)
        {
            MessagesPerSecond = _messagesSentThisSecond;
            _messagesSentThisSecond = 0;
            _lastCountReset = now;
        }
    }

    public void Dispose()
    {
        _udpClient?.Dispose();
        _udpClient = null;
    }
}
