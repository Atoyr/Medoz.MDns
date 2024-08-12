namespace Medoz.Mdns;

public record Header(
        QueryResponse QueryResponse,
        OperationCode OperationCode,
        bool TruncatedMessage, 
        bool RecursionDesired,
        bool RecursionAvailable,
        bool AuthenticatedData,
        bool CheckingDisabled,
        int ResponseCode
        )
{
    public static Header Parse(byte[] data)
    {
        if (data.Length > 2)
        {
            throw new Exception("Header data is too long");
        }

        var flags = data[0];
        var queryResponse = (QueryResponse)((flags & 0b10000000) >> 7);
        var operationCode = (OperationCode)((flags & 0b01111000) >> 3);
        var truncatedMessage = (flags & 0b00000100) != 0;
        var recursionDesired = (flags & 0b00000010) != 0;
        var recursionAvailable = (flags & 0b00000001) != 0;
        var flags2 = data[1];
        var authenticatedData = (flags2 & 0b10000000) != 0;
        var checkingDisabled = (flags2 & 0b01000000) != 0;
        var responseCode = flags2 & 0b00001111;
        return new Header(
                queryResponse, 
                operationCode, 
                truncatedMessage, 
                recursionDesired, 
                recursionAvailable, 
                authenticatedData, 
                checkingDisabled, 
                responseCode);
    }

    public override string ToString()
    {
        return $"QueryResponse: {QueryResponse}, OperationCode: {OperationCode}, TruncatedMessage: {TruncatedMessage}, RecursionDesired: {RecursionDesired}, RecursionAvailable: {RecursionAvailable}, AuthenticatedData: {AuthenticatedData}, CheckingDisabled: {CheckingDisabled}, ResponseCode: {ResponseCode}";
    }

}