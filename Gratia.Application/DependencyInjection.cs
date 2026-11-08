using Gratia.Application.Behaviors;
using Gratia.Application.Settings;
using Mediator;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Gratia.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
        {
            // Hosts that need these settings at startup add .ValidateOnStart() themselves (see the Worker)
            services.AddOptions<PointsOptions>()
                .Bind(configuration.GetSection(PointsOptions.SectionName))
                .Validate(o => o.MonthlyAllowance > 0, "AppSettings:MonthlyAllowance must be greater than 0.");

            // The Mediator source generator finds every command, query and handler at compile time and
            // generates this registration. Scoped, because handlers use the request's DbContext
            // (through the repositories and IUnitOfWork). The options must be compile-time constants.
            services.AddMediator((MediatorOptions options) =>
            {
                options.ServiceLifetime = ServiceLifetime.Scoped;
                options.PipelineBehaviors = [typeof(LoggingBehavior<,>)];
            });

            return services;
        }
    }
}
