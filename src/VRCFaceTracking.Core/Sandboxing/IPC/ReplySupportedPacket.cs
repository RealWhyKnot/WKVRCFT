namespace VRCFaceTracking.Core.Sandboxing.IPC;

public class ReplySupportedPacket : IpcPacket
{
    public bool eyeAvailable = false;
    public bool expressionAvailable = false;

    public override PacketType GetPacketType() => PacketType.ReplyGetSupported;

    public override byte[] GetBytes()
    {
        byte[] packetTypeBytes = BitConverter.GetBytes((uint)GetPacketType());
        int packedData = eyeAvailable ? 1 : 0;
        packedData = packedData | (expressionAvailable ? 2 : 0);
        int packetSize = SIZE_PACKET_MAGIC + SIZE_PACKET_TYPE + 1;
        byte[] finalDataStream = new byte[packetSize];
        Buffer.BlockCopy(HANDSHAKE_MAGIC, 0, finalDataStream, 0, SIZE_PACKET_MAGIC);
        Buffer.BlockCopy(packetTypeBytes, 0, finalDataStream, 4, SIZE_PACKET_TYPE);
        finalDataStream[8] = (byte)packedData;
        return finalDataStream;
    }

    public override void Decode(in byte[] data)
    {
        byte packedData = data[8];
        eyeAvailable = (packedData & 1) == 1;
        expressionAvailable = (packedData & 2) == 2;
    }
}
