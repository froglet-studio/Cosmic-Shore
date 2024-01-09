using Zenject;

namespace CosmicShore.Integrations.ZenJect
{
    public class TestInstaller : MonoInstaller
    {
        public Settings SceneSettings;
        public override void InstallBindings()
        {
            BaseInstaller.Install(Container);
            Container.BindInstance(SceneSettings.ShipState);
        }
    }
}