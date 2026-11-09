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
            // The points allowance and reset day are per company now (#19), so they aren't settings
            services.AddOptions<OnboardingOptions>()
                .Bind(configuration.GetSection(OnboardingOptions.SectionName));

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
