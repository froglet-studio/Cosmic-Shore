using NLog;
using Zenject;

namespace CosmicShore.Integrations.ZenJect
{
    public class TestRunner : ITestRunner
    {
        private ILogger _log;
        [Inject] private Settings _settings;
        public TestRunner(ILogger log, string message)
        {
            _log = log;
            
            if (_log.IsInfoEnabled)
            {
                _log.Info(message + " Ship state: " + _settings.ShipState);
                // _log.Info(message);
            }
        }
    }
}