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

    public event EventHandler<DataReceiveEventArgs>? OnDataReceived;
    public event EventHandler<PacketReceiveEventArgs>? OnPacketReceiving;
    public event EventHandler<PacketReceiveEventArgs>? OnPacketReceived;

    public event EventHandler<PacketReceiveEventArgs>? OnResponseReceived;
    public event EventHandler<PacketReceiveEventArgs>? OnQueryReceived;

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
        _logger?.LogDebug($"mDNS query sent. serviceName: {serviceName}");
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
                HandleDataReceived(buffer, remoteEndPoint);
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

            if (buffer.Length == 0)
            {
                _logger?.LogWarning("response length is 0.");
            }

            var packet = ParsePacket(buffer);
            if (packet is null) continue;
            HandlePacketReceiving(buffer, remoteEndPoint, packet);

            switch (packet.Header.QueryResponse)
            {
                case QueryResponse.Request:
                    HandleQueryReceived(buffer, remoteEndPoint, packet);
                    break;
                case QueryResponse.Response:
                    HandleResponseReceived(buffer, remoteEndPoint, packet);
                    break;
                default:
                    _logger?.LogWarning("Unknown QueryResponse.");
                    break;
            }

            HandlePacketReceived(buffer, remoteEndPoint, packet);
        }
        _logger?.LogInformation("mDNS response receive canceled.");
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
        var data = response[offset..(offset + dataLength)];
        offset += dataLength;
        return new Answer(name, type, @class, ttl, dataLength, data);
    }

    /// <summary>
    /// メッセージから名前を生成します。
    /// </summary>
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

    /// <summary>
    /// サービスを広告します。
    /// </summary>
    public void AdvertiseService(string serviceName, string hostName, int port)
    {
        var advertisement = BuildMdnsAdvertisement(serviceName, hostName, port);
        var endPoint = new IPEndPoint(IPAddress.Parse(MdnsAddress), MdnsPort);

        _udpClient.Send(advertisement, advertisement.Length, endPoint);
        _logger?.LogInformation("Service advertised.");
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

    protected virtual void HandleDataReceived(byte[] buffer, IPEndPoint remoteEndPoint)
    {
        _logger?.LogDebug($"Receiving packet from {remoteEndPoint}: {Encoding.ASCII.GetString(buffer)}");
        OnDataReceived?.Invoke(this, new DataReceiveEventArgs(buffer, remoteEndPoint));
    }

    protected virtual void HandlePacketReceiving(byte[] buffer, IPEndPoint remoteEndPoint, Packet packet)
    {
        _logger?.LogDebug($"Receiving packet from {remoteEndPoint}: {Encoding.ASCII.GetString(buffer)}");
        OnPacketReceiving?.Invoke(this, new PacketReceiveEventArgs(buffer, remoteEndPoint, packet));
    }

    protected virtual void HandlePacketReceived(byte[] buffer, IPEndPoint remoteEndPoint, Packet packet)
    {
        _logger?.LogDebug($"Receiving packet from {remoteEndPoint}: {Encoding.ASCII.GetString(buffer)}");
        OnPacketReceived?.Invoke(this, new PacketReceiveEventArgs(buffer, remoteEndPoint, packet));
    }

    protected virtual void HandleResponseReceived(byte[] buffer, IPEndPoint remoteEndPoint, Packet packet)
    {
        _logger?.LogDebug($"Receiving packet from {remoteEndPoint}: {Encoding.ASCII.GetString(buffer)}");
        OnResponseReceived?.Invoke(this, new PacketReceiveEventArgs(buffer, remoteEndPoint, packet));
    }

    protected virtual void HandleQueryReceived(byte[] buffer, IPEndPoint remoteEndPoint, Packet packet)
    {
        _logger?.LogDebug($"Receiving packet from {remoteEndPoint}: {Encoding.ASCII.GetString(buffer)}");
        OnQueryReceived?.Invoke(this, new PacketReceiveEventArgs(buffer, remoteEndPoint, packet));
    }

}