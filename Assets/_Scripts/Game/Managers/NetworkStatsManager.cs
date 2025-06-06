using CosmicShore.Game;
using CosmicShore.Utility.ClassExtensions;
using Unity.Multiplayer.Samples.Utilities;
using UnityEngine;


namespace CosmicShore.Core
{
    [RequireComponent(typeof(NetcodeHooks))]

    public class NetworkStatsManager : StatsManager
    {
        NetcodeHooks _netcodeHooks;

        public override void Awake()
        {
            base.Awake();
            _netcodeHooks = GetComponent<NetcodeHooks>();
        }

        protected override void OnEnable()
        {
            GameManager.OnPlayGame += ResetStats;
            GameManager.OnGameOver += OutputRoundStats;


            _netcodeHooks.OnNetworkSpawnHook += OnNetworkSpawn;
            _netcodeHooks.OnNetworkDespawnHook += OnNetworkDespawn;
        }

        protected override void OnDisable()
        {
            GameManager.OnPlayGame -= ResetStats;
            GameManager.OnGameOver -= OutputRoundStats;

            _netcodeHooks.OnNetworkSpawnHook -= OnNetworkSpawn;
            _netcodeHooks.OnNetworkDespawnHook -= OnNetworkDespawn;
        }

        private void OnNetworkSpawn()
        {
            if (_netcodeHooks.IsServer)
            {
                _onTrailBlockCreatedEventChannel.OnEventRaised += OnBlockCreated;
                _onTrailBlockDestroyedEventChannel.OnEventRaised += OnBlockDestroyed;
                _onTrailBlockRestoredEventChannel.OnEventRaised += OnBlockRestored;
            }
        }

        private void OnNetworkDespawn()
        {
            if (_netcodeHooks.IsServer)
            {
                _onTrailBlockCreatedEventChannel.OnEventRaised -= OnBlockCreated;
                _onTrailBlockDestroyedEventChannel.OnEventRaised -= OnBlockDestroyed;
                _onTrailBlockRestoredEventChannel.OnEventRaised -= OnBlockRestored;
            }
        }

        protected override IRoundStats GetRoundStats(Teams team)
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

