using Companion;
using Companion.Backend;
using Companion.Backend.AspNetCore.Middleware.AppContext;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.Builder
{
    public static class AppContextMiddlewareExtensions
    {
        public static IServiceCollection AddAppContext(this IServiceCollection services)
        {
            services.AddTransient<IAppContextFactory, DefaultAppContextFactory>();
            services.AddTransient<AppContextMiddleware>();

            return services;
        }

        public static IApplicationBuilder UseAppContext(this IApplicationBuilder builder)
        {
            builder.UseMiddleware<AppContextMiddleware>();

            return builder;
        }
    }
}
