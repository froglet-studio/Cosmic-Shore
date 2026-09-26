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
    /// <para><b>The deficit IS the score.</b> A domain's comeback value is read from
    /// <see cref="ScoringRuleSO.DomainValue"/> on the mode's own published rule
    /// (<see cref="GameDataSO.ScoringRule"/>) - the one function the HUD's domain boxes, the end
    /// condition, the winner and the placement order all read. There is no authored "which stat
    /// does the comeback track" setting any more, so the comeback cannot disagree with the
    /// score: a mode that scores gunnery catches up on gunnery, a lead-runner race catches up on
    /// the lead runner, and a mode that folds two stats (Undertow) catches up on both. The rate
    /// is calibrated in the metric's own units for the same reason.</para>
    ///
    /// <para>That setting existed until 2026-09 (<c>ScoreDifferenceSource</c>) and was the bug
    /// it describes: a scene-authored instance kept its authored value, most mode scenes were
    /// cloned from a donor, and eight shipped reading the DONOR's stat - The Bends caught up on
    /// prisms destroyed at 4.0 levels per prism, Wrecking Ball on goals it never scores, Skein
    /// on prisms stolen. Nothing could see it, because the two answers lived in two places.</para>
    ///
    /// <para>The legacy modes that publish no rule (Cellular Duel, co-op Wildlife Blitz,
    /// Freestyle, 2v2) accumulate <c>Score</c> live through TimePlayedScoring, so the fallback
    /// sums Score per domain, lower-is-better when the controller runs golf rules. The golf
    /// flag is handed over by <see cref="EnsureExists"/> on every spawn and is not serialized,
    /// for the same reason the source is gone.</para>
    ///
    /// <para>The optional comeback profile only seeds per-vessel INITIAL levels now - the old
    /// per-vessel/per-element weights are retired (equal-elements is the law).</para>
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
        [Header("Config")]
        [Tooltip("Optional: only per-vessel INITIAL levels are read from the profile now. The " +
                 "comeback strength itself comes from the game's ComebackRatePerScoreDeficit " +
                 "and applies to all four elements equally.")]
        [SerializeField] SO_ElementalComebackProfile comebackProfile;
        [Inject] GameDataSO gameData;

        /// <summary>
        /// Guarantees a party-game scene has the comeback system (the REQUIRED-component rule).
        /// A scene-authored instance is reused; otherwise one is added to the host. Either way the
        /// controller's golf flag is handed over here - it only matters for the legacy rule-less
        /// fallback, and it is deliberately not something a scene can author.
        /// </summary>
        public static ElementalComebackSystem EnsureExists(
            GameObject host, GameDataSO gameData, bool useGolfRules = false)
        {
            var existing = FindFirstObjectByType<ElementalComebackSystem>(FindObjectsInactive.Include);
            if (existing)
            {
                existing.useGolfRules = useGolfRules;
                existing.Bind(gameData);
                return existing;
            }

            // AddComponent runs OnEnable SYNCHRONOUSLY, before this method can assign anything -
            // so configuration and the gameData handoff both happen after it, and OnEnable is a
            // deliberate no-op while gameData is still null. Bind() is what actually subscribes.
            // (The same trap is recorded in DomainFaunaBuffSystem.Update.)
            var system = host.AddComponent<ElementalComebackSystem>();
            system.useGolfRules = useGolfRules;
            system.Bind(gameData);

            CSDebug.LogVerbose(CSLogChannel.ArcadeMatch, $"[ElementalComebackSystem] Auto-created for {gameData?.GameMode} " +
                        $"(rate={gameData?.ComebackRatePerScoreDeficit ?? 0f}).");
            return system;
        }

        // Legacy rule-less fallback only: lower summed Score is better. Handed over by
        // EnsureExists from the controller on every spawn - never authored (see the class doc).
        [System.NonSerialized] bool useGolfRules;

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
                CSDebug.LogVerbose(CSLogChannel.ArcadeMatch, "[ElementalComebackSystem] Subscribed to game events.");
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
            if (debugLogging && CSDebug.IsVerbose(CSLogChannel.ArcadeMatch))
                CSDebug.LogVerbose(CSLogChannel.ArcadeMatch, $"[ElementalComebackSystem] OnTurnStarted fired. " +
                          $"Rate={gameData.ComebackRatePerScoreDeficit}, " +
                          $"Players={gameData.Players?.Count ?? 0}, " +
                          $"Rule={(gameData.ScoringRule ? gameData.ScoringRule.name : "<none - legacy Score>")}");

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

                if (debugLogging && CSDebug.IsVerbose(CSLogChannel.ArcadeMatch))
                    CSDebug.LogVerbose(CSLogChannel.ArcadeMatch, $"[ElementalComebackSystem] Initial levels for {player.Name} ({vesselType}): " +
                              $"M={rs.GetLevel(Element.Mass)} C={rs.GetLevel(Element.Charge)} " +
                              $"S={rs.GetLevel(Element.Space)} T={rs.GetLevel(Element.Time)}");
            }
        }

        void OnTurnEnded()
        {
            if (debugLogging && _isActive)
                CSDebug.LogVerbose(CSLogChannel.ArcadeMatch, "[ElementalComebackSystem] Turn ended. Deactivating.");
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

                if (debugLogging && CSDebug.IsVerbose(CSLogChannel.ArcadeMatch))
                    CSDebug.LogVerbose(CSLogChannel.ArcadeMatch, $"[ElementalComebackSystem] {player.Name}: " +
                              $"value={playerValue:F1}, leader={leaderValue:F1}, diff={scoreDiff:F1}, " +
                              $"bonus={bonusLevels:F1} -> " +
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
        // Value reading - the mode's own score, per DOMAIN
        // ---------------------------------------------------------------
        // Comeback buffs are keyed off DOMAIN values: a player on the leading domain doesn't
        // get a comeback buff even if they personally contribute less than the team leader.
        // The "deficit" each player experiences is their team's deficit, not their individual one.

        float GetLeaderValue()
        {
            var list = gameData.RoundStatsList;
            if (list == null || list.Count == 0) return 0f;

            float leader = 0f;
            bool first = true;
            bool higherIsBetter = IsHigherBetter(gameData, useGolfRules);
            int dc = Mathf.Clamp(gameData.RequestedDomainCount, 1, GameDataSO.ActiveDomains.Length);
            for (int i = 0; i < dc; i++)
            {
                var d = GameDataSO.ActiveDomains[i];
                float v = DomainScore(gameData, d);
                if (first || (higherIsBetter ? v > leader : v < leader))
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
            return player != null ? DomainScore(gameData, player.Domain) : 0f;
        }

        /// <summary>
        /// A domain's comeback value: EXACTLY the mode's score for that domain
        /// (<see cref="ScoringRuleSO.DomainValue"/>), or the summed live Score for a legacy mode
        /// that publishes no rule. There is deliberately no third source.
        /// </summary>
        public static float DomainScore(GameDataSO gameData, Domains domain)
        {
            if (gameData == null) return 0f;

            var rule = gameData.ScoringRule;
            if (rule) return rule.DomainValue(gameData, domain);

            float sum = 0f;
            var list = gameData.RoundStatsList;
            if (list == null) return 0f;
            for (int i = 0, count = list.Count; i < count; i++)
            {
                var s = list[i];
                if (s != null && s.Domain == domain) sum += s.Score;
            }
            return sum;
        }

        /// <summary>
        /// A rule's <see cref="ScoringRuleSO.DomainValue"/> is always higher-is-better during
        /// play - it is what <see cref="ScoringRuleSO.ResolveWinner"/> maximizes. A rule's
        /// <c>GolfRules</c> flag is about the FINAL Score (finish time / sentinel), which is not
        /// what the comeback reads. Only the legacy Score fallback honours the golf flag.
        /// </summary>
        public static bool IsHigherBetter(GameDataSO gameData, bool legacyGolfRules) =>
            (gameData != null && gameData.ScoringRule) || !legacyGolfRules;

        bool IsHigherBetter() => IsHigherBetter(gameData, useGolfRules);

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
