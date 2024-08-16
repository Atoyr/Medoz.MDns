namespace Medoz.Mdns;

public record Header(
        int Id, 
        QueryResponse QueryResponse,
        OperationCode OperationCode,
        bool AuthoritativeAnswer, 
        bool TruncatedMessage, 
        bool RecursionDesired,
        bool RecursionAvailable,
        bool AuthenticatedData,
        bool CheckingDisabled,
        int ResponseCode, 
        int QdCount, 
        int AnCount, 
        int NsCount, 
        int ArCount
        )
{
    public byte[] ToBytes()
    {
        var data = new byte[12];
        data[0] = (byte)((Id & 0xFF00) >> 8);
        data[1] = (byte)(Id & 0x00FF);
        data[2] = (byte)(
                ((ushort)QueryResponse << 7) |
                ((ushort)OperationCode << 3) |
                (AuthoritativeAnswer ? 0b00000100 : 0) |
                (TruncatedMessage ? 0b00000010 : 0) |
                (RecursionDesired ? 0b00000001 : 0) 
                );
        data[3] = (byte)(
                (RecursionAvailable ? 0b01000000 : 0) |
                (AuthenticatedData ? 0b00100000 : 0) |
                (CheckingDisabled ? 0b00010000 : 0) |
                ResponseCode
                );
        data[4] = (byte)((QdCount & 0xFF00) >> 8);
        data[5] = (byte)(QdCount & 0x00FF);
        data[6] = (byte)((AnCount & 0xFF00) >> 8);
        data[7] = (byte)(AnCount & 0x00FF);
        data[8] = (byte)((NsCount & 0xFF00) >> 8);
        data[9] = (byte)(NsCount & 0x00FF);
        data[10] = (byte)((ArCount & 0xFF00) >> 8);
        data[11] = (byte)(ArCount & 0x00FF);
        return data;
    }

    public static Header Parse(byte[] data)
    {
        if (data.Length > 12)
        {
            throw new Exception("Header data is too long");
        }
        else if (data.Length < 12)
        {
            throw new Exception("Header data is too short");
        }

        var id = (data[0] << 8) | data[1];
        var flags = data[2];
        var queryResponse = (QueryResponse)((flags & 0b10000000) >> 7);
        var operationCode = (OperationCode)((flags & 0b01111000) >> 3);
        var authoritativeAnswer = (flags & 0b00000100) != 0;
        var truncatedMessage = (flags & 0b00000010) != 0;
        var recursionDesired = (flags & 0b00000001) != 0;
        var flags2 = data[3];
        var recursionAvailable = (flags2 & 0b10000000) != 0;
        var authenticatedData = (flags2 & 0b00100000) != 0;
        var checkingDisabled = (flags2 & 0b00010000) != 0;
        var responseCode = flags2 & 0b00001111;

        var qdCount = (data[4] << 8) | data[5];
        var anCount = (data[6] << 8) | data[7];
        var nsCount = (data[8] << 8) | data[9];
        var arCount = (data[10] << 8) | data[11];

        return new Header(
                id, 
                queryResponse, 
                operationCode, 
                authoritativeAnswer, 
                truncatedMessage, 
                recursionDesired, 
                recursionAvailable, 
                authenticatedData, 
                checkingDisabled, 
                responseCode, 
                qdCount, 
                anCount, 
                nsCount, 
                arCount
                );
    }

    public override string ToString()
    {
        return $"Id: {Id}, QueryResponse: {QueryResponse}, OperationCode: {OperationCode}, AuthoritativeAnswer: {AuthoritativeAnswer}, TruncatedMessage: {TruncatedMessage}, RecursionDesired: {RecursionDesired}, RecursionAvailable: {RecursionAvailable}, AuthenticatedData: {AuthenticatedData}, CheckingDisabled: {CheckingDisabled}, ResponseCode: {ResponseCode}, QdCount: {QdCount}, AnCount: {AnCount}, NsCount: {NsCount}, ArCount: {ArCount}";
    }

}