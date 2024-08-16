using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Medoz.Mdns;

public class PacketReceiveEventArgs : DataReceiveEventArgs
{
    public Packet Packet { get; }

    public PacketReceiveEventArgs(byte[] buffer, IPEndPoint remoteEndPoint, Packet packet)
        : base(buffer, remoteEndPoint)
    {
        Packet = packet;
    }
}