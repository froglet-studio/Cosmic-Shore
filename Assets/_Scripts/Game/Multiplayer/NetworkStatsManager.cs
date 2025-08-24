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
            _netcodeHooks.OnNetworkDespawnHook += OnNetworkDespawn;
        }

        protected override void OnDisable()
        {
            // _onPlayGame.OnRaised -= ResetStats;
            // _onGameOver.OnRaised -= OutputRoundStats;

            _netcodeHooks.OnNetworkSpawnHook -= OnNetworkSpawn;
            _netcodeHooks.OnNetworkDespawnHook -= OnNetworkDespawn;
        }

        private void OnNetworkSpawn()
        {
            if (_netcodeHooks.IsServer)
            {
                _onTrailBlockCreatedEventChannel.OnRaised += OnBlockCreated;
                _onTrailBlockDestroyedEventChannel.OnRaised += OnBlockDestroyed;
                _onTrailBlockRestoredEventChannel.OnRaised += OnBlockRestored;
            }
        }

        private void OnNetworkDespawn()
        {
            if (_netcodeHooks.IsServer)
            {
                _onTrailBlockCreatedEventChannel.OnRaised -= OnBlockCreated;
                _onTrailBlockDestroyedEventChannel.OnRaised -= OnBlockDestroyed;
                _onTrailBlockRestoredEventChannel.OnRaised -= OnBlockRestored;
            }
        }

        public override IRoundStats GetOrCreateRoundStats(Teams team)
        {
            var player = NetworkPlayerClientCache.GetPlayerByTeam(team);
            if (player == null)
            {
                Debug.LogError($"NetworkStatsManager: No player found for team {team}.");
                return null;
            }
            if (!player.gameObject.TryGetComponent(out NetworkRoundStats roundStats))
            {
                Debug.LogError($"NetworkStatsManager: No NetworkRoundStats found for player on team {team}.");
                return null;
            }
            return roundStats;
        }
    }
}

