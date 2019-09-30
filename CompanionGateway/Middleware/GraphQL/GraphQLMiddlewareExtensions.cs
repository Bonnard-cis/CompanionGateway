using System.Linq;
using System.Reflection;
using Companion.Backend.AspNetCore.Middleware.GraphQL;
using Companion.Backend.GraphQL;
using Companion.Backend.Schema;
using Companion.DataLoader;
using GraphQL;
using GraphQL.Types;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.AspNetCore.Builder
{
    public static class GraphQLMiddlewareExtensions
    {
        public static IServiceCollection AddGraphQL<TSchema>(
            this IServiceCollection services)
            where TSchema : class, ISchema
        {
            services.AddSingleton<ISchema, TSchema>();
            services.AddTransient<GraphQLMiddleware>();
            services.AddTransient<IGraphQLEndpoint, GraphQLEndpoint>();
            services.AddTransient<IDependencyResolver, GraphQLDependencyResolver>();
            services.AddSingleton<IDataLoaderContextAccessor, DataLoaderContextAccessor>();
            services.AddTransient<GraphQL.Execution.IDocumentExecutionListener, DataLoaderDocumentListener>();

            return services;
        }

        public static IServiceCollection AddGraphTypes(
            this IServiceCollection services,
            Assembly assembly)
        {
            foreach (var type in assembly.GetTypes()
                .Where(x => !x.IsAbstract && typeof(IGraphType).IsAssignableFrom(x)))
            {
                services.TryAdd(new ServiceDescriptor(type, type, ServiceLifetime.Singleton));
            }

            return services;
        }

        public static IApplicationBuilder UseGraphQL(
            this IApplicationBuilder builder,
            string path)
        {
            builder.Map(path, app =>
            {
                app.UseAppContext();

                app.UseMiddleware<GraphQLMiddleware>();
            });

            return builder;
        }
    }
}
