using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Medoz.Mdns;

public class ResponseEventArgs : EventArgs
{
    public Packet Response { get; }

    public ResponseEventArgs(Packet response)
    {
        Response = response;
    }
}