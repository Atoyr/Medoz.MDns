using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Medoz.Mdns;

public class Advertisement
{
    public string ServiceType { init; get; }
    public string ServiceName { init; get; }
    public string HostName { init; get; }
    public IPAddress IpAddress { init; get; }
    public ushort Port { init; get; }
    public ushort TTL { init; get; }
    public IDictionary<string, string> TxtRecords { init; get; }

    public Advertisement(string serviceType, string serviceName, string hostName, IPAddress ipAddress, ushort port, ushort ttl = 120, IDictionary<string, string>? txtRecords = null)
    {
        ServiceType = serviceType;
        ServiceName = serviceName;
        HostName = hostName;
        IpAddress = ipAddress;
        Port = port;
        TTL = ttl;
        TxtRecords = txtRecords ?? new Dictionary<string, string>();
    }

    public byte[] ToBytes()
    {
        var header = new Header(
            0,
            QueryResponse.Response,
            OperationCode.Default,
            true,
            false,
            false,
            false,
            false,
            false,
            0,
            0,
            1,
            0,
            2
            );

        var service = $"{ServiceName}.{ServiceType}";
        var serviceData = Answer.GetHostData(service);
        var target = $"{HostName}";
        var ptr = new Answer(
            ServiceType,
            DnsType.PTR,
            DnsClass.IN.Value,
            TTL,
            0,
            serviceData
            );
        var srv = new Answer(
            service,
            DnsType.SRV,
            DnsClass.IN.Value,
            TTL,
            0,
            Answer.GetSRVData(0, 0, Port, target)
            );
        var a = new Answer(
            target,
            DnsType.A,
            DnsClass.IN.Value,
            TTL,
            0,
            Answer.GetAData(IpAddress)
            );

        var response = new List<byte>();
        response.AddRange(header.ToBytes());
        response.AddRange(ptr.ToBytes());
        response.AddRange(srv.ToBytes());
        response.AddRange(a.ToBytes());

        return response.ToArray();
    }
}