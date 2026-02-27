using Obvious.Soap;
using CosmicShore.Utility;
using UnityEngine;
using CosmicShore.Gameplay;
namespace CosmicShore.Gameplay
{
    public class NetworkStatsManager : StatsManager
    {
        [SerializeField]
        NetcodeHooks _netcodeHooks;

        protected override void OnEnable()
        {
            base.OnEnable();
            _netcodeHooks.OnNetworkSpawnHook += OnNetworkSpawn;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            _netcodeHooks.OnNetworkSpawnHook -= OnNetworkSpawn;
        }

        private void OnNetworkSpawn()
        {
            if (_netcodeHooks.IsServer)
            {
                allowRecord = true;
                return;
            }

            allowRecord = false;
        }
    }
}

