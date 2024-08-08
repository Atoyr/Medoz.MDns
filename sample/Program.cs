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
                services.AddHostedService<MdnsClient>(); // Register Poller as a hosted service
            });
}