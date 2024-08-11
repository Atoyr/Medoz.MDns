using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Medoz.Mdns;

public class ResponseEventArgs : EventArgs
{
    public Response Response { get; }

    public ResponseEventArgs(Response response)
    {
        Response = response;
    }
}