using System.Text;

namespace VRCFaceTracking.Core.Sandboxing.IPC;

public class ReplyInitPacket : IpcPacket
{
    public bool eyeSuccess = false;
    public bool expressionSuccess = false;
    public string ModuleInformationName = "";
    public List<Stream> IconDataStreams = new();

    public override PacketType GetPacketType() => PacketType.ReplyInit;

    public override byte[] GetBytes()
    {
        byte[] packetTypeBytes = BitConverter.GetBytes((uint)GetPacketType());
        int packedData = eyeSuccess ? 1 : 0;
        packedData = packedData | (expressionSuccess ? 2 : 0);

        byte[] nameBytes = Encoding.UTF8.GetBytes(ModuleInformationName);
        byte[] nameLengthBytes = BitConverter.GetBytes(nameBytes.Length);

        // Collect icon data
        var iconDataList = new List<byte[]>();
        foreach (var stream in IconDataStreams)
        {
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            iconDataList.Add(ms.ToArray());
        }

        int totalIconSize = 0;
        foreach (var icon in iconDataList)
            totalIconSize += 4 + icon.Length;

        int packetSize = SIZE_PACKET_MAGIC + SIZE_PACKET_TYPE + 1 + 4 + nameBytes.Length + 4 + totalIconSize;
        byte[] finalDataStream = new byte[packetSize];
        int offset = 0;

        Buffer.BlockCopy(HANDSHAKE_MAGIC, 0, finalDataStream, offset, SIZE_PACKET_MAGIC); offset += SIZE_PACKET_MAGIC;
        Buffer.BlockCopy(packetTypeBytes, 0, finalDataStream, offset, SIZE_PACKET_TYPE); offset += SIZE_PACKET_TYPE;
        finalDataStream[offset] = (byte)packedData; offset += 1;
        Buffer.BlockCopy(nameLengthBytes, 0, finalDataStream, offset, 4); offset += 4;
        Buffer.BlockCopy(nameBytes, 0, finalDataStream, offset, nameBytes.Length); offset += nameBytes.Length;

        byte[] iconCountBytes = BitConverter.GetBytes(iconDataList.Count);
        Buffer.BlockCopy(iconCountBytes, 0, finalDataStream, offset, 4); offset += 4;
        foreach (var icon in iconDataList)
        {
            byte[] iconLenBytes = BitConverter.GetBytes(icon.Length);
            Buffer.BlockCopy(iconLenBytes, 0, finalDataStream, offset, 4); offset += 4;
            Buffer.BlockCopy(icon, 0, finalDataStream, offset, icon.Length); offset += icon.Length;
        }

        return finalDataStream;
    }

    public override void Decode(in byte[] data)
    {
        int offset = 8;
        byte packedData = data[offset]; offset += 1;
        eyeSuccess = (packedData & 1) == 1;
        expressionSuccess = (packedData & 2) == 2;

        int nameLength = BitConverter.ToInt32(data, offset); offset += 4;
        ModuleInformationName = Encoding.UTF8.GetString(data, offset, nameLength); offset += nameLength;

        if (offset + 4 <= data.Length)
        {
            int iconCount = BitConverter.ToInt32(data, offset); offset += 4;
            for (int i = 0; i < iconCount && offset + 4 <= data.Length; i++)
            {
                int iconLen = BitConverter.ToInt32(data, offset); offset += 4;
                if (offset + iconLen <= data.Length)
                {
                    var iconData = new byte[iconLen];
                    Buffer.BlockCopy(data, offset, iconData, 0, iconLen);
                    IconDataStreams.Add(new MemoryStream(iconData));
                    offset += iconLen;
                }
            }
        }
    }
}
