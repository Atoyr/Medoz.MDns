using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Medoz.Mdns;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddMdnsService(mdns => {
                mdns.OnQueryReceived += (sender, args) => {
                    var len = args.Packet.Header.QdCount + args.Packet.Header.AnCount + args.Packet.Header.NsCount + args.Packet.Header.ArCount + 12;
                    Console.WriteLine($"!!! Query received !!! {len} bytes from {args.RemoteEndPoint}");
                };
                });
    });
host.Build().Run();

//var client = new MdnsClient(factory.CreateLogger<MdnsClient>());
//await client.StartAsync(cts.Token);
//
