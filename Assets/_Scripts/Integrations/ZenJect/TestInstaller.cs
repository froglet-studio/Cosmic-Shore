using NLog;
using Zenject;


namespace CosmicShore.Integrations.ZenJect
{
    public class TestInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            BaseInstaller.Install(Container);
        }
    }

    public interface ITestRunner
    {
    }

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

    public class BaseInstaller : Installer<BaseInstaller>
    {
        public override void InstallBindings()
        {
            var log = LogManager.GetLogger("Logger"); 
            
            Container.BindInstance("Hello world").AsSingle();
            // Container.BindInstance(log).AsSingle();
            // Container.Bind<NLogManager>().AsSingle().NonLazy();
            Container.Bind<ILogger>().FromMethod(GetLogger).AsSingle().NonLazy();
            Container.Bind<ITestRunner>().To<TestRunner>().AsCached().NonLazy();
        }
        
        private ILogger GetLogger(InjectContext context)
        {
            return LogManager.GetLogger("Logger");
        }
    }
}