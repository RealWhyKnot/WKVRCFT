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
    private readonly object _statsLock = new();
    private readonly Timer _statsTimer;

    private bool _firstPacketLogged;
    private bool _targetDiscovered;
    private long _packetsSinceReport;
    private long _bytesSinceReport;
    private long _failuresSinceReport;
    private long _packetsTotal;
    private DateTime _lastReportUtc = DateTime.UtcNow;

    public int MessagesPerSecond { get; private set; }
    public bool IsConnected => _udpClient != null;
    public string TargetIp => _remoteEndPoint.Address.ToString();
    public int TargetPort => _remoteEndPoint.Port;

    /// <summary>True once <see cref="UpdateTarget"/> has been called from outside (typically from OSCQuery discovery), so the constructor default isn't masking a failed discovery.</summary>
    public bool TargetDiscovered => _targetDiscovered;

    public OscSendService(ILoggerFactory loggerFactory, string ip = "127.0.0.1", int port = 9000)
    {
        _logger = loggerFactory.CreateLogger<OscSendService>();
        _remoteEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);

        try
        {
            _udpClient = new UdpClient();
            _logger.LogInformation("OSC sender initialized targeting {Ip}:{Port} (default; awaits OSCQuery discovery)", ip, port);
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to create OSC UDP client: {Message}", ex.Message);
        }

        // 10-second periodic throughput report. First tick after 10s.
        _statsTimer = new Timer(_ => ReportStats(), null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
    }

    public void UpdateTarget(string ip, int port)
    {
        var prev = _remoteEndPoint;
        _remoteEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
        _targetDiscovered = true;

        if (!Equals(prev, _remoteEndPoint))
            _logger.LogInformation("OSC target updated: {Prev} -> {Next}", prev, _remoteEndPoint);
        else
            _logger.LogDebug("OSC target re-confirmed at {Endpoint}", _remoteEndPoint);
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
                Bump(1, bytesWritten);
            }
        }
        catch (SocketException ex)
        {
            BumpFailure();
            _logger.LogDebug(ex, "OSC send failed (single)");
        }
    }

    public void SendBundle(OscMessageMeta[] messages)
    {
        if (_udpClient == null || messages.Length == 0) return;

        try
        {
            int messageIndex = 0;
            int totalBytes = 0;
            int bundleCount = 0;
            while (messageIndex < messages.Length)
            {
                int bytesWritten = fti_osc.create_osc_bundle(_sendBuffer, messages, messages.Length, ref messageIndex);
                if (bytesWritten > 0)
                {
                    _udpClient.Send(_sendBuffer, bytesWritten, _remoteEndPoint);
                    totalBytes += bytesWritten;
                    bundleCount++;
                }
                else
                {
                    // create_osc_bundle returned 0 — break to avoid infinite loop on malformed input.
                    break;
                }
            }
            if (bundleCount > 0) Bump(bundleCount, totalBytes);
        }
        catch (SocketException ex)
        {
            BumpFailure();
            _logger.LogDebug(ex, "OSC send failed (bundle)");
        }
    }

    private void Bump(int packets, int bytes)
    {
        lock (_statsLock)
        {
            _packetsSinceReport += packets;
            _bytesSinceReport   += bytes;
            _packetsTotal       += packets;

            if (!_firstPacketLogged)
            {
                _firstPacketLogged = true;
                _logger.LogInformation("OSC: first packet(s) sent to {Endpoint} ({Bytes} bytes)", _remoteEndPoint, bytes);
            }
        }
    }

    private void BumpFailure()
    {
        lock (_statsLock) { _failuresSinceReport++; }
    }

    private void ReportStats()
    {
        long packets, bytes, failures;
        DateTime now = DateTime.UtcNow;
        TimeSpan elapsed;
        lock (_statsLock)
        {
            packets = _packetsSinceReport;
            bytes   = _bytesSinceReport;
            failures = _failuresSinceReport;
            elapsed = now - _lastReportUtc;

            _packetsSinceReport = 0;
            _bytesSinceReport   = 0;
            _failuresSinceReport = 0;
            _lastReportUtc      = now;
        }

        if (packets == 0 && failures == 0)
        {
            // Don't spam the log with idle ticks until we've seen activity at least once.
            if (!_firstPacketLogged) return;
            _logger.LogInformation("OSC stats: idle (no packets in last {Seconds:F1}s) target={Target}", elapsed.TotalSeconds, _remoteEndPoint);
            return;
        }

        var pps = packets / elapsed.TotalSeconds;
        var bps = bytes / elapsed.TotalSeconds;
        MessagesPerSecond = (int)Math.Round(pps);

        if (failures == 0)
            _logger.LogInformation("OSC stats: {Pps:F1} pkt/s, {Kbps:F1} KB/s, {Total} total, target={Target}",
                pps, bps / 1024.0, _packetsTotal, _remoteEndPoint);
        else
            _logger.LogWarning("OSC stats: {Pps:F1} pkt/s, {Kbps:F1} KB/s, {Total} total, {Failures} send failures in last {Seconds:F1}s, target={Target}",
                pps, bps / 1024.0, _packetsTotal, failures, elapsed.TotalSeconds, _remoteEndPoint);
    }

    public void Dispose()
    {
        _statsTimer.Dispose();
        _udpClient?.Dispose();
        _udpClient = null;
    }
}
