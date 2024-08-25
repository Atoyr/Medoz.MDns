using System.Net;

namespace Medoz.Mdns;

public static class BitConverterHelper
{
    public static byte[] GetBytesWithBigEdianness(uint value)
    {
        return BitConverter.IsLittleEndian ? BitConverter.GetBytes(value).Reverse().ToArray() : BitConverter.GetBytes(value);
    }

    public static byte[] GetBytesWithBigEdianness(ushort value)
    {
        return BitConverter.IsLittleEndian ? BitConverter.GetBytes(value).Reverse().ToArray() : BitConverter.GetBytes(value);
    }
}