using Microsoft.Extensions.Logging;
using System.Text;

namespace VRCFaceTracking.Core.Sandboxing.IPC;

public class EventLogPacket : IpcPacket
{
    public LogLevel LogLevel { get; set; }
    public string Message { get; set; } = "";

    public override PacketType GetPacketType() => PacketType.EventLog;

    public override byte[] GetBytes()
    {
        byte[] packetTypeBytes = BitConverter.GetBytes((uint)GetPacketType());
        byte[] logLevelBytes = BitConverter.GetBytes((int)LogLevel);
        byte[] messageBytes = Encoding.UTF8.GetBytes(Message);
        byte[] messageLengthBytes = BitConverter.GetBytes(messageBytes.Length);

        int packetSize = SIZE_PACKET_MAGIC + SIZE_PACKET_TYPE + 4 + 4 + messageBytes.Length;
        byte[] finalDataStream = new byte[packetSize];
        Buffer.BlockCopy(HANDSHAKE_MAGIC, 0, finalDataStream, 0, SIZE_PACKET_MAGIC);
        Buffer.BlockCopy(packetTypeBytes, 0, finalDataStream, 4, SIZE_PACKET_TYPE);
        Buffer.BlockCopy(logLevelBytes, 0, finalDataStream, 8, 4);
        Buffer.BlockCopy(messageLengthBytes, 0, finalDataStream, 12, 4);
        Buffer.BlockCopy(messageBytes, 0, finalDataStream, 16, messageBytes.Length);
        return finalDataStream;
    }

    public override void Decode(in byte[] data)
    {
        LogLevel = (LogLevel)BitConverter.ToInt32(data, 8);
        int messageLength = BitConverter.ToInt32(data, 12);
        Message = Encoding.UTF8.GetString(data, 16, messageLength);
    }
}
