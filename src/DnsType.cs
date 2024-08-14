namespace Medoz.Mdns;

public enum DnsType: ushort
{
    A = 0x0001,
    AAAA = 0x001c,
    CNAME = 0x0005,
    MX = 0x000f,
    NS = 0x0002,
    PTR = 0x000c,
    SRV = 0x0021,
    TXT = 0x0010, 
    SOA = 0x0006,
    NAPTR = 0x0023,
    SPF = 0x0063,
    CAA = 0x0101,
    ANY = 0x00ff, 
    OPT = 0x0029, 
    AXFR = 0x00fc,
    IXFR = 0x00fd, 
    TLSA = 0x0034,
}