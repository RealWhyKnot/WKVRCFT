namespace VRCFaceTracking.Core.Sandboxing.IPC;

public class EventInitGetSupported : IpcPacket
{
    public override PacketType GetPacketType() => PacketType.EventGetSupported;

    public override byte[] GetBytes()
    {
        byte[] packetTypeBytes = BitConverter.GetBytes((uint)GetPacketType());
        int packetSize = SIZE_PACKET_MAGIC + SIZE_PACKET_TYPE;
        byte[] finalDataStream = new byte[packetSize];
        Buffer.BlockCopy(HANDSHAKE_MAGIC, 0, finalDataStream, 0, SIZE_PACKET_MAGIC);
        Buffer.BlockCopy(packetTypeBytes, 0, finalDataStream, 4, SIZE_PACKET_TYPE);
        return finalDataStream;
    }

    public override void Decode(in byte[] data) { }
}
