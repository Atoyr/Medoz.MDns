namespace Medoz.Mdns;

public record Answer(
        string Name, 
        DnsType Type, 
        ushort Class, 
        uint TTL, 
        ushort DataLength, 
        byte[] Data)
{
    public Answer(string name, ushort type, ushort @class, uint ttl, ushort dataLength, byte[] data) : this(name, (DnsType)type, @class, ttl, dataLength, data) { }

}
        