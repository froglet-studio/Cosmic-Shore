using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Reflex.Attributes;
using UnityEngine;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using System.Linq;
namespace CosmicShore.Gameplay
{
    /// <summary>
    /// REQUIRED component of every party game: buffs trailing players by their team's score
    /// deficit behind first place. ALL FOUR elements rise EQUALLY - the per-game strength is
    /// `SO_ArcadeGame.ComebackRatePerScoreDeficit` (synced to every machine via
    /// GameDataSO.ComebackRatePerScoreDeficit): bonusLevels = deficit x rate. The comeback
    /// layer can never lift an element above level 10 (ResourceSystem.SustainedCeiling).
    ///
    /// Scene-authored instances keep their authored score-source settings; a scene without one
    /// gets it auto-created by MultiplayerMiniGameControllerBase (EnsureExists) with per-mode
    /// defaults. The optional comeback profile only seeds per-vessel INITIAL levels now - the
    /// old per-vessel/per-element weights are retired (equal-elements is the law).
    ///
    /// gameData arrives by one of two routes and NEITHER is guaranteed to beat OnEnable: Reflex
    /// injection for a scene-authored instance (populated after Awake), and an explicit Bind for
    /// an auto-created one (AddComponent runs OnEnable before EnsureExists can assign anything).
    /// Subscription is therefore attempted from OnEnable, Bind and Start alike, guarded by
    /// _subscribed - and only Start treats a still-null field as a fault. Getting this wrong is
    /// silent: an unsubscribed system never activates and applies no buff at all, in any mode.
    /// </summary>
    public class ElementalComebackSystem : MonoBehaviour
    {
        /// <summary>
        /// Which stat to use when calculating who is ahead/behind.
        /// SkimRace tracks elapsed time as Score (same for everyone) so use CrystalsCollected.
        /// Scurry also uses CrystalsCollected and Rampage PrismsDestroyed - in the
        /// finish-time-scored modes Score is only assigned at game end (winners a time,
        /// losers a sentinel), so the Score source would be dead during live play.
        /// AstroLeague uses GoalsScored.
        /// </summary>
        public enum ScoreDifferenceSource
        {
            Score,
            CrystalsCollected,
            Goals,
            PrismsDestroyed,
            PrismsRemaining,
            /// <summary>
            /// Wildlife Liberation's fauna kills. Domain-aggregated like every other source
            /// here - the mode is a domain race, so a player's deficit is their TEAM's deficit
            /// against the leading colour. (A per-player variant of this source existed while
            /// the mode was briefly a free-for-all and was removed with it.)
            /// </summary>
            LifeformsKilled,

            /// <summary>
            /// Dog Fight's weighted gunnery score. A team source like every entry above
            /// LifeformsKilled - Dog Fight pools points per domain - so the trailing SIDE gets
            /// the buff, not the trailing individual.
            /// </summary>
            CombatPoints,

            /// <summary>
            /// Joust's per-domain summed joust collisions. Joust's Score lands only at game end
            /// (winner a finish time, losers a sentinel - JoustScoringRuleSO.AssignScores), so
            /// the Score source would read a flat zero deficit for the whole match.
            /// </summary>
            Jousts,

            /// <summary>
            /// Hijack's per-domain summed prisms STOLEN. A team source like every entry above:
            /// the mode is a domain race and its Score lands only at game end (winner a finish
            /// time, losers a sentinel), so the Score source would read a flat zero deficit for
            /// the whole match. Worth naming separately from PrismsDestroyed even though both
            /// count prisms - nothing is destroyed in Hijack, so the destruction stat is a flat
            /// zero there and would silently disable the comeback layer.
            /// </summary>
            PrismsStolen,
        }

        [Header("Config")]
        [Tooltip("Optional: only per-vessel INITIAL levels are read from the profile now. The " +
                 "comeback strength itself comes from the game's ComebackRatePerScoreDeficit " +
                 "and applies to all four elements equally.")]
        [SerializeField] SO_ElementalComebackProfile comebackProfile;
        [Inject] GameDataSO gameData;

        /// <summary>
        /// Guarantees a party-game scene has the comeback system (the REQUIRED-component rule).
        /// A scene-authored instance is respected as-is (only its missing gameData is filled in);
        /// otherwise one is added to the host and configured with per-mode score-source defaults.
        /// </summary>
        public static ElementalComebackSystem EnsureExists(
            GameObject host, GameDataSO gameData, bool useGolfRules = false)
        {
            var existing = FindFirstObjectByType<ElementalComebackSystem>(FindObjectsInactive.Include);
            if (existing)
            {
                existing.Bind(gameData);
                return existing;
            }

            // AddComponent runs OnEnable SYNCHRONOUSLY, before this method can assign anything -
            // so configuration and the gameData handoff both happen after it, and OnEnable is a
            // deliberate no-op while gameData is still null. Bind() is what actually subscribes.
            // (The same trap is recorded in DomainFaunaBuffSystem.Update.)
            var system = host.AddComponent<ElementalComebackSystem>();
            system.differenceSource = DefaultSourceFor(gameData);
            system.useGolfRules = useGolfRules;
            system.Bind(gameData);

            CSDebug.Log($"[ElementalComebackSystem] Auto-created for {gameData?.GameMode} " +
                        $"(source={system.differenceSource}, rate={gameData?.ComebackRatePerScoreDeficit ?? 0f}).");
            return system;
        }

