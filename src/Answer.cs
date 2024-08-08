namespace Medoz.Mdns;

public record Answer(
        string Name, 
        ushort Type, 
        ushort Class, 
        uint TTL, 
        ushort DataLength, 
        byte[] Data
        );