using Companion.Backend.AspNetCore.Middleware.I18n;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.Builder
{
    public static class I18nMiddlewareExtensions
    {
        public static IServiceCollection AddI18n(
            this IServiceCollection services)
        {
            services.AddTransient<I18nMiddleware>();

            return services;
        }

        public static IApplicationBuilder UseI18n(this IApplicationBuilder applicationBuilder, string path)
        {
            applicationBuilder.Map(path, app =>
            {
                app.UseAppContext();

                app.UseMiddleware<I18nMiddleware>();
            });

            return applicationBuilder;
        }
    }
}
