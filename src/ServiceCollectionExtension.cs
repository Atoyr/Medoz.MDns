using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Medoz.Mdns;

public static class ServiceCollectionExtension
{
    public static IServiceCollection AddMdnsService(
            this IServiceCollection services
            , Action<Response>? responseReceived = null
            , Action<Answer>? serviceDiscovered = null)
    {
        services.AddSingleton<MdnsService>();
        services.AddSingleton<IHostedService>(sp => {
                var mdns = sp.GetRequiredService<MdnsService>();
                if (responseReceived is not null)
                {
                    mdns.ResponseReceived += (sender, response) => responseReceived(response);
                }
                if (serviceDiscovered is not null)
                {
                    mdns.ServiceDiscovered += (sender, answer) => serviceDiscovered(answer);
                }
                return mdns;
                });
        return services;
    }

}