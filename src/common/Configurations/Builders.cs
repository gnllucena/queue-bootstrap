using Amazon.CloudWatchLogs;
using Common.Domain.Entities;
using Common.Domain.Models.Architecture;
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
using System;
using System.Configuration;
using System.IO;

namespace Common.Configurations
{
    public class Builders
    {
        private static IConfiguration Configuration { get; } = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        public static IHostBuilder Host(Domain.Models.Architecture.Messaging messaging, Domain.Models.Architecture.Cache cache) => new HostBuilder()
            .ConfigureAppConfiguration((context, configuration) =>
            {
                configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                configuration.AddEnvironmentVariables();
            })
            .ConfigureServices((context, services) =>
            {
                services.AddOptions();

                services.AddDefaultAWSOptions(context.Configuration.GetAWSOptions());

                services.Configure<Connection>(context.Configuration.GetSection("Database"));
                services.Configure<Models.Options.Cache>(context.Configuration.GetSection("Cache"));
                services.Configure<Models.Options.Messaging>(context.Configuration.GetSection("Messaging"));

                services.AddSingleton<IDatabaseFactory, DatabaseFactory>();

                services.AddTransient<ISqlService, SqlService>();
                services.AddTransient<IValidationService, ValidationService>();
                services.AddTransient<IAuthenticatedService, AuthenticatedService>();
                services.AddTransient<IUserService, UserService>();

                services.AddSingleton<IValidator<User>, UserValidator>();

                services.AddScoped<IUserRepository, UserRepository>();

                services.AddTransient<IOrchestratorService, OrchestratorService>();

                switch (messaging)
                {
                    case Domain.Models.Architecture.Messaging.RabbitMQ:
                        services.AddSingleton<IMessagingFactory, RabbitMQFactory>();
                        services.AddTransient<IMessagingService, RabbitMQService>();
                        break;
                    case Domain.Models.Architecture.Messaging.SQS:
                        services.AddSingleton<IMessagingFactory, RabbitMQFactory>();
                        services.AddTransient<IMessagingService, SQSService>();
                        break;
                    default:
                        throw new NotImplementedException();
                }

                switch (cache)
                {
                    case Domain.Models.Architecture.Cache.Redis:
                        services.AddSingleton<ICacheFactory, RedisFactory>();
                        services.AddTransient<ICacheService, RedisService>();
                        break;
                    default:
                        throw new NotImplementedException();
                }
            })
            .UseSerilog(); 

        public static Logger Log()
        {
            var microservice = Configuration.GetSection("App:Microservice").Value;
            var service = Configuration.GetSection("App:Service").Value;

#if (!DEBUG)
            var client = Configuration.GetAWSOptions().CreateServiceClient<IAmazonCloudWatchLogs>();

            var options = new CloudWatchSinkOptions
            {
                LogGroupName = $"Microservice/{microservice}/{service}",
                LogStreamNameProvider = new DefaultLogStreamProvider(),
                BatchSizeLimit = 100,
                QueueSizeLimit = 10000,
                RetryAttempts = 3,
                LogGroupRetentionPolicy = LogGroupRetentionPolicy.ThreeDays,
                TextFormatter = new JsonFormatter(),
                MinimumLogEventLevel = LogEventLevel.Information
            };
#endif

            return new LoggerConfiguration()
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Microservice", microservice)
                .Enrich.WithProperty("Service", service)
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Debug()
                .WriteTo.Console(
                    outputTemplate: "{NewLine}[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Scope} {Message}{NewLine}{Exception}"
                )

#if (!DEBUG)
                .WriteTo.AmazonCloudWatch(options, client)
#endif

                .CreateLogger();
        }
    }
}
