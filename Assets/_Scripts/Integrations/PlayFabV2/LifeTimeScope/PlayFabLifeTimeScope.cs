using CosmicShore.Integrations.Playfab.Authentication;
using CosmicShore.Integrations.Playfab.Economy;
using CosmicShore.Integrations.Playfab.Groups;
using CosmicShore.Integrations.Playfab.PlayerModels;
using CosmicShore.Integrations.Playfab.PlayStream;
using CosmicShore.Integrations.PlayFabV2.Models;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace CosmicShore
{
    public class PlayFabLifeTimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // PlayFab Authentication
            builder.Register<AuthenticationManager>(Lifetime.Singleton);
            builder.Register<PlayFabAccount>(Lifetime.Singleton);
            builder.Register<UserProfile>(Lifetime.Singleton);
            builder.Register<PlayerSession>(Lifetime.Singleton);
            
            // PlayFab Leaderboard
            builder.Register<LeaderboardManager>(Lifetime.Singleton);
            
            // PlayFab Player Data
            builder.Register<PlayerDataController>(Lifetime.Singleton);
            
            // PlayFab Catalog and inventory
            builder.Register<CatalogManager>(Lifetime.Singleton);
            builder.Register<StoreShelve>(Lifetime.Singleton);
            builder.Register<Inventory>(Lifetime.Singleton);
            
            // PlayFab Group Controller
            builder.Register<GroupController>(Lifetime.Singleton);
            
            // PlayFab Analytics
            builder.Register<AnalyticsController>(Lifetime.Singleton);
            
            builder.UseEntryPoints(Lifetime.Singleton, entryPoints =>
            {
                entryPoints.Add<AuthenticationManager>();
                entryPoints.Add<LeaderboardManager>();
                entryPoints.Add<CatalogManager>();
                entryPoints.Add<GroupController>();
                entryPoints.Add<PlayerDataController>();
            });
            
            builder.RegisterEntryPointExceptionHandler(Debug.LogException);
        }
    }
}
