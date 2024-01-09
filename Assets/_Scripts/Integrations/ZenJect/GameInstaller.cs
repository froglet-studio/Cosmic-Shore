using CosmicShore.Integrations.ZenJect;
using UnityEngine;
using Zenject;

namespace CosmicShore
{
    public class GameInstaller : MonoInstaller
    {
        // This can be cumbersome and error prone, not recommended.
        [SerializeField] private TestFoo _testFoo;

        public override void InstallBindings()
        {
            Container.BindInstance(_testFoo);
            Container.Bind<IInitializable>().To<TestBar>().AsSingle();
        }
        
    }
}
