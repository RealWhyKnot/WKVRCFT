namespace VRCFaceTracking.Core.OSC.Query.mDNS;

public class PTRRecord : IDnsSerializer
{
    public List<string> DomainLabels = new();

    public PTRRecord() { }

    public byte[] Serialize() => BigWriter.WriteDomainLabels(DomainLabels);

    public void Deserialize(BigReader reader, int expectedLength)
    {
        DomainLabels = reader.ReadDomainLabels();
    }
}
