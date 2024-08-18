using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Medoz.Mdns;

class Program
{
    static void Main(string[] args)
    {
        CreateHostBuilder(args).Build().Run();
    }

    static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                // Register other services
                services.AddMdnsService(mdns => {
                        mdns.SetIpAddress("192.168.0.1");
                        // mdns.SendMdnsQuery("test.local");
                        mdns.AdvertiseService("_airplay._tcp.local.", "myService", "aaa", (ushort)7000);
                        mdns.OnQueryReceived += (sender, args) => {
                            var len = args.Packet.Header.QdCount + args.Packet.Header.AnCount + args.Packet.Header.NsCount + args.Packet.Header.ArCount + 12;
                            Console.WriteLine($"!!! Query received !!! {len} bytes from {args.RemoteEndPoint}");
                        };
                        });
            });
}