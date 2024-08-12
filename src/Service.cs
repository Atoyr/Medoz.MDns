namespace Medoz.Mdns;

public record Service(
        string Name, 
        string HostName, 
        string IpAddress,
        int Port
        );
