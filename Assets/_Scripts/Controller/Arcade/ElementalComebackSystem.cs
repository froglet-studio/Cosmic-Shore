using System;
using System.Collections.Generic;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Reflex.Attributes;
using UnityEngine;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using System.Linq;
namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Applies elemental buffs to losing players based on their score difference from the leader.
    /// Attach to minigame scene alongside the minigame controller. Assign a comeback profile
    /// to configure per-vessel, per-element weights.
    ///
    /// Example: In HexRace with SpaceWeight=1 and source=CrystalsCollected,
    /// a player 4 crystals behind the leader gets Space element +4, growing their skimmer.
    /// In CrystalCapture with TimeWeight=1 and source=Score, the losing player speeds up.
    ///
    /// Also detects overtake events: when a player who was leading gets overtaken,
    /// all their elemental values slam to -5 and gradually recover to 0.
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
        [Inject] GameDataSO gameData;

        [Header("Scoring")]
        [Tooltip("Which stat drives the comeback calculation")]
        [SerializeField] ScoreDifferenceSource differenceSource = ScoreDifferenceSource.CrystalsCollected;
        [Tooltip("For Score source: enable when lower score is better (e.g. race times)")]
        [SerializeField] bool useGolfRules;

        [Header("Update Settings")]
        [Tooltip("How often (in seconds) to recalculate comeback buffs")]
        [SerializeField] float updateInterval = 1f;

        [Header("Overtake Penalty")]
        [Tooltip("Enable the overtake penalty system")]
        [SerializeField] bool enableOvertakePenalty = true;
        [Tooltip("Normalized level to slam elements to on overtake (-0.5 = level -5)")]
        [SerializeField] float overtakePenaltyLevel = -0.5f;
        [Tooltip("Seconds to recover from penalty back to baseline (0)")]
        [SerializeField] float overtakeRecoveryDuration = 3f;

        [Header("Debug")]
        [SerializeField] bool debugLogging;

        /// <summary>
        /// Fired when a player gets overtaken. String parameter is the player name.
        /// HUD systems can subscribe to trigger visual juice.
        /// </summary>
        public event Action<string> OnPlayerOvertaken;
        public event Action<string> OnPlayerOvertakeRecovered;

        /// <summary>Static events for systems without a direct reference (e.g. SilhouetteController).</summary>
        public static event Action<string> OnOvertakePenaltyApplied;
        public static event Action<string> OnOvertakePenaltyRecovered;

        static readonly Element[] AllElements =
            { Element.Mass, Element.Charge, Element.Space, Element.Time };

        readonly Dictionary<string, float[]> _baselines = new();
        float _lastUpdateTime;
        bool _isActive;

        // Overtake tracking
        string _currentLeaderName;
        readonly Dictionary<string, float> _overtakePenaltyTimers = new();

        void OnEnable()
        {
            if (gameData == null)
            {
                CSDebug.LogError("[ElementalComebackSystem] GameDataSO is not assigned!");
                return;
            }
            if (comebackProfile == null)
                CSDebug.LogWarning("[ElementalComebackSystem] No comeback profile assigned. System will be inactive.");

            gameData.OnMiniGameTurnStarted.OnRaised += OnTurnStarted;
            gameData.OnMiniGameTurnEnd.OnRaised += OnTurnEnded;
            gameData.OnMiniGameEnd += OnGameEnded;

            if (debugLogging)
                CSDebug.Log("[ElementalComebackSystem] Enabled and subscribed to game events.");
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
                CSDebug.Log($"[ElementalComebackSystem] OnTurnStarted fired. " +
                          $"Profile={(comebackProfile != null ? comebackProfile.name : "NULL")}, " +
                          $"Players={gameData.Players?.Count ?? 0}, " +
                          $"Source={differenceSource}");

            if (comebackProfile == null) return;

            _isActive = true;
            _baselines.Clear();
            _overtakePenaltyTimers.Clear();
            _currentLeaderName = null;

            foreach (var player in gameData.Players)
            {
                var rs = GetResourceSystem(player);
                if (rs == null)
                {
                    if (debugLogging)
                        CSDebug.LogWarning($"[ElementalComebackSystem] Player '{player?.Name}' has no ResourceSystem. Skipping.");
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
                    CSDebug.Log($"[ElementalComebackSystem] Baseline for {player.Name} ({vesselType}): " +
                              $"M={rs.GetLevel(Element.Mass)} C={rs.GetLevel(Element.Charge)} " +
                              $"S={rs.GetLevel(Element.Space)} T={rs.GetLevel(Element.Time)}");
            }
        }

        void OnTurnEnded()
        {
            if (debugLogging && _isActive)
                CSDebug.Log("[ElementalComebackSystem] Turn ended. Deactivating.");
            _isActive = false;
        }

        void OnGameEnded()
        {
            _isActive = false;
        }

        void Update()
        {
            if (!_isActive || comebackProfile == null) return;

            // Tick overtake penalty recovery every frame for smooth interpolation
            if (enableOvertakePenalty)
                TickOvertakeRecovery();

            // Check for overtake every frame so it registers instantly
            if (enableOvertakePenalty)
                CheckForOvertake();

            if (Time.time - _lastUpdateTime < updateInterval) return;

            _lastUpdateTime = Time.time;
            ApplyComebackBuffs();
        }

        void CheckForOvertake()
        {
            var players = gameData.Players;
            if (players == null || players.Count < 2) return;

            float leaderValue = GetLeaderValue();
            string newLeaderName = FindLeaderName(leaderValue);

            if (_currentLeaderName != null
                && newLeaderName != null
                && _currentLeaderName != newLeaderName)
            {
                ApplyOvertakePenalty(_currentLeaderName);
            }

            _currentLeaderName = newLeaderName;
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

                // Skip normal comeback buff while player is recovering from overtake penalty
                if (_overtakePenaltyTimers.ContainsKey(player.Name))
                    continue;

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
                    CSDebug.Log($"[ElementalComebackSystem] {player.Name} ({vesselType}): " +
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

        // ---------------------------------------------------------------
        // Overtake penalty — slam elements to -5, recover to 0
        // ---------------------------------------------------------------

        string FindLeaderName(float leaderValue)
        {
            var players = gameData.Players;
            if (players == null) return null;

            for (int i = 0; i < players.Count; i++)
            {
                float v = GetPlayerValue(players[i]);
                if (Mathf.Approximately(v, leaderValue))
                    return players[i].Name;
            }
            return null;
        }

        void ApplyOvertakePenalty(string playerName)
        {
            var players = gameData.Players;
            if (players == null) return;

            IPlayer target = null;
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].Name == playerName)
                {
                    target = players[i];
                    break;
                }
            }

            if (target == null) return;

            var rs = GetResourceSystem(target);
            if (rs == null) return;

            // Slam all elements to penalty level (-5)
            for (int i = 0; i < AllElements.Length; i++)
                rs.SetElementLevel(AllElements[i], overtakePenaltyLevel);

            // Start recovery timer
            _overtakePenaltyTimers[playerName] = 0f;

            if (debugLogging)
                CSDebug.Log($"[ElementalComebackSystem] OVERTAKE PENALTY applied to {playerName}! " +
                          $"All elements slammed to {overtakePenaltyLevel * 10f:F0}");

            OnPlayerOvertaken?.Invoke(playerName);
            OnOvertakePenaltyApplied?.Invoke(playerName);
        }

        void TickOvertakeRecovery()
        {
            if (_overtakePenaltyTimers.Count == 0) return;

            var players = gameData.Players;
            if (players == null) return;

            // Collect completed recoveries to remove after iteration
            List<string> completed = null;

            foreach (var kvp in _overtakePenaltyTimers)
            {
                string playerName = kvp.Key;
                float elapsed = kvp.Value + Time.deltaTime;

                IPlayer target = null;
                for (int i = 0; i < players.Count; i++)
                {
                    if (players[i].Name == playerName)
                    {
                        target = players[i];
                        break;
                    }
                }

                if (target == null) continue;

                var rs = GetResourceSystem(target);
                if (rs == null) continue;

                float t = Mathf.Clamp01(elapsed / overtakeRecoveryDuration);
                // Lerp from penalty level to 0 (baseline)
                float currentLevel = Mathf.Lerp(overtakePenaltyLevel, 0f, t);

                for (int i = 0; i < AllElements.Length; i++)
                    rs.SetElementLevel(AllElements[i], currentLevel);

                if (t >= 1f)
                {
                    completed ??= new List<string>();
                    completed.Add(playerName);

                    OnPlayerOvertakeRecovered?.Invoke(playerName);
                    OnOvertakePenaltyRecovered?.Invoke(playerName);

                    if (debugLogging)
                        CSDebug.Log($"[ElementalComebackSystem] {playerName} recovered from overtake penalty.");
                }
            }

            // Update timers (can't modify during foreach, so rebuild)
            var keys = new List<string>(_overtakePenaltyTimers.Keys);
            foreach (var key in keys)
            {
                if (completed != null && completed.Contains(key))
                    _overtakePenaltyTimers.Remove(key);
                else
                    _overtakePenaltyTimers[key] = _overtakePenaltyTimers[key] + Time.deltaTime;
            }
        }

        static ResourceSystem GetResourceSystem(IPlayer player)
        {
            return player?.Vessel?.VesselStatus?.ResourceSystem;
        }
    }
}
