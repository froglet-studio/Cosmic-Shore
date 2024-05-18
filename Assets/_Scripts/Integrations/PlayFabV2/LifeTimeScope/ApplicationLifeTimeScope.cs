using System;
using CosmicShore.App.UI.Menus;
using CosmicShore.Integrations.PlayFab.Authentication;
using CosmicShore.Integrations.PlayFab.Economy;
using CosmicShore.Integrations.PlayFab.Groups;
using CosmicShore.Integrations.PlayFab.PlayerModels;
using CosmicShore.Integrations.PlayFab.PlayStream;
using CosmicShore.Integrations.PlayFabV2.Models;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace CosmicShore
{
    public class ApplicationLifeTimeScope : LifetimeScope
    {
        // [SerializeField] private ProfileMenu profileMenu;
        // [SerializeField] private LeaderboardsMenu leaderboardsMenu;
        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);
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
            
            // Register Player Profile Menu
            // builder.RegisterComponentInHierarchy<ProfileMenu>();
            
            // Register Leaderboards menu
            // builder.RegisterComponentInHierarchy<LeaderboardsMenu>();
            
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

        private void Start()
        {
            DontDestroyOnLoad(gameObject);
        }
    }
}
