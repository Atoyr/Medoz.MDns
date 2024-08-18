using System.Net;

namespace Medoz.Mdns;

public record Answer(
        string Name, 
        DnsType Type, 
        DnsClass Class, 
        uint TTL, 
        ushort DataLength, 
        byte[] Data)
{
    public Answer(string name, ushort type, ushort @class, uint ttl, ushort dataLength, byte[] data) : this(name, (DnsType)type, new DnsClass(@class), ttl, dataLength, data) { }

    public byte[] ToBytes() 
    {
        var bytes = new List<byte>();

        // Nameをbyte[]に変換して追加
        bytes.AddRange(MdnsService.EncodeName(Name));

        // Typeを追加 (2バイト)
        bytes.AddRange(BitConverter.GetBytes((ushort)Type));

        // Classを追加 (2バイト)
        bytes.AddRange(BitConverter.GetBytes(Class.Value));

        // TTLを追加 (4バイト)
        bytes.AddRange(BitConverter.GetBytes(TTL).Reverse());

        // DataLengthを追加 (2バイト)
        bytes.AddRange(BitConverter.GetBytes((ushort)Data.Length));

        // Dataを追加 (可変長)
        if (Data is not null)
        {
            bytes.AddRange(Data);
        }

        return bytes.ToArray();
    }

    public static byte[] GetAData(string ipAddress)
    {
        return GetAData(IPAddress.Parse(ipAddress));
    }

    public static byte[] GetAData(IPAddress ipAddress)
    {
        return ipAddress.GetAddressBytes();
    }

    public static byte[] GetHostData(string hostName)
    {
        return MdnsService.EncodeName(hostName);
    }

    public static byte[] GetSRVData(ushort priority, ushort weight, uint port, string target)
    {
        var bytes = new List<byte>();
        // Priorityを追加
        bytes.AddRange(BitConverter.GetBytes(priority));
        // Weightを追加
        bytes.AddRange(BitConverter.GetBytes(weight));
        // Portを追加
        bytes.AddRange(BitConverter.GetBytes(port));
        // Targetを追加
        bytes.AddRange(MdnsService.EncodeName(target));
        return bytes.ToArray();
    }
}
        