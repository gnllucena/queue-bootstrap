using Common.Domain.Models.Events;
using Common.Factories;
using Common.Models.Options;
using Common.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Hosted
{
    public class Host : BackgroundService
    {
        private string _tag;
        private Task _executingTask;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly Messaging _messaging;
        private readonly IMessagingService _messagingService;
        private readonly IMessagingFactory _messagingFactory;
        private readonly IOrchestratorService _orchestratorService;
        private readonly ILogger<Host> _logger;

        public Host(
            ICacheFactory cacheFactory,
            IMessagingFactory messagingFactory,
            IDatabaseFactory databaseFactory,
            IOrchestratorService orchestratorService,
            IMessagingService messagingService,
            IOptions<Messaging> messaging,
            ILogger<Host> logger)
        {
            _messaging = messaging.Value ?? throw new ArgumentNullException(nameof(messaging));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _orchestratorService = orchestratorService ?? throw new ArgumentNullException(nameof(orchestratorService));
            _messagingService = messagingService ?? throw new ArgumentNullException(nameof(messagingService));
            _messagingFactory = messagingFactory ?? throw new ArgumentNullException(nameof(messagingFactory));
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            _executingTask = ExecuteAsync(_cancellationTokenSource.Token);

            if (_executingTask.IsCompleted)
            {
                return _executingTask;
            }

            return Task.CompletedTask;
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_executingTask == null)
            {
                return;
            }

            _cancellationTokenSource.Cancel();

            await Task.WhenAny(_executingTask, Task.Delay(-1, cancellationToken)).ConfigureAwait(false);

            var channel = _messagingFactory.Configure();
            channel.BasicCancel(_tag);

            _messagingFactory.Disconnect();
        }

        protected override Task ExecuteAsync(CancellationToken cancellationToken)
        {
            var channel = _messagingFactory.Configure();
            var consumer = new AsyncEventingBasicConsumer(channel);

            consumer.Received += _messagingService.Dequeue(cancellationToken, async (string raw, NewUserEvent message) =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (message == null)
                {
                    return;
                }

                await _orchestratorService.OrchestrateAsync(message);
            });

            _tag = channel.BasicConsume(_messaging.Consuming.Queue, false, consumer);

            return Task.CompletedTask;
        }
    }
}
