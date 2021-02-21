using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Common.Configurations;
using Common.Domain.Models.Architecture;
using Common.Domain.Models.Events;
using Common.Services;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Serilog;
using System.Threading.Tasks;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
namespace Lambda
{
    public class Function
    {
        public async Task Handler(SQSEvent evnt, ILambdaContext context)
        {
            Log.Logger = Builders.Log();

            try
            {
                var host = Builders.Host(Messaging.SQS, Cache.Redis);

                var application = host.Build();

                using (application)
                {
                    var orchestrator = application.Services.GetService<IOrchestratorService>();

                    foreach (var record in evnt.Records)
                    {
                        var message = JsonConvert.DeserializeObject<NewUserEvent>(record.Body);

                        await orchestrator.OrchestrateAsync(message);
                    }
                }
            }
            finally
            {
                Log.CloseAndFlush();
            }

            await Task.CompletedTask;
        }
    }
}
