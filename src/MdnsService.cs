using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

namespace Medoz.Mdns;

public class MdnsService : IHostedService, IDisposable
{
    protected const int MdnsPort = 5353;
    protected const string MdnsAddress = "224.0.0.251";
    protected UdpClient udpClient;

    private ILogger<MdnsService>? _logger;

    protected object Lock = new object();
    protected bool IsRunning = false;

    public event EventHandler<Response> ResponseReceived;
    public event EventHandler<Answer> ServiceDiscovered;

    public MdnsService()
    {
        udpClient = new UdpClient();
        udpClient.JoinMulticastGroup(IPAddress.Parse(MdnsAddress));
        udpClient.MulticastLoopback = true;
        udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, MdnsPort));
    }

    public MdnsService(ILogger<MdnsService> logger) : this()
    {
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken stoppingToken)
    {
        lock(Lock)
        {
            IsRunning = true;
        }
        await ReceiveMdnsResponsesAsync(stoppingToken);
    }

    public Task StopAsync(CancellationToken stoppingToken)
    {
        lock(Lock)
        {
            IsRunning = false;
        }
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        lock(Lock)
        {
            IsRunning = false;
        }
        udpClient.Close();
    }

    public void SendMdnsQuery(string serviceName)
    {
        var query = BuildMdnsQuery(serviceName);
        var endPoint = new IPEndPoint(IPAddress.Parse(MdnsAddress), MdnsPort);
        udpClient.Send(query, query.Length, endPoint);
        _logger?.LogInformation("mDNS query sent.");
    }

    private byte[] BuildMdnsQuery(string serviceName)
    {
        var query = new StringBuilder();
        query.Append(serviceName);
        query.Append("\0\0\x01\0\x01\0\0\0\0\0\0\0");

        return Encoding.ASCII.GetBytes(query.ToString());
    }

    private async Task ReceiveMdnsResponsesAsync(CancellationToken stoppingToken)
    {
        _logger?.LogInformation("listening for mDNS responses...");
        while (!stoppingToken.IsCancellationRequested && IsRunning)
        {
            byte[] buffer;
            try 
            {
                var result = await udpClient.ReceiveAsync(stoppingToken);
                _logger?.LogInformation($"mDNS response received. host: {result.RemoteEndPoint.Address}, port: {result.RemoteEndPoint.Port}, length: {result.Buffer.Length}");
                buffer = result.Buffer;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "mDNS response receive failed.");
                throw;
            }
            var r = ParseMdnsResponse(buffer);
            if (r is null) continue;

            // レスポンス受信時のイベントを発火
            ResponseReceived?.Invoke(this, r);

            // サービス発見時のイベントを発火
            foreach (var record in r.Answers)
            {
                if (record.Type == 12) // PTRレコード
                {
                    _logger?.LogInformation("ServiceDiscovered. Name: {0}, Type: {1}, Class: {2}, TTL: {3}, DataLength: {4}", record.Name, record.Type, record.Class, record.TTL, record.DataLength);
                    _logger?.LogInformation("ServiceDiscovered: Data:", Encoding.UTF8.GetString(record.Data));
                                       
                    ServiceDiscovered?.Invoke(this, record);
                }
            }
        }
        _logger?.LogInformation("mDNS response receive canceled.");
    }

    private Response? ParseMdnsResponse(byte[] response)
    {
        // DNSヘッダーの解析
        int id = (response[0] << 8) | response[1];
        int flags = (response[2] << 8) | response[3];
        int qdCount = (response[4] << 8) | response[5];
        int anCount = (response[6] << 8) | response[7];
        // NSCOUNT
        int nsCount = (response[8] << 8) | response[9];
        // ARCOUNT
        int arCount = (response[10] << 8) | response[11];

        int totalRecords = qdCount + anCount + nsCount + arCount;
        if (response.Length < 12 + (totalRecords * 16)) // 16バイトは一般的なレコードのサイズの推定値
        {
            _logger?.LogInformation("response length is too short.");
            return null;
        }

        bool isTruncated = (flags & 0x0200) != 0;
        _logger?.LogInformation("The message is truncated: {0}", isTruncated);

        _logger?.LogInformation($"ID: {id}, Flags: {flags}, QDCOUNT: {qdCount}, ANCOUNT: {anCount}, NSCOUNT: {nsCount}, ARCOUNT: {arCount}");

        int offset = 12; // DNSヘッダーは12バイト
        for (int i = 0; i < qdCount; i++)
        {
            offset = SkipQuestion(response, offset);
        }

        List<Answer> answers = new();
        for (int i = 0; i < anCount; i++)
        {
            answers.Add(ParseAnswer(response, ref offset));
        }

        return new Response(id, flags, qdCount, anCount, offset, answers);
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
        _logger.LogInformation("Service advertised.");
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