using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

namespace Medoz.Mdns;

public sealed class MdnsClient : IHostedService, IDisposable
{
    private const int MdnsPort = 5353;
    private const string MdnsAddress = "224.0.0.251";
    private UdpClient udpClient;

    private ILogger<MdnsClient>? _logger;

    private object _lock = new object();
    private bool _isRunning = false;

    public event EventHandler<Response> ResponseReceived;

    public MdnsClient()
    {
        udpClient = new UdpClient();
        udpClient.JoinMulticastGroup(IPAddress.Parse(MdnsAddress));
        udpClient.MulticastLoopback = true;
    }

    public MdnsClient(ILogger<MdnsClient> logger) : this()
    {
        _logger = logger;
    }

    public void SendMdnsQuery(string serviceName)
    {
        var query = BuildMdnsQuery(serviceName);
        var endPoint = new IPEndPoint(IPAddress.Parse(MdnsAddress), MdnsPort);
        udpClient.Send(query, query.Length, endPoint);
        _logger?.LogInformation("mDNS query sent.");
    }

    public Task StartAsync(CancellationToken stoppingToken)
    {
        lock(_lock)
        {
            _isRunning = true;
        }
        ReceiveMdnsResponses(stoppingToken);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken stoppingToken)
    {
        lock(_lock)
        {
            _isRunning = false;
        }
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        lock(_lock)
        {
            _isRunning = false;
        }
        udpClient.Close();
    }

    private void ReceiveMdnsResponses(CancellationToken stoppingToken)
    {
        var endPoint = new IPEndPoint(IPAddress.Any, MdnsPort);
        _logger?.LogInformation("listening for mDNS responses...");
        while (!stoppingToken.IsCancellationRequested && _isRunning)
        {
            byte[] response = udpClient.Receive(ref endPoint);
            var r = ParseMdnsResponse(response);
            _logger?.LogInformation("mDNS response received.", r);
            ResponseReceived?.Invoke(this, r);
        }
    }

    private byte[] BuildMdnsQuery(string serviceName)
    {
        var query = new StringBuilder();
        query.Append(serviceName);
        query.Append("\0\0\x01\0\x01\0\0\0\0\0\0\0");

        return Encoding.ASCII.GetBytes(query.ToString());
    }

    private Response ParseMdnsResponse(byte[] response)
    {
        // DNSヘッダーの解析
        int id = (response[0] << 8) | response[1];
        int flags = (response[2] << 8) | response[3];
        int questionCount = (response[4] << 8) | response[5];
        int answerCount = (response[6] << 8) | response[7];

        Console.WriteLine($"ID: {id}, Flags: {flags}, Questions: {questionCount}, Answers: {answerCount}");

        int offset = 12; // DNSヘッダーは12バイト
        for (int i = 0; i < questionCount; i++)
        {
            offset = SkipQuestion(response, offset);
        }

        List<Answer> answers = new();
        for (int i = 0; i < answerCount; i++)
        {
            answers.Add(ParseAnswer(response, ref offset));
        }

        return new Response(id, flags, questionCount, answerCount, offset, answers);
    }

    private int SkipQuestion(byte[] response, int offset)
    {
        // 質問セクションをスキップ
        while (response[offset] != 0)
        {
            offset += response[offset] + 1;
        }
        offset += 5; // 終端バイト + QTYPE + QCLASS
        return offset;
    }

    private Answer ParseAnswer(byte[] response, ref int offset)
    {
        var name = ReadName(response, ref offset);
        var type = (ushort)((response[offset++] << 8) | response[offset++]);
        var @class = (ushort)((response[offset++] << 8) | response[offset++]);
        var ttl = (uint)((response[offset++] << 24) | (response[offset++] << 16) | (response[offset++] << 8) | response[offset++]);
        var dataLength = (ushort)((response[offset++] << 8) | response[offset++]);
        var data = new byte[dataLength];
        offset += dataLength;
        return new Answer(name, type, @class, ttl, dataLength, data);
    }

    private string ReadName(byte[] message, ref int offset)
    {
        var name = new StringBuilder();
        var length = message[offset++];
        while (length != 0)
        {
            if (name.Length > 0)
            {
                name.Append(".");
            }
            name.Append(Encoding.UTF8.GetString(message, offset, length));
            offset += length;
            length = message[offset++];
        }
        return name.ToString();
    }

    public void AdvertiseService(string serviceName, string hostName, int port)
    {
        var advertisement = BuildMdnsAdvertisement(serviceName, hostName, port);
        var endPoint = new IPEndPoint(IPAddress.Parse(MdnsAddress), MdnsPort);

        udpClient.Send(advertisement, advertisement.Length, endPoint);
        Console.WriteLine("Service advertised.");
    }

    private byte[] BuildMdnsAdvertisement(string serviceName, string hostName, int port)
    {
        var builder = new StringBuilder();
        builder.Append(serviceName);
        builder.Append("\0\0\x21\0\x01\0\0\0\x78\0");
        builder.Append((char)hostName.Length);
        builder.Append(hostName);
        builder.Append("\0\0\x1c\0\x01\0\0\0\x78\0\x04");
        builder.Append((char)(port >> 8));
        builder.Append((char)(port & 0xff));

        return Encoding.ASCII.GetBytes(builder.ToString());
    }
}