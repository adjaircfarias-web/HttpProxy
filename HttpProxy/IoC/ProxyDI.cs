using Microsoft.Extensions.DependencyInjection;
using HttpProxy.Interface;
using HttpProxy.Proxy;

namespace HttpProxy.IoC;

public static class ProxyDI
{
    public static void AddHttpProxyConfiguration(this IServiceCollection services)
    {
        services.AddScoped(typeof(IProxyFabric<>), typeof(ProxyFabric<>));
        services.AddHttpClient();
    }
}
