using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Amanet.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssembly(ApplicationAssemblyReference.Assembly);
        });
        services.AddValidatorsFromAssembly(ApplicationAssemblyReference.Assembly);
        return services;
    }
}
