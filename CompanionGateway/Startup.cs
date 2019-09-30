using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using Companion.AspNetCore.ForwardedHeaders;
using Companion.AspNetCore.Proxy;
using Companion.AspNetCore.Serilog;
using Companion.Backend;
using Companion.Backend.Oobe;
using Companion.Backend.Schema;
using Companion.BusinessObjects.Security;
using Companion.Core;
using Companion.DocumentAggregator;
using Companion.Sync;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.AspNetCore.SpaServices.Webpack;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Companion.Gateway
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<DebugToolSettings>(new DebugToolSettings()
            {
                IsEnabled = false,
            });

            services.AddMemoryCache();
            services.AddBackend();
            
            services.AddCompanionSession()
                .AddLanguageProvider<ContactSecuritySessionLanguageProvider>();
            services.AddDocumentAggregator();

            services.AddFileStore();

            services.AddAppContext();

            services.AddWorkflow();
            services.AddI18n();

            services.AddTransient<WorkspaceType>(provider =>
            {
                return new WorkspaceType(
                    provider.GetService<SyncApiClient>());
            });

            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

            services.AddSingleton<SyncConfigurationProvider>();
            services.AddSingleton<SyncClientOptions>(_ =>
            {
                var options = new SyncClientOptions();

                var address = Companion.Core.Security.SecurityCafe.SecurityItemManager.GetSettings("Companion.Sync.Server", true)
                   ?.GetSettingAsString("Address", true);


                if (Uri.TryCreate(address, UriKind.Absolute, out var addressUri))
                {
                    options.Address = addressUri;
                }

                return options;
            });
            services.AddTransient<ForwardedHeadersHandler>();
            services.AddHttpClient<SyncApiClient>()
                .AddHttpMessageHandler<ForwardedHeadersHandler>();

            services.AddGraphQL<CompanionSchema>();
            services.AddGraphTypes(typeof(CompanionSchema).Assembly);
            
            services.AddForwardedHeadersFromAppXml("Companion.Gateway", defaultToEnabled: true);
            
            services.AddProxy();
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseSerilog();
            app.UseForwardedHeaders();

            if (!env.IsDevelopment())
            {
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            //Because of Webpack, these must be as rewrite.
            //Future plan:
            // - Stop using AspNet webpack integration
            // - Start webpack manually
            // - Forward request to webpack dev server manually in their own MVC controller (in dev)
            var rewriteOptions = new RewriteOptions()
                .AddRedirect("^app/store$", "app/store/", 301)
                .AddRewrite("^app/store/.*$", "static/ui/CompanionRep/index.html", true)
                .AddRedirect("^$", "app/store/", 302);
            
            var environment = app.ApplicationServices.GetRequiredService<IHostingEnvironment>();

            if (env.IsDevelopment())
            {
                rewriteOptions
                    .AddRedirect("^app/rep$", "app/rep/", 301)
                    .AddRewrite("^app/rep/.*$", "static/ui/CompanionRep/index.html", true)
                    .AddRedirect("^devtools$", "devtools/", 301)
                    .AddRewrite("^devtools/.*$", "static/ui/DevTools/index.html", true);
            }

            app.UseRewriter(rewriteOptions);

            UseDevFrontend(app);

            app.UseStaticFiles("/static");

            // UseReport(app);

            app.UseI18n("/api/i18n");
            app.UseFileStore();
            app.UseWorkflow("/api/workflow");
            app.UseGraphQL("/api/graphql");
            
            var syncServerOptions = app.ApplicationServices.GetRequiredService<SyncClientOptions>();
            
            app.Map("/api/sync", x => x.RunProxy(new ProxyOptions()
            {
                Scheme = syncServerOptions.Address.Scheme,
                Host = new HostString(syncServerOptions.Address.Authority),
                PathBase = "/api/sync",
            }));
        }

        [Conditional("DEBUG")]
        static void UseDevFrontend(
            IApplicationBuilder builder,
            [CallerFilePath] string thisFile = null)
        {
            var environment = builder.ApplicationServices.GetRequiredService<IHostingEnvironment>();

            var frontendPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile), @"..\Companion.Ui\"));

            if (environment.IsDevelopment()
                 && Directory.Exists(frontendPath))
            {
                var loggerFactory = builder.ApplicationServices.GetRequiredService<ILoggerFactory>();

                StartElmGraph(
                    loggerFactory.CreateLogger("elm-graphql"),
                    frontendPath);

                builder.UseWebpackDevMiddleware(new WebpackDevMiddlewareOptions()
                {
                    ProjectPath = frontendPath,
                    HotModuleReplacement = true,
                    HotModuleReplacementClientOptions = new Dictionary<string, string>()
                    {
                        ["reload"] = "true",
                    },
                });
            }
        }

        [Conditional("DEBUG")]
        static void StartElmGraph(
            ILogger outputLogger,
            string projectPath,
            [CallerFilePath] string thisFile = null)
        {
            var toolPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile), @"..\..\.tools"));

            var startInfo = new System.Diagnostics.ProcessStartInfo()
            {
                FileName = Path.Combine(toolPath, "elm-graphql.exe"),
                Arguments = $"--watch --parentPid {Process.GetCurrentProcess().Id}",
                WorkingDirectory = projectPath,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
            };

            var process = Process.Start(startInfo);

            process.OutputDataReceived += (s, e) =>
            {
                if (e.Data != null)
                {
                    outputLogger.LogInformation(e.Data);
                }
            };

            process.ErrorDataReceived += (s, e) =>
            {
                if (e.Data != null)
                {
                    outputLogger.LogError(e.Data);
                }
            };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }
    }
    
}
