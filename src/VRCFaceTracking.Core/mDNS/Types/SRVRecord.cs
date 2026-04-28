namespace VRCFaceTracking.Core.OSC.Query.mDNS;

public class SRVRecord : IDnsSerializer
{
    public ushort Priority;
    public ushort Weight;
    public ushort Port;
    public List<string> Target = new();

    public byte[] Serialize()
    {
        List<byte> bytes = new List<byte>();
        bytes.AddRange(BigWriter.WriteUInt16(Priority));
        bytes.AddRange(BigWriter.WriteUInt16(Weight));
        bytes.AddRange(BigWriter.WriteUInt16(Port));
        bytes.AddRange(BigWriter.WriteDomainLabels(Target));
        return bytes.ToArray();
    }

    public void Deserialize(BigReader reader, int expectedLength)
    {
        Priority = reader.ReadUInt16();
        Weight   = reader.ReadUInt16();
        Port     = reader.ReadUInt16();
        Target   = reader.ReadDomainLabels();
    }
}
