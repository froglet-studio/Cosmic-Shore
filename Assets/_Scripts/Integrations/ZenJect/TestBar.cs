using NLog;
using Zenject;

namespace CosmicShore.Integrations.ZenJect
{
    public class TestBar : IInitializable
    {
        private readonly TestFoo _testFoo;
        [Inject] private readonly ILogger _logger;

        public TestBar(TestFoo testFoo)
        {
            _testFoo = testFoo;
        }

        public void Initialize()
        {
            _logger.Info("TestBar initialized.");
            _testFoo.Inititalize(nameof(TestBar));
        }
    }
}