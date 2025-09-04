using CosmicShore.Core;
using Obvious.Soap;
using Unity.Multiplayer.Samples.Utilities;
using UnityEngine;


namespace CosmicShore.Game
{
    [RequireComponent(typeof(NetcodeHooks))]
    public class NetworkStatsManager : StatsManager
    {
        /*[SerializeField] 
        ScriptableEventNoParam _onPlayGame;
        
        [SerializeField]
        ScriptableEventNoParam _onGameOver;*/
        
        NetcodeHooks _netcodeHooks;

        public override void Awake()
        {
            base.Awake();
            _netcodeHooks = GetComponent<NetcodeHooks>();
        }

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

        public override IRoundStats GetOrCreateRoundStats(Teams team)
        {
            var player = NetworkPlayerClientCache.GetPlayerByTeam(team);
            if (!player)
            {
                Debug.LogError($"NetworkStatsManager: No player found for team {team}.");
                return null;
            }

            if (player.gameObject.TryGetComponent(out NetworkRoundStats roundStats)) 
                return roundStats;
            
            Debug.LogError($"NetworkStatsManager: No NetworkRoundStats found for player on team {team}.");
            return null;
        }
    }
}

