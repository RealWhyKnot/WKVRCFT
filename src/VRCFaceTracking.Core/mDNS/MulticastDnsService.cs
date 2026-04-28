using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using VRCFaceTracking.Core.OSC.Query.mDNS;

namespace VRCFaceTracking.Core.mDNS;

/// <summary>
/// Self-contained mDNS responder used to discover the VRChat client's OSCQuery
/// endpoint (advertised at <c>_oscjson._tcp.local</c>) and to advertise our own
/// OSC services back to the network so VRChat sends avatar/parameter events to
/// us. Mirrors the reference VRCFaceTracking implementation; does not depend on
/// any external mDNS library.
/// </summary>
public class MulticastDnsService
{
    private static readonly IPAddress MulticastIp = IPAddress.Parse("224.0.0.251");
    private const int MulticastPost = 5353;
    private static readonly IPEndPoint MdnsEndpointIp4 = new(MulticastIp, MulticastPost);

    private readonly List<IPAddress> _localIpAddresses;
    private readonly ILogger<MulticastDnsService> _logger;

    private static readonly Dictionary<IPAddress, UdpClient> Senders   = new();
    private static readonly Dictionary<UdpClient, CancellationToken> Receivers = new();
    private static readonly Dictionary<string, AdvertisedService>    Services = new();

    /// <summary>Fired the first time a VRChat client is discovered on the network.</summary>
    public Action OnVrcClientDiscovered = () => { };

    /// <summary>The discovered VRChat client OSCQuery endpoint, or <c>null</c> if not yet discovered.</summary>
    public IPEndPoint? VrchatClientEndpoint { get; private set; }

    private static List<NetworkInterface> GetIpv4NetInterfaces() => NetworkInterface.GetAllNetworkInterfaces()
        .Where(net =>
            net.OperationalStatus == OperationalStatus.Up &&
            net.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
            (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
             (net.SupportsMulticast && net.GetIPProperties().MulticastAddresses.Count != 0)))
        .ToList();

    private static IPAddress? GetIpv4Address(NetworkInterface net) => net.GetIPProperties()
        .UnicastAddresses
        .Select(addr => addr.Address)
        .FirstOrDefault(addr => addr.AddressFamily == AddressFamily.InterNetwork);

    public MulticastDnsService(ILogger<MulticastDnsService> logger)
    {
        _logger = logger;

        _localIpAddresses = GetIpv4NetInterfaces()
            .Select(GetIpv4Address)
            .Where(addr => addr != null)
            .Cast<IPAddress>()
            .ToList();

        // Always include loopback so a same-machine VRChat is reachable even when
        // no external interface is up.
        if (!_localIpAddresses.Any(IPAddress.IsLoopback))
            _localIpAddresses.Add(IPAddress.Loopback);

        var cts = new CancellationTokenSource();
        var receiver = new UdpClient(AddressFamily.InterNetwork);
        receiver.Client.ExclusiveAddressUse = false;
        receiver.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        receiver.Client.Bind(new IPEndPoint(IPAddress.Any, MulticastPost));
        Receivers.Add(receiver, cts.Token);

        foreach (var ipAddress in _localIpAddresses)
        {
            try
            {
                receiver.JoinMulticastGroup(MulticastIp, ipAddress);

                var sender = new UdpClient(ipAddress.AddressFamily);
                sender.Client.ExclusiveAddressUse = false;
                sender.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                sender.Client.Bind(new IPEndPoint(ipAddress, MulticastPost));
                sender.JoinMulticastGroup(MulticastIp, ipAddress);
                sender.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastLoopback, true);

                Receivers.Add(sender, cts.Token);
                Senders.Add(ipAddress, sender);

                _logger.LogDebug("mDNS bound to interface {Interface}", ipAddress);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("mDNS could not bind to {Interface}: {Message}", ipAddress, ex.Message);
            }
        }

        foreach (var sender in Receivers)
        {
            Listen(sender.Key, sender.Value);
        }

