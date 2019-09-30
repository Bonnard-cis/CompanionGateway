using System.IO;
using System.Threading.Tasks;
using Companion.Backend.GraphQL;
using GraphQL.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Companion.Backend.AspNetCore.Middleware.GraphQL
{
    public class GraphQLMiddleware : IMiddleware
    {
        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            var query = default(string);
            var operationName = default(string);
            var variables = default(string);

            var writer = new DocumentWriter();

            if (context.Request.Method == HttpMethods.Get)
            {
                query = context.Request.Query["query"];
                operationName = context.Request.Query["operationName"];
                variables = context.Request.Query["variables"];
            }
            else if (context.Request.Method == HttpMethods.Post)
            {
                using (var reader = new JsonTextReader(new StreamReader(context.Request.Body)))
                {
                    var json = JObject.Load(reader);

                    query = json.Value<string>("query");
                    operationName = json.Value<string>("operationName");
                    variables = json["variables"]?.ToString(Formatting.Indented);
                }
            }
            else
            {
                context.Response.StatusCode = 400;
            }

            var endpoint = context.RequestServices.GetRequiredService<IGraphQLEndpoint>();

            var result = await endpoint.Execute(
                query,
                operationName,
                variables,
                internalQuery: true);

            context.Response.ContentType = "application/json";

            await context.Response.WriteAsync(result);
        }
    }
}
