using Xunit;

using Medoz.Mdns;

namespace Medoz.Mdns.test;

public class MdnsServiceTest
{

    [Fact]
    public void EncodeName_Equal()
    {
        MdnsService mdns = new();
        var name = mdns.EncodeName("test.local");

        Assert.NotNull(name);

        Assert.Equal(0x04, name[0]);
        Assert.Equal(0x74, name[1]);
        Assert.Equal(0x65, name[2]);
        Assert.Equal(0x73, name[3]);
        Assert.Equal(0x74, name[4]);
        Assert.Equal(0x05, name[5]);
        Assert.Equal(0x6C, name[6]);
        Assert.Equal(0x6F, name[7]);
        Assert.Equal(0x63, name[8]);
        Assert.Equal(0x61, name[9]);
        Assert.Equal(0x6C, name[10]);
        Assert.Equal(0x00, name[11]);
    }

    [Fact]
    public void BuildMdnsQuery_Test()
    {
        MdnsService mdns = new();
        var query = mdns.BuildMdnsQuery("test.local");

        Assert.NotNull(query);
        // DNS heder
        Assert.Equal(0x00,  query[0]); // ID
        Assert.Equal(0x00,  query[1]); // ID
        Assert.Equal(0x00,  query[2]); // Flags
        Assert.Equal(0x00,  query[3]); // Flags
        Assert.Equal(0x00,  query[4]); // QDCOUNT
        Assert.Equal(0x01,  query[5]); // QDCOUNT
        Assert.Equal(0x00,  query[6]); // ANCOUNT
        Assert.Equal(0x00,  query[7]); // ANCOUNT
        Assert.Equal(0x00,  query[8]); // NSCOUNT
        Assert.Equal(0x00,  query[9]); // NSCOUNT
        Assert.Equal(0x00,  query[10]); // ARCOUNT
        Assert.Equal(0x00,  query[11]); // ARCOUNT

        // name
        Assert.Equal(0x04, query[12]);
        Assert.Equal(0x74, query[13]);
        Assert.Equal(0x65, query[14]);
        Assert.Equal(0x73, query[15]);
        Assert.Equal(0x74, query[16]);
        Assert.Equal(0x05, query[17]);
        Assert.Equal(0x6C, query[18]);
        Assert.Equal(0x6F, query[19]);
        Assert.Equal(0x63, query[20]);
        Assert.Equal(0x61, query[21]);
        Assert.Equal(0x6C, query[22]);
        Assert.Equal(0x00, query[23]);

        // query section
        Assert.Equal(0x00, query[24]); // Type: PTR
        Assert.Equal(0x0C, query[25]); // Type: PTR
        Assert.Equal(0x00, query[26]); // Class: IN
        Assert.Equal(0x01, query[27]); // Class: IN
    }
}