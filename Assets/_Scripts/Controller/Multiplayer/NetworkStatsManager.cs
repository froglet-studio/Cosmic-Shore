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

        void OnEnable()
        {
            _netcodeHooks.OnNetworkSpawnHook += OnNetworkSpawn;
        }

        void OnDisable()
        {
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

