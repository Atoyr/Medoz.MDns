using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Medoz.MDns;

public class MDnsService
{
    private const int MDnsPort = 5353;
    private const string MDnsMulticastAddress = "224.0.0.251";
    private UdpClient udpClient;

    public MDnsService()
    {
        udpClient = new UdpClient();
        udpClient.JoinMulticastGroup(IPAddress.Parse(MDnsMulticastAddress));
        udpClient.MulticastLoopback = true;
    }

    public void SendMDnsQuery(string hostname)
    {
        var query = CreateMDnsQuery(hostname);
        var multicastEndpoint = new IPEndPoint(IPAddress.Parse(MDnsMulticastAddress), MDnsPort);
        udpClient.Send(query, query.Length, multicastEndpoint);
    }

    private byte[] CreateMDnsQuery(string hostname)
    {
        // mDNSクエリの基本的なフォーマットを構築
        var message = new byte[512];
        var pos = 0;

        // IDフィールド（0）
        message[pos++] = 0;
        message[pos++] = 0;

        // フラグフィールド（標準クエリ）
        message[pos++] = 0;
        message[pos++] = 0;

        // 質問数（1つ）
        message[pos++] = 0;
        message[pos++] = 1;

        // 応答数、権威、追加数（0）
        message[pos++] = 0;
        message[pos++] = 0;
        message[pos++] = 0;
        message[pos++] = 0;
        message[pos++] = 0;
        message[pos++] = 0;

        // ホスト名をラベルに分割して追加
        var labels = hostname.Split('.');
        foreach (var label in labels)
        {
            var labelBytes = Encoding.UTF8.GetBytes(label);
            message[pos++] = (byte)labelBytes.Length;
            labelBytes.CopyTo(message, pos);
            pos += labelBytes.Length;
        }

        // 終端
        message[pos++] = 0;

        // タイプA（IPv4アドレス）
        message[pos++] = 0;
        message[pos++] = 1;

        // クラスIN（インターネット）
        message[pos++] = 0;
        message[pos++] = 1;

        // 生成されたバイト配列を返す
        var query = new byte[pos];
        Array.Copy(message, query, pos);
        return query;
    }

    public void ListenForResponses()
    {
        try
        {
            // udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, MDnsPort));
            udpClient.Client.Bind(new IPEndPoint(IPAddress.Parse("127.0.0.1"), MDnsPort));
            Console.WriteLine("mDNS応答を待機しています...");
        } 
        catch (Exception ex)
        {
            Console.WriteLine("エラー: " + ex.Message);
            throw;
        }

        while (true)
        {
            var remoteEndpoint = new IPEndPoint(IPAddress.Any, 0);
            var response = udpClient.Receive(ref remoteEndpoint);
            Console.WriteLine("mDNS応答を受信しました: " + Encoding.UTF8.GetString(response));
            // 受信した応答を解析するためのコードを追加
        }
    }
}
