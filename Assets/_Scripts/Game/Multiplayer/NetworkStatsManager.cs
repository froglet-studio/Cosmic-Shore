using CosmicShore.Core;
using Obvious.Soap;
using Unity.Multiplayer.Samples.Utilities;
using UnityEngine;


namespace CosmicShore.Game
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

