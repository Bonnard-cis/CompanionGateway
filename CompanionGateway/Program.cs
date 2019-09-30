using System;
using System.Diagnostics;
using System.IO;
using CommandLine;
using Companion.AspNetCore.Kestrel;
using Companion.Backend;
using Companion.Core;
using Companion.Core.SystemEvent;
using Companion.Core.Utilities;
using Companion.Core.Utilities.WindowsServices;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.WindowsServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace Companion.Gateway
{
    class Program
    {
        public class Options
        {
            [Option(Required = false, HelpText = "Location of AppData")]
            public string Data { get; set; }
        }

        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                .WithParsed<Options>(o =>
                {
                    if (!string.IsNullOrWhiteSpace(o.Data))
                    {
                        UtilitiesCafe.DirectoryManager.SetCommonDirectory(
                            CommonDirectory.AppData,
                            o.Data);
                    }

                    UtilitiesCafe.DirectoryManager.SetCommonDirectory(
                        CommonDirectory.Temp,
                        Path.Combine(Path.GetTempPath(), "cis", "Companion.Gateway"));

                    Run();
                });
        }

        static void Run()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .WriteTo.File(
                    new CompactJsonFormatter(),
                    Path.Combine(
                        UtilitiesCafe.DirectoryManager.GetCommonDirectory(CommonDirectory.Logs),
                        "Gateway-.txt"),
                    rollingInterval: RollingInterval.Day,
                    rollOnFileSizeLimit: true)
                .CreateLogger();

            try
            {
                Log.Information("Starting CompanionGateway");

                var applicationInfo = new ApplicationInfo(
                    "Gateway",
                    SemanticVersion.Parse(ThisAssembly.AssemblyInformationalVersion),
                    null);

                SystemEventCafe.Client = new SystemEventClient(
                    SystemEventConfiguration.LoadFromAppXml(),
                    applicationInfo);

                SystemEventCafe.Client.StartApplication();

                var host = new WebHostBuilder()
                    .UseKestrelFromAppXml("Companion.Gateway")
                    .UseContentRoot(System.AppContext.BaseDirectory)
                    .ConfigureAppConfiguration((hostingContext, config) =>
                    {
                        var env = hostingContext.HostingEnvironment;

                        config
                            .SetBasePath(UtilitiesCafe.DirectoryManager.GetCommonDirectory(CommonDirectory.AppData))
                            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                            .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true);
                    })
                    .ConfigureServices(services =>
                    {
                        services.AddSingleton(applicationInfo);
                    })
                    .UseSerilog()
                    .UseStartup<Startup>()
                    .Build();

                Configuration.StartServer();

                if (WindowsServicesHelper.IsUnderServiceHost(Process.GetCurrentProcess()))
                {
                    host.RunAsService();
                }
                else
                {
                    host.Run();
                }

                SystemEventCafe.Client.StopApplication();

                Log.Information("CompanionGateway stopped");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "CompanionGateway termniated unexpectedly");
            }
            finally
            {
                SystemEventCafe.Client?.CloseAndFlush();

                Log.CloseAndFlush();
            }
        }
    }
}
