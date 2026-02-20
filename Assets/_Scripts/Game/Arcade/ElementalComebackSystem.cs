using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Game;
using CosmicShore.Soap;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    /// <summary>
    /// Applies elemental buffs to losing players based on their score difference from the leader.
    /// Attach to minigame scene alongside the minigame controller. Assign a comeback profile
    /// to configure per-vessel, per-element weights.
    ///
    /// Example: In HexRace with SpaceWeight=1 and source=CrystalsCollected,
    /// a player 4 crystals behind the leader gets Space element +4, growing their skimmer.
    /// In CrystalCapture with TimeWeight=1 and source=Score, the losing player speeds up.
    /// </summary>
    public class ElementalComebackSystem : MonoBehaviour
    {
        /// <summary>
        /// Which stat to use when calculating who is ahead/behind.
        /// HexRace tracks elapsed time as Score (same for everyone) so use CrystalsCollected.
        /// CrystalCapture uses Score directly.
        /// </summary>
        public enum ScoreDifferenceSource
        {
            Score,
            CrystalsCollected,
        }

        [Header("Config")]
        [SerializeField] SO_ElementalComebackProfile comebackProfile;
        [SerializeField] GameDataSO gameData;

        [Header("Scoring")]
        [Tooltip("Which stat drives the comeback calculation")]
        [SerializeField] ScoreDifferenceSource differenceSource = ScoreDifferenceSource.CrystalsCollected;
        [Tooltip("For Score source: enable when lower score is better (e.g. race times)")]
        [SerializeField] bool useGolfRules;

        [Header("Update Settings")]
        [Tooltip("How often (in seconds) to recalculate comeback buffs")]
        [SerializeField] float updateInterval = 1f;

        [Header("Debug")]
        [SerializeField] bool debugLogging;

        static readonly Element[] AllElements =
            { Element.Mass, Element.Charge, Element.Space, Element.Time };

        readonly Dictionary<string, float[]> _baselines = new();
        float _lastUpdateTime;
        bool _isActive;

        void OnEnable()
        {
            if (gameData == null)
            {
                Debug.LogError("[ElementalComebackSystem] GameDataSO is not assigned!");
                return;
            }
            if (comebackProfile == null)
                Debug.LogWarning("[ElementalComebackSystem] No comeback profile assigned. System will be inactive.");

            gameData.OnMiniGameTurnStarted.OnRaised += OnTurnStarted;
            gameData.OnMiniGameTurnEnd.OnRaised += OnTurnEnded;
            gameData.OnMiniGameEnd += OnGameEnded;

            if (debugLogging)
                Debug.Log("[ElementalComebackSystem] Enabled and subscribed to game events.");
        }

        void OnDisable()
        {
            if (gameData == null) return;
            gameData.OnMiniGameTurnStarted.OnRaised -= OnTurnStarted;
            gameData.OnMiniGameTurnEnd.OnRaised -= OnTurnEnded;
            gameData.OnMiniGameEnd -= OnGameEnded;
        }

        void OnTurnStarted()
        {
            if (debugLogging)
                Debug.Log($"[ElementalComebackSystem] OnTurnStarted fired. " +
                          $"Profile={(comebackProfile != null ? comebackProfile.name : "NULL")}, " +
                          $"Players={gameData.Players?.Count ?? 0}, " +
                          $"Source={differenceSource}");

            if (comebackProfile == null) return;

            _isActive = true;
            _baselines.Clear();

            foreach (var player in gameData.Players)
            {
                var rs = GetResourceSystem(player);
                if (rs == null)
                {
                    if (debugLogging)
                        Debug.LogWarning($"[ElementalComebackSystem] Player '{player?.Name}' has no ResourceSystem. Skipping.");
                    continue;
                }

                var vesselType = player.Vessel.VesselStatus.VesselType;
                var config = comebackProfile.GetConfig(vesselType);

                ApplyInitialValues(rs, config);

                var baseline = new float[AllElements.Length];
                for (int i = 0; i < AllElements.Length; i++)
                    baseline[i] = rs.GetNormalizedLevel(AllElements[i]);

                _baselines[player.Name] = baseline;

                if (debugLogging)
                    Debug.Log($"[ElementalComebackSystem] Baseline for {player.Name} ({vesselType}): " +
                              $"M={rs.GetLevel(Element.Mass)} C={rs.GetLevel(Element.Charge)} " +
                              $"S={rs.GetLevel(Element.Space)} T={rs.GetLevel(Element.Time)}");
            }
        }

        void OnTurnEnded()
        {
            if (debugLogging && _isActive)
                Debug.Log("[ElementalComebackSystem] Turn ended. Deactivating.");
            _isActive = false;
        }

        void OnGameEnded()
        {
            _isActive = false;
        }

        void Update()
        {
            if (!_isActive || comebackProfile == null) return;
            if (Time.time - _lastUpdateTime < updateInterval) return;

            _lastUpdateTime = Time.time;
            ApplyComebackBuffs();
        }

        void ApplyComebackBuffs()
        {
            var players = gameData.Players;
            if (players == null || players.Count < 2) return;

            float leaderValue = GetLeaderValue();

            for (int p = 0; p < players.Count; p++)
            {
                var player = players[p];
                var rs = GetResourceSystem(player);
                if (rs == null) continue;
                if (!_baselines.TryGetValue(player.Name, out var baseline)) continue;

                float playerValue = GetPlayerValue(player);
                float scoreDiff = CalculateScoreDifference(leaderValue, playerValue);

                var vesselType = player.Vessel.VesselStatus.VesselType;
                var config = comebackProfile.GetConfig(vesselType);

                for (int i = 0; i < AllElements.Length; i++)
                {
                    var element = AllElements[i];
                    float weight = config.GetWeight(element);
                    float bonusLevels = scoreDiff * weight;
                    float targetNormalized = baseline[i] + (bonusLevels / 10f);

                    rs.SetElementLevel(element, targetNormalized);
                }

                if (debugLogging)
                    Debug.Log($"[ElementalComebackSystem] {player.Name} ({vesselType}): " +
                              $"value={playerValue:F1}, leader={leaderValue:F1}, diff={scoreDiff:F1} → " +
                              $"M={rs.GetLevel(Element.Mass)} C={rs.GetLevel(Element.Charge)} " +
                              $"S={rs.GetLevel(Element.Space)} T={rs.GetLevel(Element.Time)}");
            }
        }

        void ApplyInitialValues(ResourceSystem rs, SO_ElementalComebackProfile.VesselComebackConfig config)
        {
            for (int i = 0; i < AllElements.Length; i++)
            {
                float initialLevel = config.GetInitialLevel(AllElements[i]);
                rs.SetElementLevel(AllElements[i], initialLevel / 10f);
            }
        }

        // ---------------------------------------------------------------
        // Value reading — uses the configured ScoreDifferenceSource
        // ---------------------------------------------------------------

        float GetLeaderValue()
        {
            var stats = gameData.RoundStatsList;
            if (stats == null || stats.Count == 0) return 0f;

            float leader = ReadValue(stats[0]);
            for (int i = 1; i < stats.Count; i++)
            {
                float v = ReadValue(stats[i]);
                if (IsHigherBetter() ? v > leader : v < leader)
                    leader = v;
            }
            return leader;
        }

        float GetPlayerValue(IPlayer player)
        {
            return gameData.TryGetRoundStats(player.Name, out var stats) ? ReadValue(stats) : 0f;
        }

        float ReadValue(IRoundStats stats)
        {
            return differenceSource switch
            {
                ScoreDifferenceSource.CrystalsCollected => stats.CrystalsCollected,
                ScoreDifferenceSource.Score => stats.Score,
                _ => stats.Score
            };
        }

        /// <summary>
        /// For CrystalsCollected the leader has MORE crystals (higher is better).
        /// For Score it depends on useGolfRules (golf = lower is better).
        /// </summary>
        bool IsHigherBetter()
        {
            return differenceSource switch
            {
                ScoreDifferenceSource.CrystalsCollected => true,
                ScoreDifferenceSource.Score => !useGolfRules,
                _ => !useGolfRules
            };
        }

        /// <summary>
        /// Returns a non-negative value representing how far behind this player is.
        /// 0 for the leader; positive for everyone else.
        /// </summary>
        float CalculateScoreDifference(float leaderValue, float playerValue)
        {
            return IsHigherBetter()
                ? Mathf.Max(0f, leaderValue - playerValue)
                : Mathf.Max(0f, playerValue - leaderValue);
        }

        static ResourceSystem GetResourceSystem(IPlayer player)
        {
            return player?.Vessel?.VesselStatus?.ResourceSystem;
        }
    }
}
