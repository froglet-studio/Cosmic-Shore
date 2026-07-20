using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using CosmicShore.Data;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    public class MultiplayerFreestyleController : MultiplayerMiniGameControllerBase
    {
        [Header("Scoring")]
        [Tooltip("Drag FreestyleScoringRule.asset - config for the live centerline feed (volume created). The sandbox has no objective; the rule never ends the game.")]
        [SerializeField] ScoringRuleSO rule;

        // ── Live centerline feed (replaces the legacy NetworkScoreTracker's VolumeCreatedScoring) ──
        // Server-only: RoundStats.Score suppresses the local OnScoreChanged while spawned, so only
        // a server write (→ n_Score replication) reaches the MiniGameHUD centerline on every peer.
        // B15 (Docs/ScoringSystem/BUGS.md): detach from THIS record only - never by iterating
        // gameData.RoundStatsList (SceneLoader clears it before old-scene destruction) - and never
        // gate cleanup on turn-end alone (freestyle's turn never ends; exits are scene changes).
        readonly List<IRoundStats> _scoreFeedStats = new();

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            gameData.ScoringRule = rule;
            gameData.OnClientReady.OnRaised += OnClientReady;

            if (IsServer)
                gameData.OnMiniGameTurnStarted.OnRaised += RefreshScoreFeed;
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            gameData.OnClientReady.OnRaised -= OnClientReady;

            if (IsServer)
                gameData.OnMiniGameTurnStarted.OnRaised -= RefreshScoreFeed;
            StopScoreFeed();
        }

        public override void OnDestroy()
        {
            StopScoreFeed(); // B15: destruction paths that bypass despawn must still detach
            base.OnDestroy();
        }

        void OnClientReady() => gameData.SetNonOwnerPlayersActiveInNewClient();

        /// <summary>
        /// Freestyle raises StartTurn once per player activation, so this runs repeatedly -
        /// the Contains guard makes re-subscription a no-op while catching stats that joined
        /// the roster since the last activation.
        /// </summary>
        void RefreshScoreFeed()
        {
            foreach (var stats in gameData.RoundStatsList)
            {
                if (stats == null || _scoreFeedStats.Contains(stats)) continue;
                stats.OnVolumeCreatedChanged += FeedScore;
                _scoreFeedStats.Add(stats);
            }
        }

        void StopScoreFeed()
        {
            foreach (var stats in _scoreFeedStats)
            {
                if (stats == null) continue;
                stats.OnVolumeCreatedChanged -= FeedScore;
            }
            _scoreFeedStats.Clear();
        }

        void FeedScore(IRoundStats stats) => stats.Score = rule.LiveMetric(stats);

        protected override void OnCountdownTimerEnded()
        {
            OnCountdownTimerEnded_ServerRpc(gameData.LocalPlayer.Name);
        }

        [ServerRpc(RequireOwnership = false)]
        void OnCountdownTimerEnded_ServerRpc(FixedString128Bytes playerName)
        {
            OnCountdownTimerEnded_ClientRpc(playerName);
        }

        [ClientRpc]
        void OnCountdownTimerEnded_ClientRpc(FixedString128Bytes playerName)
        {
            string name = playerName.ToString();
            gameData.SetNewPlayerActive(name);
            gameData.StartTurn();

            // If it's the local player who just activated, force their input live
            // so a replay-race leaves them controllable.
            if (gameData.LocalPlayer != null && gameData.LocalPlayer.Name == name)
                EnsureLocalHumanCanMove();
        }
    }
}
