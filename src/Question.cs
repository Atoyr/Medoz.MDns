using System;
using System.Text;

namespace Medoz.Mdns;

public record Question(
        string Name, 
        ushort Type, 
        ushort Class
        )
{
    public override string ToString() => $"Name: {Name} Type: {Type} Class: {Class}";
}
