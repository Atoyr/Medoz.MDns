using System;
using System.Text;

namespace Medoz.Mdns;

public record Question(
        string Name, 
        DnsType Type, 
        ushort Class
        )
{
    public override string ToString() => $"Name: {Name} Type: {Type} Class: {Class}";
    public Question(string name, ushort type, ushort @class) : this(name, (DnsType)type, @class) { }
}