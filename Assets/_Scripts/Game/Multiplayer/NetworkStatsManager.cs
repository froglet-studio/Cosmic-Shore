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
            if (!_netcodeHooks.IsServer)
                allowRecord = false;
        }

        public override IRoundStats GetOrCreateRoundStats(Domains domain)
        {
            var player = NetworkPlayerClientCache.GetPlayerByTeam(domain);
            if (!player)
            {
                Debug.LogError($"NetworkStatsManager: No player found for team {domain}.");
                return null;
            }

            if (player.gameObject.TryGetComponent(out NetworkRoundStats roundStats)) 
                return roundStats;
            
            Debug.LogError($"NetworkStatsManager: No NetworkRoundStats found for player on team {domain}.");
            return null;
        }
    }
}

