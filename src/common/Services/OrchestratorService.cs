using Common.Domain.Entities;
using Common.Domain.Models.Events;
using Common.Factories;
using Common.Models.Options;
using Common.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Common.Services
{
    public interface IOrchestratorService
    {
        Task OrchestrateAsync(NewUserEvent message);
    }

    public class OrchestratorService : IOrchestratorService
    {
        private readonly ILogger<UserService> _logger;
        private readonly IUserRepository _userRepository;
        private readonly IMessagingService _messagingService;
        private readonly ICacheService _cacheService;
        private readonly IDatabaseFactory _databaseFactory;
        private readonly ICacheFactory _cacheFactory;

        private readonly Messaging _messaging;

        public OrchestratorService(
            ILogger<UserService> logger,
            IUserRepository userRepository,
            IMessagingService messagingService,
            IDatabaseFactory databaseFactory,
            ICacheFactory cacheFactory,
            ICacheService cacheService,
            IOptions<Messaging> messaging)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _messagingService = messagingService ?? throw new ArgumentNullException(nameof(messagingService));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _databaseFactory = databaseFactory ?? throw new ArgumentNullException(nameof(databaseFactory));
            _cacheFactory = cacheFactory ?? throw new ArgumentNullException(nameof(cacheFactory));
            _messaging = messaging.Value ?? throw new ArgumentNullException(nameof(messaging));
        }

        public async Task OrchestrateAsync(NewUserEvent message)
        {
            using (_logger.BeginScope(Guid.NewGuid().ToString()))
            {
                try
                {
                    await _cacheFactory.ConnectAsync();

                    await _databaseFactory.OpenConnectionAsync();

                    _databaseFactory.BeginTransaction();

                    await ProcessAsync(message);

                    _databaseFactory.CommitTransaction();
                }
                catch (Exception ex)
                {
                    _logger.LogCritical($"HOST | CRITICAL ERROR: {ex}");

                    _databaseFactory.RollbackTransaction();

                    throw;
                }
                finally
                {
                    await _cacheFactory.DisconnectAsync();

                    _databaseFactory.CloseConnection();
                }
            }
        }
        
        private async Task ProcessAsync(NewUserEvent message)
        {
            var user = await _cacheService.GetSingleAsync<User>("NEWUSER");

            var users = await _cacheService.GetListAsync<User>("PAGINATEDUSERS");

            if (message.Id == user?.Id && users.Any())
            {
                var userExistsOnCacheEvent = new UserExistsOnCacheEvent()
                {
                    Id = message.Id
                };

                _messagingService.Queue(_messaging.Publishing.Exchange.Name, _messaging.Publishing.Routingkey, userExistsOnCacheEvent);
            }
        }
    }
}
