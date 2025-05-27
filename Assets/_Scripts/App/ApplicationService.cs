using CosmicShore.Utilities;
using Unity.Netcode;
using UnityEngine;
using VContainer;
using VContainer.Unity;


namespace CosmicShore.App
{
    /// <summary>
    /// An entry point to the application, where we bind all the common dependencies to the root DI Scope
    /// </summary>
    public class ApplicationService : LifetimeScope
    {
        [SerializeField]
        UpdateRunner _updateRunner;

        [SerializeField]
        NetworkManager _networkManager;

        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);

            builder.RegisterComponent(_updateRunner);
            builder.RegisterComponent(_networkManager);
        }

        private void Start()
        {
            DontDestroyOnLoad(gameObject);
        }
    }
}