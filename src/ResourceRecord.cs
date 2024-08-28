using System.Net;

namespace Medoz.Mdns;

// TODO ResourceRecordに変更する
public record ResourceRecord(
        string Name, 
        DnsType Type, 
        DnsClass Class, 
        uint TTL, 
        byte[] Data)
{
    public ushort DataLength => (ushort)Data.Length;
    public ResourceRecord(string name, ushort type, ushort @class, uint ttl, byte[] data) : this(name, (DnsType)type, new DnsClass(@class), ttl, data) { }

    public byte[] ToBytes() 
    {
        var bytes = new List<byte>();

        // Nameをbyte[]に変換して追加
        bytes.AddRange(MdnsService.EncodeName(Name));

        // Typeを追加 (2バイト)
        bytes.AddRange(BitConverterHelper.GetBytesWithBigEdianness((ushort)Type));

        // Classを追加 (2バイト)
        bytes.AddRange(BitConverterHelper.GetBytesWithBigEdianness(Class.Value));

        // TTLを追加 (4バイト)
        bytes.AddRange(BitConverterHelper.GetBytesWithBigEdianness(TTL));

        // DataLengthを追加 (2バイト)
        bytes.AddRange(BitConverterHelper.GetBytesWithBigEdianness(DataLength));

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
        bytes.AddRange(BitConverterHelper.GetBytesWithBigEdianness(priority));
        // Weightを追加
        bytes.AddRange(BitConverterHelper.GetBytesWithBigEdianness(weight));
        // Portを追加
        bytes.AddRange(BitConverterHelper.GetBytesWithBigEdianness((ushort)port));
        // Targetを追加
        bytes.AddRange(MdnsService.EncodeName(target));
        return bytes.ToArray();
    }
}
        