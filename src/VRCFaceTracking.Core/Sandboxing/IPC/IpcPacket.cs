namespace VRCFaceTracking.Core.Sandboxing.IPC;

public class IpcPacket
{
    internal static readonly byte[] HANDSHAKE_MAGIC = { 0xAF, 0xEC, 0x00, 0x8D };
    internal static int SIZE_PACKET_MAGIC => HANDSHAKE_MAGIC.Length;
    internal static int SIZE_PACKET_TYPE => 4;

    public enum PacketType : uint
    {
        Unknown = 0,
        Handshake = 1,
        SplitPacketChunk = 2,
        Heartbeat = 3, // NEW: heartbeat for crash detection

        MetadataUpdate = 100,
        LipUpdate = 101,
        EyeUpdate = 102,

        EventGetSupported = 200,
        EventInit = 201,
        EventTeardown = 202,
        EventUpdate = 203,
        EventUpdateStatus = 204,
        EventLog = 205,

        ReplyGetSupported = 300,
        ReplyInit = 301,
        ReplyUpdate = 302,
        ReplyTeardown = 303,

        DebugStreamFrame = 1000,
    }

    public virtual PacketType GetPacketType() => PacketType.Unknown;

    public virtual byte[] GetBytes()
    {
        byte[] packetTypeBytes = BitConverter.GetBytes((uint)GetPacketType());
        int packetSize = SIZE_PACKET_MAGIC + SIZE_PACKET_TYPE;
        byte[] data = new byte[packetSize];
        Buffer.BlockCopy(HANDSHAKE_MAGIC, 0, data, 0, SIZE_PACKET_MAGIC);
        Buffer.BlockCopy(packetTypeBytes, 0, data, 4, SIZE_PACKET_TYPE);
        return data;
    }

    public virtual void Decode(in byte[] data) { }
}
