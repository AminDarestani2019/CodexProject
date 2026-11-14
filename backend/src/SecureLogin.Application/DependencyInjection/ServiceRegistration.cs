using Microsoft.Extensions.DependencyInjection;

namespace SecureLogin.Application.DependencyInjection;

public static class ServiceRegistration
{
    public static IServiceCollection AddApplication(this IServiceCollection services) => services;
}
