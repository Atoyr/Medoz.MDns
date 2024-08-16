using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Medoz.Mdns;

public class DataReceiveEventArgs : EventArgs
{
    public byte[] Buffer { get; }
    public IPEndPoint RemoteEndPoint { get; }

    public DataReceiveEventArgs(byte[] buffer, IPEndPoint remoteEndPoint)
    {
        Buffer = buffer;
        RemoteEndPoint = remoteEndPoint;
    }
}