using VRCFaceTracking.Core.Library;

namespace VRCFaceTracking.Core.Sandboxing.IPC;

public class EventStatusUpdatePacket : IpcPacket
{
    public ModuleState ModuleState { get; set; }

    public override PacketType GetPacketType() => PacketType.EventUpdateStatus;

    public override byte[] GetBytes()
    {
        byte[] packetTypeBytes = BitConverter.GetBytes((uint)GetPacketType());
        byte[] stateBytes = BitConverter.GetBytes((int)ModuleState);
        int packetSize = SIZE_PACKET_MAGIC + SIZE_PACKET_TYPE + 4;
        byte[] finalDataStream = new byte[packetSize];
        Buffer.BlockCopy(HANDSHAKE_MAGIC, 0, finalDataStream, 0, SIZE_PACKET_MAGIC);
        Buffer.BlockCopy(packetTypeBytes, 0, finalDataStream, 4, SIZE_PACKET_TYPE);
        Buffer.BlockCopy(stateBytes, 0, finalDataStream, 8, 4);
        return finalDataStream;
    }

    public override void Decode(in byte[] data)
    {
        ModuleState = (ModuleState)BitConverter.ToInt32(data, 8);
    }
}
