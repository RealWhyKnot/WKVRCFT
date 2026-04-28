using System.Net;

namespace VRCFaceTracking.Core.OSC.Query.mDNS;

public class ARecord : IDnsSerializer
{
    public IPAddress Address = IPAddress.Loopback;

    public ARecord() { }

    public byte[] Serialize() => Address.GetAddressBytes();

    public void Deserialize(BigReader reader, int expectedLength)
    {
        Address = new IPAddress(reader.ReadBytes(expectedLength));
    }
}
