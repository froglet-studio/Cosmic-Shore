using System.Collections;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using CosmicShore.Utility;
using CosmicShore.Core;

namespace CosmicShore.Game.Arcade
{
    public class DogFightController : MultiplayerDomainGamesController
    {
        [Header("Dog Fight")]
        [SerializeField] public DogFightCollisionTurnMonitor dogFightTurnMonitor;

        [Header("Missile Configuration")]
        [Tooltip("Index of the missile resource in the vessel's ResourceSystem.")]
        [SerializeField] int missileAmmoIndex = 2;

        [Tooltip("Set to 0 to disable missiles entirely (Dog Fight). Set > 0 for recharge rate per second (Missile Dog Fight).")]
        [SerializeField] float missileRechargeRate = 0f;

        [Tooltip("Initial missile ammo (0-1 normalized). 0 = empty, 1 = full.")]
        [SerializeField, Range(0f, 1f)] float initialMissileAmmo = 0f;

        private bool _finalResultsSent;
        private bool _missilesConfigured;

        public string WinnerName { get; private set; } = "";
        public bool ResultsReady { get; private set; } = false;

        protected override bool UseGolfRules => true;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            numberOfRounds = 1;
            numberOfTurnsPerRound = 1;
            _finalResultsSent = false;
            _missilesConfigured = false;

            if (gameData.OnMiniGameTurnStarted)
                gameData.OnMiniGameTurnStarted.OnRaised += OnTurnStartedConfigureMissiles;

            CSDebug.Log($"[DogFightController] HitsNeeded={dogFightTurnMonitor.HitsNeeded} " +
                      $"MissileRecharge={missileRechargeRate} InitialAmmo={initialMissileAmmo}");
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            if (gameData.OnMiniGameTurnStarted)
                gameData.OnMiniGameTurnStarted.OnRaised -= OnTurnStartedConfigureMissiles;
        }

        void OnTurnStartedConfigureMissiles()
        {
            if (_missilesConfigured) return;
            _missilesConfigured = true;

            // Delay 2 frames so ResourceSystem.Start() finishes resetting to prefab defaults first
            StartCoroutine(ConfigureMissilesDelayed());
        }

        IEnumerator ConfigureMissilesDelayed()
        {
            yield return null;
            yield return null;

            CSDebug.Log($"[DogFightController] ConfigureMissilesDelayed — Players={gameData.Players.Count}");

            foreach (var player in gameData.Players)
            {
                if (player?.Vessel == null)
                {
                    CSDebug.Log($"[DogFightController] Player '{player?.Name}' has no vessel, skipping");
                    continue;
                }
                var resourceSystem = player.Vessel.VesselStatus?.ResourceSystem;
                ConfigureMissiles(resourceSystem);
            }
        }

        void ConfigureMissiles(ResourceSystem resourceSystem)
        {
            if (resourceSystem == null)
            {
                CSDebug.LogWarning("[DogFightController] ConfigureMissiles: resourceSystem is null");
                return;
            }
            if (missileAmmoIndex >= resourceSystem.Resources.Count)
            {
                CSDebug.LogWarning($"[DogFightController] ConfigureMissiles: ammoIndex {missileAmmoIndex} >= Resources.Count {resourceSystem.Resources.Count}");
                return;
            }

            var missileResource = resourceSystem.Resources[missileAmmoIndex];
            float prevAmount = missileResource.CurrentAmount;
            float prevGainRate = missileResource.resourceGainRate;

            missileResource.resourceGainRate = missileRechargeRate;
            resourceSystem.SetResourceAmount(missileAmmoIndex, initialMissileAmmo);

            CSDebug.Log($"[DogFightController] Configured missiles: index={missileAmmoIndex} " +
                      $"ammo {prevAmount:F2}→{initialMissileAmmo:F2} " +
                      $"gainRate {prevGainRate:F3}→{missileRechargeRate:F3}/s " +
                      $"(resource='{missileResource.Name}')");
        }

        public void NotifyHit(string playerName, int hitCount)
        {
            if (!IsServer) return;
            var stats = gameData.RoundStatsList.FirstOrDefault(s => s.Name == playerName);
            if (stats != null) stats.DogFightHits = hitCount;
            NotifyHit_ClientRpc(playerName, hitCount);
        }

        public void ReportHitToServer(string playerName, int hitCount)
        {
            ReportHit_ServerRpc(playerName, hitCount);
        }

