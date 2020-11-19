using Common.Domain.Models.Events;
using System.Threading.Tasks;

namespace Consumer
{
    public interface IOrchestrator
    {
        Task OrchestrateAsync(NewUserEvent message);
    }

    public class Orchestrator : IOrchestrator
    {
        public async Task OrchestrateAsync(NewUserEvent message)
        {
            await Task.Delay(1000);
        }
    }
}
