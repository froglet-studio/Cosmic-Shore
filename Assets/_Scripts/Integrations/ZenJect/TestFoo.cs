using UnityEngine;
using Zenject;
using ILogger = NLog.ILogger;

namespace CosmicShore.Integrations.ZenJect
{
    public class TestFoo : MonoBehaviour
    {
        [Inject] private readonly ILogger _logger;
        public void Inititalize(string caller)
        {
            _logger.Info($"TestFoo Initialized from {caller}");
        }
    }
}
