using System.Collections.Generic;
using CosmicShore.Core;
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
    /// Operates only in the 0.0–1.5 normalized range (levels 0–15). The first 5 base pips
    /// (levels -5 to 0) are reserved for the overtake impact effect and are never touched here.
    /// </summary>
    public class ElementalComebackSystem : MonoBehaviour
    {
        /// <summary>
        /// Which stat to use when calculating who is ahead/behind.
        /// HexRace tracks elapsed time as Score (same for everyone) so use CrystalsCollected.
        /// CrystalCapture uses Score directly. AstroLeague uses GoalsScored.
        /// </summary>
        public enum ScoreDifferenceSource
        {
            Score,
            CrystalsCollected,
            Goals,
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

        [Header("Audio")]
        [Tooltip("Minimum seconds between comeback audio events for the same element. " +
                 "Prevents the sound firing every update tick while the buff is held.")]
        [SerializeField, Min(0f)] float comebackAudioCooldown = 3f;

        [Header("Debug")]
        [SerializeField] bool debugLogging;

        static readonly Element[] AllElements =
            { Element.Mass, Element.Charge, Element.Space, Element.Time };

        float _lastUpdateTime;
        bool _isActive;

        // Per-element last-played timestamp for the local player's comeback audio.
        // Index matches AllElements order: Mass=0, Charge=1, Space=2, Time=3.
        readonly float[] _lastComebackAudioTime = { -999f, -999f, -999f, -999f };

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
            gameData.OnMiniGameEnd.OnRaised += OnGameEnded;

            if (debugLogging)
                CSDebug.Log("[ElementalComebackSystem] Enabled and subscribed to game events.");
        }

        void OnDisable()
        {
            if (gameData == null) return;
            gameData.OnMiniGameTurnStarted.OnRaised -= OnTurnStarted;
            gameData.OnMiniGameTurnEnd.OnRaised -= OnTurnEnded;
            gameData.OnMiniGameEnd.OnRaised -= OnGameEnded;
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
            ResetComebackAudioTimestamps();

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

                if (debugLogging)
                    CSDebug.Log($"[ElementalComebackSystem] Initial levels for {player.Name} ({vesselType}): " +
                              $"M={rs.GetLevel(Element.Mass)} C={rs.GetLevel(Element.Charge)} " +
                              $"S={rs.GetLevel(Element.Space)} T={rs.GetLevel(Element.Time)}");
            }
        }

        void OnTurnEnded()
        {
            if (debugLogging && _isActive)
                CSDebug.Log("[ElementalComebackSystem] Turn ended. Deactivating.");
            Deactivate();
        }

        void OnGameEnded()
        {
            Deactivate();
        }

        void Deactivate()
        {
            if (!_isActive) return;
            _isActive = false;
            ClearAllComebackModifiers();
        }

        void ClearAllComebackModifiers()
        {
            var players = gameData.Players;
            if (players == null) return;
            foreach (var player in players)
                GetResourceSystem(player)?.ClearComebackModifiers();
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

                float playerValue = GetPlayerValue(player);
                float scoreDiff = CalculateScoreDifference(leaderValue, playerValue);

                var vesselType = player.Vessel.VesselStatus.VesselType;
                var config = comebackProfile.GetConfig(vesselType);

                bool isLocalPlayer = player.IsLocalUser;
                for (int i = 0; i < AllElements.Length; i++)
                {
                    var element = AllElements[i];
                    float weight = config.GetWeight(element);
                    float bonusLevels = scoreDiff * weight;

                    // Composited through the ResourceSystem's comeback-modifier layer instead
                    // of overwriting the base level — mid-turn crystal gains (AdjustLevel)
                    // persist underneath the comeback bonus instead of being erased each tick.
                    rs.SetComebackModifier(element, Mathf.Max(0f, bonusLevels / 10f));

                    // Fire comeback audio for the local player when a buff activates,
                    // gated by per-element cooldown so it doesn't fire every tick.
                    if (isLocalPlayer && bonusLevels > 0f)
                    {
                        float now = Time.unscaledTime;
                        if (now - _lastComebackAudioTime[i] >= comebackAudioCooldown)
                        {
                            _lastComebackAudioTime[i] = now;
                            AudioSystem.Instance?.PlayGameplaySFX(ComebackCategoryForElement(element));
                        }
                    }
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
                // Clamp initial values to 0.0–1.5 range
                float normalized = Mathf.Clamp(initialLevel / 10f, 0f, 1.5f);
                rs.SetElementLevel(AllElements[i], normalized);
            }
        }

        // ---------------------------------------------------------------
        // Value reading — uses the configured ScoreDifferenceSource
        // ---------------------------------------------------------------
        // Comeback buffs are now keyed off DOMAIN aggregates: a player on the
        // leading domain doesn't get a comeback buff even if they personally
        // contribute less than the team leader. The "deficit" each player
        // experiences is their team's deficit, not their individual one.

        float GetLeaderValue()
        {
            var list = gameData.RoundStatsList;
            if (list == null || list.Count == 0) return 0f;

            float leader = 0f;
            bool first = true;
            int dc = Mathf.Clamp(gameData.RequestedDomainCount, 1, GameDataSO.ActiveDomains.Length);
            for (int i = 0; i < dc; i++)
            {
                var d = GameDataSO.ActiveDomains[i];
                float v = ReadDomainValue(d);
                if (first || (IsHigherBetter() ? v > leader : v < leader))
                {
                    leader = v;
                    first = false;
                }
            }
            return first ? 0f : leader;
        }

        float GetPlayerValue(IPlayer player)
        {
            // A player's "value" for comeback purposes is their domain's aggregate.
            return player != null ? ReadDomainValue(player.Domain) : 0f;
        }

        float ReadDomainValue(Domains domain)
        {
            switch (differenceSource)
            {
                case ScoreDifferenceSource.CrystalsCollected:
                    return gameData.SumCrystalsCollectedByDomain(domain);
                case ScoreDifferenceSource.Goals:
                    return ScoringMetrics.SumByDomain(gameData, ScoringMetric.Goals, domain);
                case ScoreDifferenceSource.Score:
                    float sum = 0f;
                    var list = gameData.RoundStatsList;
                    for (int i = 0, count = list.Count; i < count; i++)
                    {
                        var s = list[i];
                        if (s != null && s.Domain == domain) sum += s.Score;
                    }
                    return sum;
                default:
                    return 0f;
            }
        }

        bool IsHigherBetter()
        {
            return differenceSource switch
            {
                ScoreDifferenceSource.CrystalsCollected => true,
                ScoreDifferenceSource.Goals => true,
                ScoreDifferenceSource.Score => !useGolfRules,
                _ => !useGolfRules
            };
        }

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

        // Reset audio timestamps so the sound can fire immediately at the start of each turn.
        void ResetComebackAudioTimestamps()
        {
            for (int i = 0; i < _lastComebackAudioTime.Length; i++)
                _lastComebackAudioTime[i] = -999f;
        }

        static GameplaySFXCategory ComebackCategoryForElement(Element element) => element switch
        {
            Element.Charge => GameplaySFXCategory.ComebackCharge,
            Element.Mass   => GameplaySFXCategory.ComebackMass,
            Element.Space  => GameplaySFXCategory.ComebackSpace,
            Element.Time   => GameplaySFXCategory.ComebackTime,
            _              => GameplaySFXCategory.ComebackCharge,
        };
    }
}
