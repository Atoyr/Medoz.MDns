using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Medoz.Mdns;

public class AnswerEventArgs : EventArgs
{
    public ResourceRecord Answer { get; }

    public AnswerEventArgs(ResourceRecord answer)
    {
        Answer = answer;
    }
}