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
    /// Example: In HexRace with SpaceWeight=1, a player 4 crystals behind the leader
    /// gets their Space element raised by 4 levels, increasing their skimmer size.
    /// In CrystalCapture with TimeWeight=1, the losing player's speed increases instead.
    /// </summary>
    public class ElementalComebackSystem : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] SO_ElementalComebackProfile comebackProfile;
        [SerializeField] GameDataSO gameData;

        [Header("Scoring")]
        [Tooltip("Enable for games where lower score is better (e.g. race times)")]
        [SerializeField] bool useGolfRules;

        [Header("Update Settings")]
        [Tooltip("How often (in seconds) to recalculate comeback buffs")]
        [SerializeField] float updateInterval = 1f;

        [Header("Debug")]
        [SerializeField] bool debugLogging;

        static readonly Element[] AllElements =
            { Element.Mass, Element.Charge, Element.Space, Element.Time };

        // Baseline elemental levels captured at turn start (before any comeback adjustments)
        readonly Dictionary<string, float[]> _baselines = new();
        float _lastUpdateTime;
        bool _isActive;

        void OnEnable()
        {
            if (gameData == null) return;
            gameData.OnMiniGameTurnStarted.OnRaised += OnTurnStarted;
            gameData.OnMiniGameTurnEnd.OnRaised += OnTurnEnded;
            gameData.OnMiniGameEnd += OnGameEnded;
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
            if (comebackProfile == null) return;

            _isActive = true;
            _baselines.Clear();

            foreach (var player in gameData.Players)
            {
                var rs = GetResourceSystem(player);
                if (rs == null) continue;

                var vesselType = player.Vessel.VesselStatus.VesselType;
                var config = comebackProfile.GetConfig(vesselType);

                // Apply minigame-specific initial elemental values for this vessel
                ApplyInitialValues(rs, config);

                // Capture baseline levels after initialization
                var baseline = new float[AllElements.Length];
                for (int i = 0; i < AllElements.Length; i++)
                    baseline[i] = rs.GetNormalizedLevel(AllElements[i]);

                _baselines[player.Name] = baseline;
            }

            if (debugLogging)
                Debug.Log("[ElementalComebackSystem] Turn started. Baselines captured for " +
                          _baselines.Count + " players.");
        }

        void OnTurnEnded()
        {
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

            float leaderScore = GetLeaderScore();

            for (int p = 0; p < players.Count; p++)
            {
                var player = players[p];
                var rs = GetResourceSystem(player);
                if (rs == null) continue;
                if (!_baselines.TryGetValue(player.Name, out var baseline)) continue;

                float playerScore = GetPlayerScore(player);
                float scoreDiff = CalculateScoreDifference(leaderScore, playerScore);

                var vesselType = player.Vessel.VesselStatus.VesselType;
                var config = comebackProfile.GetConfig(vesselType);

                for (int i = 0; i < AllElements.Length; i++)
                {
                    var element = AllElements[i];
                    float weight = config.GetWeight(element);
                    // Each unit of score difference increases the element by 'weight' levels
                    float bonusLevels = scoreDiff * weight;
                    // Convert from integer levels to normalized (1 level = 0.1 normalized)
                    float targetNormalized = baseline[i] + (bonusLevels / 10f);

                    rs.SetElementLevel(element, targetNormalized);
                }

                if (debugLogging && scoreDiff > 0)
                    Debug.Log($"[ElementalComebackSystem] {player.Name} ({vesselType}): " +
                              $"scoreDiff={scoreDiff:F1}, " +
                              $"M={rs.GetLevel(Element.Mass)} C={rs.GetLevel(Element.Charge)} " +
                              $"S={rs.GetLevel(Element.Space)} T={rs.GetLevel(Element.Time)}");
            }
        }

        /// <summary>
        /// Set initial elemental values from the profile for this vessel type.
        /// These provide per-vessel per-minigame balancing (e.g., Squirrel starts with
        /// different Space level than Manta in HexRace).
        /// </summary>
        void ApplyInitialValues(ResourceSystem rs, SO_ElementalComebackProfile.VesselComebackConfig config)
        {
            for (int i = 0; i < AllElements.Length; i++)
            {
                float initialLevel = config.GetInitialLevel(AllElements[i]);
                // Convert from integer level (-5 to 15) to normalized (-0.5 to 1.5)
                rs.SetElementLevel(AllElements[i], initialLevel / 10f);
            }
        }

        float GetLeaderScore()
        {
            var stats = gameData.RoundStatsList;
            if (stats == null || stats.Count == 0) return 0f;

            float leader = stats[0].Score;
            for (int i = 1; i < stats.Count; i++)
            {
                float s = stats[i].Score;
                if (useGolfRules ? s < leader : s > leader)
                    leader = s;
            }
            return leader;
        }

        float GetPlayerScore(IPlayer player)
        {
            return gameData.TryGetRoundStats(player.Name, out var stats) ? stats.Score : 0f;
        }

        /// <summary>
        /// Returns a non-negative value representing how far behind this player is.
        /// 0 for the leader; positive for everyone else.
        /// </summary>
        float CalculateScoreDifference(float leaderScore, float playerScore)
        {
            if (useGolfRules)
                return Mathf.Max(0f, playerScore - leaderScore);
            else
                return Mathf.Max(0f, leaderScore - playerScore);
        }

        static ResourceSystem GetResourceSystem(IPlayer player)
        {
            return player?.Vessel?.VesselStatus?.ResourceSystem;
        }
    }
}
