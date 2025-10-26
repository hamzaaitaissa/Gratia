using Gratia.Application.Command;
using Gratia.Application.Interfaces;
using Gratia.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gratia.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplicationService(this IServiceCollection services)
        {   
            services.AddScoped<ICompanyCommandService, CompanyCommandService>();
            services.AddScoped<ICompanyQueryService, CompanyQueryService>();

            return services;
        }
    }
}
