using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Medoz.Mdns;

public static class ServiceCollectionExtension
{
    public static IServiceCollection AddMdnsService(
            this IServiceCollection services
            , Action<MdnsService>? action = null) 
    {
        services.AddSingleton<MdnsService>();
        services.AddSingleton<IHostedService>(sp => {
                var mdns = sp.GetRequiredService<MdnsService>();
                action?.Invoke(mdns);
                return mdns;
                });
        return services;
    }

}