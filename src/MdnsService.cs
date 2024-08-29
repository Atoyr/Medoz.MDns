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
    private Timer _advertisementTimer;

    private ILogger<MdnsService>? _logger;

    protected object _lock = new object();
    protected bool _isRunning = false;

    public event EventHandler<DataReceiveEventArgs>? OnDataReceived;
    public event EventHandler<PacketReceiveEventArgs>? OnPacketReceiving;
    public event EventHandler<PacketReceiveEventArgs>? OnPacketReceived;

    public event EventHandler<PacketReceiveEventArgs>? OnResponseReceived;
    public event EventHandler<PacketReceiveEventArgs>? OnQueryReceived;

    public string IpAddress { get; private set; } = "127.0.0.1";
    public string HostName { get; private set; }

    private List<Advertisement> Advertisements { get; } = new List<Advertisement>();

    public MdnsService()
    {
        _udpClient = new UdpClient();
        HostName = Dns.GetHostName();
        var adrList = Dns.GetHostAddresses(HostName);
        if (adrList.Length > 0)
        {
            IpAddress = adrList[0].ToString();
        }
    }

    public MdnsService(ILogger<MdnsService> logger) : this()
    {
        _logger = logger;
    }

    public void SetIpAddress(string ipAddress)
    {
        lock(_lock)
        {
            if (_isRunning)
            {
                throw new InvalidOperationException("Cannot change IP address while running.");
            }
            IpAddress = ipAddress;
        }
    }

    public async Task StartAsync(CancellationToken stoppingToken)
    {
        if (_isRunning)
        {
            return;
        }

        if (_udpClient.Client is null)
        {
            _udpClient = new UdpClient();
        }

        while(_udpClient.Client.IsBound && !stoppingToken.IsCancellationRequested)
        {
            _logger?.LogWarning($"UpdClient is already active. Wait for 1 second....");
            await Task.Delay(1000);
        }

        lock(_lock)
        {
            _udpClient.JoinMulticastGroup(IPAddress.Parse(MdnsAddress));
            _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _udpClient.MulticastLoopback = true;
            _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, MdnsPort));

            _advertisementTimer = new (AdvertisementTimerCallback, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));

            _isRunning = true;
        }
        await ReceiveMdnsAsync(stoppingToken);
    }

    public Task StopAsync(CancellationToken stoppingToken)
    {
        lock(_lock)
        {
            _advertisementTimer.Change(Timeout.Infinite, Timeout.Infinite);
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
        lock(_lock)
        {
            if (_isRunning == false)
            {
                _logger?.LogDebug("mDNS is not running. Start UdpClient.");
                _udpClient.JoinMulticastGroup(IPAddress.Parse(MdnsAddress));
                _udpClient.MulticastLoopback = true;
                _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, MdnsPort));
            }
        }

        var query = BuildMdnsQuery(serviceName);
        var endPoint = new IPEndPoint(IPAddress.Parse(MdnsAddress), MdnsPort);
        _udpClient.Send(query, query.Length, endPoint);
        _logger?.LogDebug($"mDNS query sent. serviceName: {serviceName}");

        lock(_lock)
        {
            if (_isRunning == false)
            {
                _udpClient.Close();
                _udpClient.Dispose();
            }
        }
    }

    /// <summary>
    /// mDNSクエリを生成します。
    /// </summary>
    internal byte[] BuildMdnsQuery(string serviceName, DnsType type = DnsType.PTR, DnsClass? cls = null)
    {
        var query = new List<byte>();

        // DNS Header
        query.AddRange(new byte[] { 0x00, 0x00 }); // ID
        query.AddRange(new byte[] { 0x00, 0x00 }); // Flags: Standard query, no recursion
        query.AddRange(new byte[] { 0x00, 0x01 }); // QDCOUNT (1 question)
        query.AddRange(new byte[] { 0x00, 0x00 }); // ANCOUNT (0 answers)
        query.AddRange(new byte[] { 0x00, 0x00 }); // NSCOUNT (0 authority records)
        query.AddRange(new byte[] { 0x00, 0x00 }); // ARCOUNT (0 additional records)

        cls ??= DnsClass.IN;

        // Question Section
        query.AddRange(EncodeName(serviceName));   // Name: _airplay._tcp.local.
        query.AddRange(BitConverter.GetBytes((ushort)type).Reverse()); // Type: PTR (type=12)
        query.AddRange(BitConverter.GetBytes(cls.Value).Reverse()); // Class: IN (class=1)

        return query.ToArray();
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

        List<ResourceRecord> answers = new();
        for (int i = 0; i < header.AnCount; i++)
        {
            var tempOffset = offset;
            try
            {
                answers.Add(ParseAnswer(response, ref offset));
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Parse answer failed. data: {BitConverter.ToString(response[tempOffset..offset])}");
                continue;
            }
            _logger?.LogDebug($"Parse answer. {answers[^1]}");
        }

        return new Packet(header, questions, answers);
    }

    internal static Question ParseQuestion(byte[] span, ref int offset)
    {
        if (span.Length < 5)
        {
            throw new ArgumentException("Invalid Question");
        }

        var name = DecodeName(span, ref offset);
        var type = (ushort)(span[offset] << 8 | span[offset + 1]);
        var cls = (ushort)(span[offset + 2] << 8 | span[offset + 3]);
        offset = offset + 4;
        return new Question(name, type, cls);
    }

    internal static ResourceRecord ParseAnswer(byte[] response, ref int offset)
    {
        var name = DecodeName(response, ref offset);
        var type = (ushort)((response[offset++] << 8) | response[offset++]);
        var @class = (ushort)((response[offset++] << 8) | response[offset++]);
        var ttl = (uint)((response[offset++] << 24) | (response[offset++] << 16) | (response[offset++] << 8) | response[offset++]);
        var dataLength = (ushort)((response[offset++] << 8) | response[offset++]);
        var data = response[offset..(offset + dataLength)];
        offset += dataLength;
        return new ResourceRecord(name, type, @class, ttl, data);
    }

    /// <summary>
    /// 名前をDNS形式にエンコードします。
    /// </summary>
    internal static byte[] EncodeName(string name)
    {
        if (name.EndsWith(".")) name = name.Substring(0, name.Length - 1); // remove trailing dot (if present
        var parts = name.Split('.');
        var result = new List<byte>();

        foreach (var part in parts)
        {
            result.Add((byte)part.Length);
            result.AddRange(Encoding.ASCII.GetBytes(part));
        }
        result.Add(0); // Null byte at the end

        return result.ToArray();
    }

    /// <summary>
    /// DNS形式の名前をデコードします。
    /// </summary>
    internal static string DecodeName(byte[] message, ref int offset)
    {
        StringBuilder name = new StringBuilder();
        bool jumped = false;
        int originalOffset = offset;

        while (true)
        {
            byte length = message[offset];

            // Check if this is a pointer (first two bits are '11')
            if ((length & 0xC0) == 0xC0)
            {
                if (!jumped)
                {
                    originalOffset = offset + 2; // save for later
                }
                int pointer = ((length & 0x3F) << 8) | message[offset + 1];
                offset = pointer;
                jumped = true;
            }
            else if (length == 0)
            {
                offset++;
                break;
            }
            else
            {
                offset++;
                name.Append(Encoding.ASCII.GetString(message, offset, length));
                offset += length;
                name.Append(".");
            }
        }

        if (jumped)
        {
            offset = originalOffset; // restore the original offset
        }

        return name.ToString().TrimEnd('.');
    }

    // TODO 60秒ごとに再送する
    /// <summary>
    /// サービスを広告します。
    /// </summary>
    public void AdvertiseService(string serviceType, string serviceName, int port, string hostName = "", int ttl = 120)
    {
        if (string.IsNullOrEmpty(hostName))
        {
            hostName = HostName;
        }
        var advertisement = new Advertisement(serviceType, serviceName, hostName, IPAddress.Parse(IpAddress), (ushort)port, (ushort)ttl);
        Advertisements.Add(advertisement);
        _logger?.LogDebug($"register advertisement. {BitConverter.ToString(advertisement.ToBytes())}");
        _logger?.LogInformation($"Service {serviceName} register advertisement.");
    }

    private void AdvertisementTimerCallback(object args)
    {
        _logger?.LogDebug($"Service advertised with Timer.");
        var endPoint = new IPEndPoint(IPAddress.Parse(MdnsAddress), MdnsPort);
        foreach(var advertisement in Advertisements)
        {
            var data = advertisement.ToBytes();
            _udpClient.Send(data, data.Length, endPoint);
            _logger?.LogDebug($"Service advertised with Timer. {advertisement}");
        }
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