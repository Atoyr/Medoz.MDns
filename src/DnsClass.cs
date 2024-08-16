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

    public static implicit operator DnsClass(ushort value) => new(value);

    public static implicit operator DnsClass(string value) 
    {
        switch(value)
        {
            case "IN":
            return new DnsClass(0x0001);
            case "CS":
            return new DnsClass(0x0002);
            case "CH":
            return new DnsClass(0x0003);
            case "HS":
            return new DnsClass(0x0004);
            case "ANY":
            return new DnsClass(0x00ff);
            default:
            return new DnsClass(0x0001);
        }
    }

    public static DnsClass IN => 0x0001;
    public static DnsClass CS => 0x0002;
    public static DnsClass CH => 0x0003;
    public static DnsClass HS => 0x0004;
    public static DnsClass ANY => 0x00ff;
}