        /// <summary>
        /// The live score source for a mode. Every mode whose Score is assigned only at game end
        /// needs the stat it actually accumulates during play, or the deficit reads a flat zero
        /// for the whole match and the comeback layer silently does nothing.
        /// </summary>
        public static ScoreDifferenceSource DefaultSourceFor(GameDataSO gameData)
        {
            switch (gameData ? gameData.GameMode : GameModes.Random)
            {
                case GameModes.SkimRace: // Score is elapsed time - crystals are the honest stat
                case GameModes.Scurry: // Score lands only at game end (time/sentinel)
                    return ScoreDifferenceSource.CrystalsCollected;
                case GameModes.AstroLeague:
                    return ScoreDifferenceSource.Goals;
                case GameModes.ScarabScramble: // Score lands only at game end - hoop goals are the live stat
                    return ScoreDifferenceSource.Goals;
                case GameModes.BroodRush: // Score lands only at game end - broods are the live stat
                    return ScoreDifferenceSource.Goals;
                case GameModes.Rampage: // Score lands only at game end - destruction is the live stat
                case GameModes.PeelTheCage: // same: the race metric is hostile prisms destroyed
                case GameModes.Salvo:   // same: the Sparrow demolition race
                    return ScoreDifferenceSource.PrismsDestroyed;
                case GameModes.WildlifeLiberation: // Score lands only at game end - kills are the live stat
                    return ScoreDifferenceSource.LifeformsKilled;
                case GameModes.DogFight: // Score lands only at game end - gunnery is the live stat
                case GameModes.Bends:    // same shape: bends land as CombatPoints, Score at the end
                    return ScoreDifferenceSource.CombatPoints;
                case GameModes.Joust: // Score lands only at game end - jousts are the live stat
                    return ScoreDifferenceSource.Jousts;
                case GameModes.Hijack: // Score lands only at game end - steals are the live stat
                    return ScoreDifferenceSource.PrismsStolen;
                default:
                    // The legacy composite/time-scored modes (Cellular Duel, Wildlife Blitz co-op,
                    // Freestyle, 2v2) accumulate Score live via TimePlayedScoring, so Score is
                    // the honest source there.
                    return ScoreDifferenceSource.Score;
            }
        }

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
        bool _subscribed;

        /// <summary>
        /// True once gameData is bound AND the turn/game events are subscribed. An instance that
        /// reports false applies no buff at all, silently - which is exactly the state the
        /// AddComponent/OnEnable ordering used to leave every auto-created system in.
        /// </summary>
        public bool IsRunning => _subscribed;

        // Rising-edge tracker for the local player's "comeback system is on" toast - fires
        // once when their buff activates, re-arms when the deficit closes. Only modes whose
        // GameToastConfigSO authors ComebackActivated display it (e.g. Skim Race).
        bool _localComebackActive;

        // Per-element last-played timestamp for the local player's comeback audio.
        // Index matches AllElements order: Mass=0, Charge=1, Space=2, Time=3.
        readonly float[] _lastComebackAudioTime = { -999f, -999f, -999f, -999f };

        /// <summary>
        /// Hands this instance its GameDataSO and subscribes. Called by EnsureExists right after
        /// AddComponent (which already ran OnEnable with a null field) and for a scene-authored
        /// instance whose Reflex injection has not landed. Safe to call repeatedly.
        /// </summary>
        public void Bind(GameDataSO data)
        {
            if (!gameData) gameData = data;
            TrySubscribe();
        }

        // Idempotent - the three entry points (OnEnable, Bind, Start) all route through here and
        // any of them can be the one that wins, depending on how this instance came to exist.
        void TrySubscribe()
        {
            if (_subscribed || gameData == null) return;

            // Profile is optional (initial-levels only) - the system runs without one.
            gameData.OnMiniGameTurnStarted.OnRaised += OnTurnStarted;
            gameData.OnMiniGameTurnEnd.OnRaised += OnTurnEnded;
            gameData.OnMiniGameEnd.OnRaised += OnGameEnded;
            _subscribed = true;

            if (debugLogging)
                CSDebug.Log("[ElementalComebackSystem] Subscribed to game events.");
        }

        void Unsubscribe()
        {
            if (!_subscribed) return;
            _subscribed = false;
            if (gameData == null) return;
            gameData.OnMiniGameTurnStarted.OnRaised -= OnTurnStarted;
            gameData.OnMiniGameTurnEnd.OnRaised -= OnTurnEnded;
            gameData.OnMiniGameEnd.OnRaised -= OnGameEnded;
        }

        // Deliberately silent when gameData is null: an auto-created instance runs this INSIDE
        // AddComponent, before EnsureExists can hand over gameData, and a scene-authored one can
        // run it before Reflex injection lands. Both are covered by Bind/Start - the fail-loud
        // check lives in Start, where a null field is a genuine wiring fault.
        void OnEnable() => TrySubscribe();