        _logger.LogInformation("mDNS service started on {Count} interface(s)", _localIpAddresses.Count);
    }

    private async void ResolveDnsQueries(DnsPacket packet, IPEndPoint remoteEndpoint)
    {
        if (packet.OPCODE != 0 || VrchatClientEndpoint == null)
            return;

        foreach (var question in packet.questions)
        {
            if (question.Labels.Count != 3
                || question.Labels[2] != "local"
                || !Services.TryGetValue($"{question.Labels[0]}.{question.Labels[1]}", out var service))
            {
                continue;
            }

            var qualifiedServiceName = new List<string>
            {
                service.ServiceName,
                question.Labels[0],
                question.Labels[1],
                question.Labels[2]
            };

            var serviceName = new List<string>
            {
                service.ServiceName,
                question.Labels[0].Trim('_'),
                question.Labels[1].Trim('_')
            };

            var txt = new TXTRecord { Text = new List<string> { "txtvers=1" } };
            var srv = new SRVRecord { Port = (ushort)service.Port, Target = serviceName };
            var aRecord = new ARecord { Address = service.Address };
            var ptrRecord = new PTRRecord { DomainLabels = qualifiedServiceName };

            var additionalRecords = new List<DnsResource>
            {
                new(txt, qualifiedServiceName),
                new(srv, qualifiedServiceName),
                new(aRecord, serviceName)
            };

            var answers = new List<DnsResource> { new(ptrRecord, question.Labels) };

            var response = new DnsPacket
            {
                CONFLICT      = true,
                ID            = 0,
                OPCODE        = 0,
                QUERYRESPONSE = true,
                RESPONSECODE  = 0,
                TENTATIVE     = false,
                TRUNCATION    = false,
                questions     = Array.Empty<DnsQuestion>(),
                answers       = answers.ToArray(),
                authorities   = Array.Empty<DnsResource>(),
                additionals   = additionalRecords.ToArray()
            };

            var bytes = response.Serialize();

            if (remoteEndpoint.Port == MulticastPost)
            {
                foreach (var sender in Senders)
                {
                    try { await sender.Value.SendAsync(bytes, bytes.Length, MdnsEndpointIp4); }
                    catch (Exception ex) { _logger.LogDebug("mDNS multicast reply failed on {Iface}: {Msg}", sender.Key, ex.Message); }
                }
            }
        }
    }

    public async void SendQuery(string labels)
    {
        var dnsPacket = new DnsPacket();
        var dnsQuestion = new DnsQuestion(labels.Split('.').ToList(), 255, 1);
        dnsPacket.questions     = new[] { dnsQuestion };
        dnsPacket.QUERYRESPONSE = false;
        dnsPacket.OPCODE        = 0;
        dnsPacket.TRUNCATION    = false;

        var bytes = dnsPacket.Serialize();

        _logger.LogInformation("mDNS query sent: {Labels}", labels);

        foreach (var sender in Senders)
        {
            try { await sender.Value.SendAsync(bytes, bytes.Length, MdnsEndpointIp4); }
            catch (Exception ex) { _logger.LogDebug("mDNS query send failed on {Iface}: {Msg}", sender.Key, ex.Message); }
        }
    }

    private void ResolveVrChatClient(DnsPacket packet, IPEndPoint remoteEndpoint)
    {
        if (!packet.QUERYRESPONSE || packet.answers.Length <= 0 || packet.answers[0].Type != 12)
            return;

        if (packet.answers[0].Data is not PTRRecord ptrRecord) return;

        if (ptrRecord.DomainLabels.Count != 4
            || (!ptrRecord.DomainLabels[0].StartsWith("VRChat-Client") &&
                !ptrRecord.DomainLabels[0].StartsWith("ChilloutVR-GameClient")))
        {
            return;
        }

        if (packet.answers[0].Labels.Count != 3 ||
            packet.answers[0].Labels[0] != "_oscjson" ||
            packet.answers[0].Labels[1] != "_tcp" ||
            packet.answers[0].Labels[2] != "local")
        {
            return;
        }

        var aRecord   = packet.additionals.FirstOrDefault(r => r.Type == 1);
        var srvRecord = packet.additionals.FirstOrDefault(r => r.Type == 33);
        if (aRecord == null || srvRecord == null) return;

        if (aRecord.Data is not ARecord vrChatClientIp || srvRecord.Data is not SRVRecord vrChatClientPort)
            return;

        var hostAddress = vrChatClientIp.Address;

        // VRChat always advertises 127.0.0.1 in the A record. If we heard the
        // response come from a non-loopback peer, use that as the actual address.
        if (IPAddress.IsLoopback(hostAddress) && !_localIpAddresses.Contains(remoteEndpoint.Address))
        {
            hostAddress = remoteEndpoint.Address;
        }

        var newEndpoint = new IPEndPoint(hostAddress, vrChatClientPort.Port);

        // Ignore re-announcements that aren't actually new.
        if (VrchatClientEndpoint != null && VrchatClientEndpoint.Equals(newEndpoint))
            return;

        VrchatClientEndpoint = newEndpoint;
        _logger.LogInformation("VRChat OSCQuery client discovered at {Endpoint} (host label {Label})",
            newEndpoint, ptrRecord.DomainLabels[0]);

        try { OnVrcClientDiscovered(); }
        catch (Exception ex) { _logger.LogError(ex, "OnVrcClientDiscovered handler threw"); }
    }

    private async void Listen(UdpClient client, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var result = await client.ReceiveAsync(ct);

                if (!_localIpAddresses.Any(i => i.Equals(result.RemoteEndPoint.Address))
                    && !IPAddress.IsLoopback(result.RemoteEndPoint.Address))
                {
                    // Don't act on packets from peers we don't recognise as our own machine.
                    continue;
                }

                var reader = new BigReader(result.Buffer);
                var packet = new DnsPacket(reader);

                ResolveVrChatClient(packet, result.RemoteEndPoint);
                ResolveDnsQueries(packet, result.RemoteEndPoint);
            }
            catch (OperationCanceledException) { return; }
            catch (ObjectDisposedException)    { return; }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "mDNS listen iteration failed");
            }
        }
    }

    /// <summary>Advertise a service over mDNS so VRChat (and other peers) can resolve us.</summary>
    public void Advertise(string serviceName, AdvertisedService advertisement)
    {
        _logger.LogInformation("mDNS advertising service {ServiceName} on {Address}:{Port}",
            serviceName, advertisement.Address, advertisement.Port);

        Services[serviceName] = advertisement;
    }
}
