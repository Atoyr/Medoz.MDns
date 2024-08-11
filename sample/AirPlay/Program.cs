using Microsoft.Extensions.Logging;
using Medoz.Mdns;


using ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddConsole());
using var cts = new CancellationTokenSource();

var server = new MdnsServer(factory.CreateLogger<MdnsClient>(), factory.CreateLogger<MdnsServer>());

server.AdvertiseService("_airplay._tcp.local", "MyAirPlayDevice", 7000);

await server.StartAsync(cts.Token);

//var client = new MdnsClient(factory.CreateLogger<MdnsClient>());
//await client.StartAsync(cts.Token);