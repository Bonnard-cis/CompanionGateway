using Companion.Backend.AspNetCore.Middleware;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.Builder
{
    public static class WorkflowMiddlewareExtensions
    {
        public static IServiceCollection AddWorkflow(
            this IServiceCollection services)
        {
            services.AddTransient<WorkflowMiddleware>();

            return services;
        }


        public static IApplicationBuilder UseWorkflow(
            this IApplicationBuilder applicationBuilder,
            string path)
        {
            applicationBuilder.Map(path, app =>
            {
                app.UseAppContext();

                app.UseMiddleware<WorkflowMiddleware>();
            });

            return applicationBuilder;
        }
    }
}