        [ServerRpc(RequireOwnership = false)]
        void ReportHit_ServerRpc(string playerName, int hitCount)
        {
            var stats = gameData.RoundStatsList.FirstOrDefault(s => s.Name == playerName);
            if (stats == null)
            {
                CSDebug.LogError($"[DogFightController] ServerRpc: no stats for '{playerName}'");
                return;
            }
            if (hitCount <= stats.DogFightHits) return;
            stats.DogFightHits = hitCount;
            NotifyHit_ClientRpc(playerName, hitCount);
        }

        [ClientRpc]
        void NotifyHit_ClientRpc(string playerName, int hitCount)
        {
            if (!gameData.TryGetRoundStats(playerName, out IRoundStats stats)) return;
            stats.DogFightHits = hitCount;
        }

        protected override void OnTurnEndedCustom()
        {
            base.OnTurnEndedCustom();
            if (!IsServer) return;
            if (_finalResultsSent) return;

            CalculateDogFightScores_Server();
            SyncDogFightResults_Authoritative();
            _finalResultsSent = true;
        }

        void CalculateDogFightScores_Server()
        {
            if (!dogFightTurnMonitor)
            {
                CSDebug.LogError("[DogFightController] DogFightTurnMonitor is null!");
                return;
            }

            int hitsNeeded = dogFightTurnMonitor.HitsNeeded;
            float currentTime = Time.time - gameData.TurnStartTime;

            string winnerName = "";
            foreach (var stats in gameData.RoundStatsList)
            {
                if (stats.DogFightHits >= hitsNeeded)
                {
                    winnerName = stats.Name;
                    break;
                }
            }

            CSDebug.Log($"[DogFightController] Calculating scores. Winner='{winnerName}' Time={currentTime:F2}s " +
                      $"Players=[{string.Join(", ", gameData.RoundStatsList.Select(s => $"{s.Name}:{s.DogFightHits}h"))}]");

            foreach (var stats in gameData.RoundStatsList)
            {
                if (stats.Name == winnerName)
                    stats.Score = currentTime;
                else
                    stats.Score = 99999f;
            }

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);
        }

        void SyncDogFightResults_Authoritative()
        {
            string winnerName = gameData.RoundStatsList.Count > 0
                ? gameData.RoundStatsList[0].Name
                : "";

            var list = gameData.RoundStatsList;
            int count = list.Count;

            var names      = new FixedString64Bytes[count];
            var scores     = new float[count];
            var collisions = new int[count];
            var domains    = new int[count];

            for (int i = 0; i < count; i++)
            {
                names[i]      = new FixedString64Bytes(list[i].Name);
                scores[i]     = list[i].Score;
                collisions[i] = list[i].DogFightHits;
                domains[i]    = (int)list[i].Domain;
            }

            SyncDogFightResults_ClientRpc(names, scores, collisions, domains,
                new FixedString64Bytes(winnerName));
        }

        [ClientRpc]
        void SyncDogFightResults_ClientRpc(
            FixedString64Bytes[] names,
            float[] scores,
            int[] collisions,
            int[] domains,
            FixedString64Bytes winnerName)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string n = names[i].ToString();
                var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == n);
                if (stat == null)
                {
                    CSDebug.LogError($"[DogFightController] Client could not match '{n}'. " +
                                   $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                    continue;
                }
                stat.Score        = scores[i];
                stat.DogFightHits = collisions[i];
                stat.Domain       = (Domains)domains[i];
            }

            WinnerName   = winnerName.ToString();
            ResultsReady = true;

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);

            CSDebug.Log($"[DogFightController] Client synced. Winner='{WinnerName}' " +
                      $"Order=[{string.Join(", ", gameData.RoundStatsList.Select(s => $"{s.Name}:{s.Score:F1}"))}]");

            gameData.InvokeWinnerCalculated();
            gameData.InvokeMiniGameEnd();
        }

        protected override void OnResetForReplayCustom()
        {
            base.OnResetForReplayCustom();
            _finalResultsSent = false;
            _missilesConfigured = false;
            WinnerName   = "";
            ResultsReady = false;

            if (dogFightTurnMonitor) dogFightTurnMonitor.ResetMonitor();

            foreach (var s in gameData.RoundStatsList)
            {
                s.DogFightHits = 0;
                s.Score = 0f;
            }

            // Re-configure missiles for replay (delayed to run after ResourceSystem.Reset)
            StartCoroutine(ConfigureMissilesDelayed());

            gameData.InvokeTurnStarted();
        }
    }
}
