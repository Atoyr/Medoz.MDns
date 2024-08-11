using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Medoz.Mdns;

public class QueryEventArgs : EventArgs
{
    public byte[] Query { get; }
    public IPEndPoint RemoteEndPoint { get; }

    public QueryEventArgs(byte[] query, IPEndPoint remoteEndPoint)
    {
        Query = query;
        RemoteEndPoint = remoteEndPoint;
    }
}