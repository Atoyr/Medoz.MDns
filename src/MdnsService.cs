using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

namespace Medoz.Mdns;

public class MdnsService : IHostedService, IDisposable
{
    private const int MdnsPort = 5353;
    private const string MdnsAddress = "224.0.0.251";
    private UdpClient _udpClient;

    private ILogger<MdnsService>? _logger;

    protected object _lock = new object();
    protected bool _isRunning = false;

    public event EventHandler<ResponseEventArgs> ResponseReceived;
    public event EventHandler<AnswerEventArgs> ServiceDiscovered;
    public event EventHandler<QueryEventArgs> QueryReceived;

    public MdnsService()
    {
        _udpClient = new UdpClient();
        _udpClient.JoinMulticastGroup(IPAddress.Parse(MdnsAddress));
        _udpClient.MulticastLoopback = true;
        _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, MdnsPort));
    }

    public MdnsService(ILogger<MdnsService> logger) : this()
    {
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken stoppingToken)
    {
        lock(_lock)
        {
            _isRunning = true;
        }
        await ReceiveMdnsAsync(stoppingToken);
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
        _udpClient.Close();
    }

    /// <summary>
    /// mDNSクエリを送信します。
    /// </summary>
    public void SendMdnsQuery(string serviceName)
    {
        var query = BuildMdnsQuery(serviceName);
        var endPoint = new IPEndPoint(IPAddress.Parse(MdnsAddress), MdnsPort);
        _udpClient.Send(query, query.Length, endPoint);
        _logger?.LogInformation("mDNS query sent.");
    }

    
    /// <summary>
    /// mDNSクエリを生成します。
    /// </summary>
    private byte[] BuildMdnsQuery(string serviceName)
    {
        var query = new StringBuilder();
        query.Append(serviceName);
        query.Append("\0\0\x01\0\x01\0\0\0\0\0\0\0");

        return Encoding.ASCII.GetBytes(query.ToString());
    }

    /// <summary>
    /// mDNSの受信を開始します。
    /// </summary>
    private async Task ReceiveMdnsAsync(CancellationToken stoppingToken)
    {
        _logger?.LogInformation("listening for mDNS request and response...");
        while (!stoppingToken.IsCancellationRequested && _isRunning)
        {
            byte[] buffer;
            IPEndPoint? remoteEndPoint;
            try 
            {
                var result = await _udpClient.ReceiveAsync(stoppingToken);
                _logger?.LogInformation($"mDNS response received.");
                _logger?.LogDebug($"mDNS response received. host: {result.RemoteEndPoint.Address}, port: {result.RemoteEndPoint.Port}, length: {result.Buffer.Length}");
                buffer = result.Buffer;
                remoteEndPoint = result.RemoteEndPoint;
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

            HandleIncomingPacket(buffer, remoteEndPoint);
            if (buffer.Length == 0)
            {
                _logger?.LogWarning("response length is 0.");
            }

            var packet = ParsePacket(buffer);
            if (packet is null) continue;

            // レスポンス受信時のイベントを発火
            OnResponseReceived(new ResponseEventArgs(packet));

            // サービス発見時のイベントを発火
            foreach (var record in packet.Answers)
            {
                if (record.Type == 12) // PTRレコード
                {
                    _logger?.LogInformation($"ServiceDiscovered. Name: {record.Name}");
                    _logger?.LogDebug($"ServiceDiscovered. Name: {record.Name}, Type: {record.Type}, Class: {record.Class}, TTL: {record.TTL}, DataLength: {record.DataLength}, Data: {Encoding.UTF8.GetString(record.Data)}");
                                       
                    OnServiceDiscovered(new AnswerEventArgs(record));
                }
            }
        }
        _logger?.LogInformation("mDNS response receive canceled.");
    }

    private void HandleIncomingPacket(byte[] packet, IPEndPoint remoteEndPoint)
    {
        // パケットを文字列に変換して内容を表示
        string receivedData = Encoding.ASCII.GetString(packet);
        _logger?.LogDebug($"Received packet from {remoteEndPoint}: {receivedData}");
    }

    private Packet? ParsePacket(byte[] response)
    {
        if (response.Length < 12)
        {
            _logger?.LogWarning("The message is too short");
            return null;
        }

        var header = Header.Parse(response[..12]);
        _logger?.LogDebug($"response header parsed. {header}");

        int offset = 12; // DNSヘッダーは12バイト
        List<Question> questions = new();
        for (int i = 0; i < header.QdCount; i++)
        {
            questions.Add(ParseQuestion(response, ref offset));
            _logger?.LogDebug($"Parse question. {questions[^1]}");
        }

        List<Answer> answers = new();
        for (int i = 0; i < header.AnCount; i++)
        {
            answers.Add(ParseAnswer(response, ref offset));
            _logger?.LogDebug($"Parse answer. {answers[^1]}");
        }

        return new Packet(header, questions, answers);
    }

    private Question ParseQuestion(byte[] span, ref int offset)
    {
        if (span.Length < 5)
        {
            throw new ArgumentException("Invalid Question");
        }

        var name = ReadName(span, ref offset);
        var type = (ushort)(span[offset] << 8 | span[offset + 1]);
        var cls = (ushort)(span[offset + 2] << 8 | span[offset + 3]);
        offset = offset + 4;
        return new Question(name, type, cls);
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

        _udpClient.Send(advertisement, advertisement.Length, endPoint);
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

    protected virtual void OnResponseReceived(ResponseEventArgs e)
    {
        ResponseReceived?.Invoke(this, e);
    }

    protected virtual void OnServiceDiscovered(AnswerEventArgs e)
    {
        ServiceDiscovered?.Invoke(this, e);
    }

    protected virtual void OnQueryReceived(QueryEventArgs e)
    {
        QueryReceived?.Invoke(this, e);
    }

}