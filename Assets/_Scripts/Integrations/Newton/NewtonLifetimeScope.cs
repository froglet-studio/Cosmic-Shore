using VContainer;
using VContainer.Unity;

namespace CosmicShore.Integrations.Newton
{
    public class NewtonLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<ISerializeFactory, SerializeFactory>(Lifetime.Singleton);
            builder.Register<ISerializeServices, CustomSerialize>(Lifetime.Singleton);
            builder.RegisterEntryPoint<JsonPresenter>();
        }
    }
}
