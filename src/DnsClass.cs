namespace Medoz.Mdns;

public class DnsClass
{
    public ushort Value { get; set; } = 0x0001;

    public DnsClass(ushort value)
    {
        Value = value;
    }

    public override string ToString()
    {
        return Value switch
        {
            0x0001 => "IN",
            0x0002 => "CS",
            0x0003 => "CH",
            0x0004 => "HS",
            0x00ff => "ANY",
            _ => "QCLASS", 
        };
    }
}