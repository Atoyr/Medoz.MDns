using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Medoz.Mdns;

public class AnswerEventArgs : EventArgs
{
    public Answer Answer { get; }

    public AnswerEventArgs(Answer answer)
    {
        Answer = answer;
    }
}
