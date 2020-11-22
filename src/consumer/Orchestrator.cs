﻿using Common.Domain.Entities;
using Common.Domain.Models.Events;
using Common.Models.Options;
using Common.Repositories;
using Common.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Consumer
{
    public interface IOrchestrator
    {
        Task OrchestrateAsync(NewUserEvent message);
    }

    public class Orchestrator : IOrchestrator
    {
        private readonly ILogger<UserService> _logger;
        private readonly IUserRepository _userRepository;
        private readonly IMessagingService _messagingService;
        private readonly ICacheService _cacheService;
        private readonly Messaging _messaging;

        public Orchestrator(
            ILogger<UserService> logger,
            IUserRepository userRepository,
            IMessagingService messagingService,
            ICacheService cacheService,
            IOptions<Messaging> messaging)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _messagingService = messagingService ?? throw new ArgumentNullException(nameof(messagingService));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _messaging = messaging.Value ?? throw new ArgumentNullException(nameof(messaging));
        }

        public async Task OrchestrateAsync(NewUserEvent message)
        {
            var user = await _cacheService.GetSingleAsync<User>("NEWUSER");

            var users = await _cacheService.GetListAsync<User>("PAGINATEDUSERS");

            if (message.Id == user.Id && users.Any())
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
