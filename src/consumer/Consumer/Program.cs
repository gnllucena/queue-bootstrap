using Amazon.CloudWatchLogs;
using Common.Domain.Entities;
using Common.Factories;
using Common.Models.Options;
using Common.Repositories;
using Common.Services;
using Common.Validators;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Json;
using Serilog.Sinks.AwsCloudWatch;
using System.IO;
using System.Threading.Tasks;

namespace Consumer
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            Log.Logger = CreateLogger();

            try
            {
                var host = BuildHost(args);

                using (host)
                {
                    await host.StartAsync();

                    await host.WaitForShutdownAsync();
                }
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private static Logger CreateLogger()
        {
            var group = "AnotherContext";
            var application = "Consumer.AnotherContext.NewUserEvent";

#if (DEBUG)
            var client = Configuration.GetAWSOptions().CreateServiceClient<IAmazonCloudWatchLogs>();

            var options = new CloudWatchSinkOptions
            {
                LogGroupName = $"Applications/{group}/{application}",
                LogStreamNameProvider = new DefaultLogStreamProvider(),
                BatchSizeLimit = 100,
                QueueSizeLimit = 10000,
                RetryAttempts = 3,
                LogGroupRetentionPolicy = LogGroupRetentionPolicy.FiveDays,
                TextFormatter = new JsonFormatter(),
                MinimumLogEventLevel = LogEventLevel.Information
            };
#endif
            return new LoggerConfiguration()
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", application)
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Debug()
                .WriteTo.Console(
                    outputTemplate: "{NewLine}[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Scope} {Message}{NewLine}{Exception}"
                )
#if (DEBUG)
                .WriteTo.AmazonCloudWatch(options, client)
#endif
                .CreateLogger();
        }

        private static IConfiguration Configuration { get; } = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        public static IHost BuildHost(string[] args) => new HostBuilder()
            .ConfigureAppConfiguration((hostContext, configuration) =>
            {
                configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            })
            .ConfigureServices((hostContext, services) =>
            {
                services.AddOptions();

                services.AddDefaultAWSOptions(hostContext.Configuration.GetAWSOptions());

                services.Configure<Connection>(hostContext.Configuration.GetSection("Database"));
                services.Configure<Cache>(hostContext.Configuration.GetSection("Cache"));
                services.Configure<Messaging>(hostContext.Configuration.GetSection("Messaging"));

                services.AddSingleton<IDatabaseFactory, DatabaseFactory>();
                services.AddSingleton<IMessagingFactory, MessagingFactory>();
                services.AddSingleton<ICacheFactory, CacheFactory>();

                services.AddTransient<ISqlService, SqlService>();
                services.AddTransient<IValidationService, ValidationService>();
                services.AddTransient<IAuthenticatedService, AuthenticatedService>();
                services.AddTransient<IUserService, UserService>();
                services.AddTransient<IMessagingService, MessagingService>();
                services.AddTransient<ICacheService, CacheService>();

                services.AddSingleton<IValidator<User>, UserValidator>();

                services.AddScoped<IUserRepository, UserRepository>();

                services.AddTransient<IOrchestrator, Orchestrator>();

                services.AddHostedService<Host>();
            })
            .UseSerilog()
            .Build();
    }
}