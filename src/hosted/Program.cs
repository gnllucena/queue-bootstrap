using Common.Configurations;
using Common.Domain.Models.Architecture;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.Threading.Tasks;

namespace Hosted
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            Log.Logger = Builders.Log();

            try
            {
                var host = Builders.Host(Messaging.RabbitMQ, Cache.Redis);

                host.ConfigureServices((context, services) =>
                {
                    services.AddHostedService<Host>();
                });

                var application = host.Build();

                using (application)
                {
                    await application.StartAsync();

                    await application.WaitForShutdownAsync();
                }
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}