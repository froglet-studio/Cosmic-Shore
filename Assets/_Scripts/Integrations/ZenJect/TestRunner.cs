using NLog;

namespace CosmicShore.Integrations.ZenJect
{
    public class TestRunner : ITestRunner
    {
        private ILogger _log;
        public TestRunner(ILogger log, string message)
        {
            _log = log;
            
            if (_log.IsInfoEnabled)
            {
                _log.Info(message + GetHashCode());
            }
        }
    }
}