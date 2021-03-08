using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Steeltoe.Connector;
using Steeltoe.Connector.RabbitMQ;
using Steeltoe.Extensions.Configuration.CloudFoundry;
using Steeltoe.Extensions.Configuration.ConfigServer;
using Steeltoe.Extensions.Configuration.Kubernetes;
using Steeltoe.Extensions.Logging.DynamicSerilog;
using Microsoft.Azure.SpringCloud.Client;

namespace RabbitDemo
{
    class Program
    {
        static void Main(string[] args)
        {
            var builder = new ConfigurationBuilder();
            BuildStartupConfig(builder);

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(builder.Build())
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .CreateLogger();

            Log.Logger.Information("Configuring Services");

            var host = Host.CreateDefaultBuilder()
                .ConfigureLogging((hostingContext, loggingBuilder) =>
                {
                    loggingBuilder.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));

                    // Add Serilog Dynamic Logger
                    loggingBuilder.AddDynamicSerilog();
                })

                //// For loading when on cloud foundry
                .AddCloudFoundryConfiguration()

                // For loading when on azure spring cloud
                .UseAzureSpringCloudService()

                // Add Config Server if available
                .AddConfigServer()

                // Load all the connection strings for bound services via Steeltoe connectors which benefits TAS developers
                //    - Also supports Azure Service Broker service bindings via STv3
                //    - This approach allows for multiple database connections for the same datavase type using connection string names
                .ConfigureAppConfiguration(builder => builder.AddConnectionStrings())

                // Alternately load a secrets file if it is available
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    config.AddJsonFile("secrets/appsettings.secrets.json", optional: true, reloadOnChange: true);
                })
                .AddKubernetesConfiguration()

                // Configure services
                .ConfigureServices((context, services) =>
                {
                    services.AddRabbitMQConnection(context.Configuration);
                    services.AddTransient<IRabbitSender, RabbitSender>();
                })                
                .UseSerilog()
                .Build();

            Log.Logger.Information("Service Starting");
            var svc = ActivatorUtilities.CreateInstance<RabbitSender>(host.Services);
            svc.Run();
        }

        /// <summary>
        /// Load configuration to enable logging earlier in service creation
        /// </summary>
        /// <param name="builder"></param>
        static void BuildStartupConfig(IConfigurationBuilder builder)
        {
            builder.SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
                .AddEnvironmentVariables();
        }
    }
}
