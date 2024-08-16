using Xunit;

using Medoz.Mdns;

namespace Medoz.Mdns.test;

public class HeaderTest
{

    [Fact]
    public void Header_Equal()
    {
        byte[] sampleHeader = new byte[]
        {
            0x12, 0x34, // ID: 0x1234
            0x01, 0x00, // Flags: 0b00000001 00000000 (QR=0, Opcode=0, AA=0, TC=0, RD=1, RA=0, Z=0, RCODE=0)
            0x00, 0x01, // QDCOUNT: 1
            0x00, 0x00, // ANCOUNT: 0
            0x00, 0x00, // NSCOUNT: 0
            0x00, 0x00  // ARCOUNT: 0
        };

        Header h = Header.Parse(sampleHeader);

        Assert.Equal(0x1234, h.Id);
        Assert.Equal(QueryResponse.Request, h.QueryResponse);
        Assert.Equal(OperationCode.Default, h.OperationCode);
        Assert.False(h.TruncatedMessage);
        Assert.True(h.RecursionDesired);
        Assert.False(h.RecursionAvailable);
        Assert.False(h.AuthenticatedData);
        Assert.False(h.CheckingDisabled);
        Assert.Equal(0, h.ResponseCode);
        Assert.Equal(1, h.QdCount);
        Assert.Equal(0, h.AnCount);
        Assert.Equal(0, h.NsCount);
        Assert.Equal(0, h.ArCount);

        Assert.Equal(sampleHeader, h.ToBytes());
    }
}