        void Start()
        {
            TrySubscribe();
            if (gameData == null)
                CSDebug.LogError("[ElementalComebackSystem] GameDataSO is not assigned!");
        }

        void OnDisable() => Unsubscribe();

        void OnTurnStarted()
        {
            if (debugLogging)
                CSDebug.Log($"[ElementalComebackSystem] OnTurnStarted fired. " +
                          $"Rate={gameData.ComebackRatePerScoreDeficit}, " +
                          $"Players={gameData.Players?.Count ?? 0}, " +
                          $"Source={differenceSource}");

            _isActive = true;
            _localComebackActive = false;
            ResetComebackAudioTimestamps();

            if (comebackProfile == null) return; // profile only seeds optional initial levels

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
            if (!_isActive) return;

            if (Time.time - _lastUpdateTime < updateInterval) return;

            _lastUpdateTime = Time.time;
            ApplyComebackBuffs();
        }

        void ApplyComebackBuffs()
        {
            var players = gameData.Players;
            if (players == null || players.Count < 2) return;

            float leaderValue = GetLeaderValue();
            float rate = gameData.ComebackRatePerScoreDeficit;
            if (rate <= 0f) return; // this game opted out of comeback

            for (int p = 0; p < players.Count; p++)
            {
                var player = players[p];
                var rs = GetResourceSystem(player);
                if (rs == null) continue;

                float playerValue = GetPlayerValue(player);
                float scoreDiff = CalculateScoreDifference(leaderValue, playerValue);

                // ALL FOUR elements rise EQUALLY - the game's authored rate is the only dial.
                // The ResourceSystem caps the comeback contribution so it can never lift an
                // element above level 10 (earned progression alone reaches the overcharge band).
                float bonusLevels = scoreDiff * rate;
                float normalizedBonus = Mathf.Max(0f, bonusLevels / 10f);

                bool isLocalPlayer = player.IsLocalUser;
                if (isLocalPlayer)
                    UpdateLocalComebackToast(player, bonusLevels);

                for (int i = 0; i < AllElements.Length; i++)
                {
                    var element = AllElements[i];

                    // Composited through the ResourceSystem's comeback-modifier layer instead
                    // of overwriting the base level - mid-turn crystal gains (AdjustLevel)
                    // persist underneath the comeback bonus instead of being erased each tick.
                    rs.SetComebackModifier(element, normalizedBonus);

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
                    CSDebug.Log($"[ElementalComebackSystem] {player.Name}: " +
                              $"value={playerValue:F1}, leader={leaderValue:F1}, diff={scoreDiff:F1}, " +
                              $"bonus={bonusLevels:F1} → " +
                              $"M={rs.GetLevel(Element.Mass)} C={rs.GetLevel(Element.Charge)} " +
                              $"S={rs.GetLevel(Element.Space)} T={rs.GetLevel(Element.Time)}");
            }
        }

        /// <summary>
        /// Posts the ComebackActivated toast situation on the rising edge of the LOCAL
        /// player's comeback buff, and re-arms once the buff drops back to zero. Whether it
        /// displays is up to the current mode's toast config (unauthored = silent).
        /// </summary>
        void UpdateLocalComebackToast(IPlayer player, float bonusLevels)
        {
            if (bonusLevels > 0f)
            {
                if (_localComebackActive) return;
                _localComebackActive = true;
                GameToastAPI.Post(GameToastSituation.ComebackActivated, player.Domain, player.Name);
            }
            else
            {
                _localComebackActive = false;
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
        // Value reading - uses the configured ScoreDifferenceSource
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
                case ScoreDifferenceSource.PrismsRemaining:
                    return ScoringMetrics.SumByDomain(gameData, ScoringMetric.PrismsRemaining, domain);
                case ScoreDifferenceSource.PrismsDestroyed:
                    return ScoringMetrics.SumByDomain(gameData, ScoringMetric.PrismsDestroyed, domain);
                case ScoreDifferenceSource.CombatPoints:
                    return ScoringMetrics.SumByDomain(gameData, ScoringMetric.CombatPoints, domain);
                case ScoreDifferenceSource.LifeformsKilled:
                    return ScoringMetrics.SumByDomain(gameData, ScoringMetric.LifeformsKilled, domain);
                case ScoreDifferenceSource.Jousts:
                    return ScoringMetrics.SumByDomain(gameData, ScoringMetric.Jousts, domain);
                case ScoreDifferenceSource.PrismsStolen:
                    return ScoringMetrics.SumByDomain(gameData, ScoringMetric.PrismsStolen, domain);
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
                ScoreDifferenceSource.PrismsDestroyed => true,
                ScoreDifferenceSource.PrismsRemaining => true,
                ScoreDifferenceSource.LifeformsKilled => true,
                ScoreDifferenceSource.CombatPoints => true,
                ScoreDifferenceSource.Jousts => true,
                ScoreDifferenceSource.PrismsStolen => true,
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
