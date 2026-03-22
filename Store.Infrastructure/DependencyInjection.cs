using Microsoft.Extensions.DependencyInjection;
using Store.Infrastructure.Services;
using Store.Application.Interfaces;
using Store.Application.Services;

namespace Store.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfracstructure(this IServiceCollection services )
    {

        // there inject the product service using Scoped that means this service will be available per request 
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IOrderService, OrderService>();
        return services;
    }
}
