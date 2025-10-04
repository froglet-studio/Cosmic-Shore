using CosmicShore.Core;
using Obvious.Soap;
using Unity.Multiplayer.Samples.Utilities;
using UnityEngine;


namespace CosmicShore.Game
{
    public class NetworkStatsManager : StatsManager
    {
        /*[SerializeField] 
        ScriptableEventNoParam _onPlayGame;
        
        [SerializeField]
        ScriptableEventNoParam _onGameOver;*/
        
        [SerializeField]
        NetcodeHooks _netcodeHooks;

        protected override void OnEnable()
        {
            // _onPlayGame.OnRaised += ResetStats;
            // _onGameOver.OnRaised += OutputRoundStats;


            _netcodeHooks.OnNetworkSpawnHook += OnNetworkSpawn;
        }

        protected override void OnDisable()
        {
            // _onPlayGame.OnRaised -= ResetStats;
            // _onGameOver.OnRaised -= OutputRoundStats;

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

