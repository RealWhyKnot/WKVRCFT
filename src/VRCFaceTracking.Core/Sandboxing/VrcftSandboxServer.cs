using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VRCFaceTracking.Core.Library;
using VRCFaceTracking.Core.Sandboxing.IPC;

namespace VRCFaceTracking.Core.Sandboxing;

public delegate void OnPacketReceived(in IpcPacket packet, in int port);

public class VrcftSandboxServer : UdpFullDuplex
{
    private static readonly Random Random = new ();

    private List<int>       _connectedClients       = new();
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<VrcftSandboxServer> _logger;
    private Dictionary<int, DateTime> _lastHeartbeat = new();
    public OnPacketReceived? OnPacketReceived;
    public VrcftSandboxServer(ILoggerFactory factory, int[] reservedPorts) : base(0, reservedPorts) // 0 is reserved for the OS to pick for us
    {
        // Init loggers
        _loggerFactory = factory;
        _logger = factory.CreateLogger<VrcftSandboxServer>();

        Port = ( ( IPEndPoint )_receivingUdpClient.Client.LocalEndPoint! ).Port;
        _logger.LogInformation($"Starting sandbox host on port {Port}...");
        _isConnected = true;
    }

    /// <summary>
    /// Gets the last heartbeat time for a given module process ID.
    /// </summary>
    public DateTime? GetLastHeartbeat(int processId)
    {
        return _lastHeartbeat.TryGetValue(processId, out var time) ? time : null;
    }

    /// <summary>
    /// Checks whether a module process has timed out based on the given threshold.
    /// </summary>
    public bool IsModuleTimedOut(int processId, TimeSpan timeout)
    {
        if (!_lastHeartbeat.TryGetValue(processId, out var lastTime))
            return true; // No heartbeat ever received
        return DateTime.UtcNow - lastTime > timeout;
    }

    public override void OnBytesReceived(in byte[] data, in IPEndPoint endpoint)
    {
        bool decodeResult = VrcftPacketDecoder.TryDecodePacket(data, out IpcPacket packet);

        // @TODO: Use packet
        if ( decodeResult )
        {
            if ( packet.GetPacketType() == IpcPacket.PacketType.Handshake )
            {
                // Handshake request
                var handshakePacket = (HandshakePacket) packet;
                if ( handshakePacket.IsValid )
                {
                    _logger.LogInformation($"Received handshake from port {endpoint.Port}. Sending ACK...");
                    // Return ACK
                    SendData(in packet, in endpoint);
                    _connectedClients.Add(endpoint.Port);
                }
            }
            if ( packet.GetPacketType() == IpcPacket.PacketType.Heartbeat )
            {
                // Update heartbeat timestamp for the module process
                var heartbeatPacket = (HeartbeatPacket) packet;
                _lastHeartbeat[heartbeatPacket.ProcessId] = DateTime.UtcNow;
            }
            if ( OnPacketReceived != null )
            {
                if ( packet.GetPacketType() == IpcPacket.PacketType.SplitPacketChunk )
                {
                    PartialPacket.DecodePacket(data, out var combinedData);
                    if ( combinedData.Length > 0 )
                    {
                        OnBytesReceived(combinedData, endpoint);
                    }
                }
                else
                {
                    OnPacketReceived(packet, endpoint.Port);
                }
            }
        }

    }
}
