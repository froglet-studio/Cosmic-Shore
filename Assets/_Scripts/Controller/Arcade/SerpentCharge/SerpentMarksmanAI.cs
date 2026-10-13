using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Serpent Charge's AI: every autopiloted Serpent becomes a MARKSMAN that holds its scope,
    /// plans the fattest firing line it can bring to bear, steers its nose down it, and fires the
    /// moment the rifle is ready and the live line is worth the cooldown.
    ///
    /// <para><b>Why a mode needs this at all.</b> <see cref="AIPilot"/> writes a stick and a
    /// throttle and nothing else. Its generic ability cycler toggles the Serpent's cloak on a timer
    /// and never touches the scope, and the rifle only fires while the scope is up
    /// (<see cref="SniperShotActionExecutor"/>'s gate). Without this driver every AI Serpent would
    /// fly the whole match unable to score anything but what its hull rams - an opponent that
    /// cannot play, which the Tollway rule calls a defect.</para>
    ///
    /// <para><b>What makes it hard to beat.</b> It asks the SAME question the round answers. A
    /// candidate line is scored with <see cref="PrismSpatialIndex.QueryCone"/> using the rifle's own
    /// range, half-angle and radius floor, the rifle's own target filter (never its own domain;
    /// super-shielded mass only once Charge 5 "Pierce" is up), and the round's own reach rule for
    /// pilots (a strip reaches only as far as the farthest prism the round kills). So a line it
    /// plans is a line it will actually be paid for. It samples aim points from the densest
    /// hostile stand (the same <see cref="Cell.GetExplosionTarget"/> question the objective arrow
    /// asks) and from the mass ahead of its nose, weighs each by how far it must turn to bring it
    /// to bear, and - from intensity 2 - hunts rival PILOTS, which the rule prices as a stand of
    /// forest. On a long cooldown it detours to Charge crystals, which shorten the next one.</para>
    ///
    /// <para><b>What keeps it beatable.</b> Every one of those strengths is a per-intensity
    /// ladder on <see cref="SerpentChargeSettingsSO"/>: how often it re-plans, how many lines it
    /// considers, how much aim error it carries (in degrees, against a 1.5° cone), how long it
    /// takes to react to a ready rifle, how picky it is, how far it hunts pilots and how far it
    /// will go for a crystal. Intensity 1 is a slow, wobbly bot that never hunts you; intensity 4
    /// re-plans twice a second with a quarter-degree of error and hunts across the whole range.</para>
    ///
    /// <para><b>Replicated, never local.</b> An AI runs on the server only, and the shot resolves
    /// on EVERY peer (each one destroys the same conserved mass), so both the scope and the
    /// trigger go through <see cref="R_VesselActionHandler.PerformShipControllerActionsReplicated"/>,
    /// found by capability (<see cref="R_VesselActionHandler.TryGetInputForAction{T}"/>) so no
    /// control is named here. The scope is a LATCH (press engages, release disengages), so it is
    /// pressed once and held; <see cref="Disarm"/> releases it, because a release is an input event
    /// and nothing else would send one. While it is held the Serpent's cloak declines on its own
    /// (<c>CloakSeedWallActionSO</c> asks the scope), which retires AIPilot's blind cloak cycler
    /// for the match without touching it.</para>
    ///
    /// <para><b>It cloaks, and it cannot see you when you do.</b> The right trigger is the cloak
    /// unscoped and the rifle scoped, so the marksman owns it outright: it asks AIPilot's ability
    /// cycler to keep off the cloak (<see cref="AIPilot.SuppressCycledAbility{T}"/>) and, once per
    /// reload long enough to be worth it, lowers its scope, presses the cloak (replicated, so the
    /// hull vanishes, the trail fades and the seed is laid on every machine) and raises the scope
    /// again before the rifle is ready. And a rival whose hull is cloaked
    /// (<see cref="CloakSeedWallActionExecutor.IsCloaked"/>) is not a target and not worth anything
    /// in a line: the bot loses them where it last saw them, exactly as AIPilot's own player
    /// seeking does.</para>
    ///
    /// <para>Server-only, plain C#: the controller owns one, arms it when the turn starts, ticks it
    /// from Update on the server, and disarms it at the whistle, on replay and on despawn.</para>
    /// </summary>
    public sealed class SerpentMarksmanAI
    {
        sealed class Marksman
        {
            public IPlayer Player;
            public SniperScopeActionExecutor Scope;
            public SniperShotActionExecutor Shot;
            public CloakSeedWallActionExecutor Cloak;
            public System.Random Rng;

            // One cloak per reload: set when it presses the cloak, cleared when the rifle fires.
            public bool CloakedThisReload;

            public bool ScopeHeld;
            public float ScopePressedAt = float.NegativeInfinity;

            public float NextPlanAt;
            public bool HasPlan;
            public Vector3 AimPoint;
            public int PlannedValue;

            // When the plan is a rival pilot, the aim follows the pilot every frame; the jitter is
            // fixed for the plan's life (a constant error reads as a miss, not as a shiver).
            public IPlayer Quarry;
            public Vector3 QuarryOffset;

            public float ReadySince = -1f;
            public Transform Detour;
        }

        readonly List<Marksman> _marksmen = new();
        readonly List<Prism> _hits = new();
        readonly List<Prism> _samples = new();
        readonly List<Vector3> _candidates = new();

        GameDataSO _gameData;
        SerpentChargeSettingsSO _settings;
        SerpentChargeScoringRuleSO _rule;
        Cell _arena;
        Func<IReadOnlyList<ElementalCrystalImpactor>> _crystals;
        int _intensity = 1;
        float _nextTick;

        public bool IsArmed => _marksmen.Count > 0;

        /// <summary>
        /// SERVER. Takes every AI pilot on the roster: installs the steering provider and starts
        /// the fire loop. <paramref name="crystals"/> answers the live Charge crystals (may be
        /// empty). Safe to call again - it disarms first.
        /// </summary>
        public void Arm(GameDataSO gameData, SerpentChargeSettingsSO settings, ScoringRuleSO rule,
                        Cell arena, Func<IReadOnlyList<ElementalCrystalImpactor>> crystals)
        {
            Disarm();
            if (gameData == null || settings == null) return;

            _gameData = gameData;
            _settings = settings;
            _rule = rule as SerpentChargeScoringRuleSO;
            _arena = arena;
            _crystals = crystals;
            _intensity = Mathf.Clamp(gameData.SelectedIntensity != null ? gameData.SelectedIntensity.Value : 1, 1, 4);
            _nextTick = 0f;

            int ordinal = 0;
            foreach (var p in gameData.Players)
            {
                if (p == null || !p.IsInitializedAsAI) continue;
                var pilot = p.Vessel?.VesselStatus?.AIPilot;
                if (pilot == null) continue;

                var m = new Marksman
                {
                    Player = p,
                    // Seeded per bot so two bots sampling the same stand do not pick the same line.
                    Rng = new System.Random(unchecked(settings.crystalScatterSeed * 7919 + ++ordinal)),
                };
                // Spread the bots across the re-plan interval, so their full-range cone queries
                // never all land in one frame (a plan is ~lines+rivals QueryCones each).
                m.NextPlanAt = Time.time + Mathf.Max(0.1f,
                    SerpentChargeSettingsSO.Pick(settings.aiSolveSecondsByIntensity, _intensity, 1f)) * ordinal / 4f;
                _marksmen.Add(m);
                pilot.SetExternalTargetProvider(() => SteerPoint(m));
                // The marksman owns the right trigger (cloak unscoped, rifle scoped) - see the
                // class doc. Restored in Disarm.
                pilot.SuppressCycledAbility<CloakSeedWallActionSO>();
            }
        }

        /// <summary>SERVER. Releases every held scope and hands steering back to the platform.</summary>
        public void Disarm()
        {
            for (int i = 0; i < _marksmen.Count; i++)
            {
                var m = _marksmen[i];
                var status = m.Player?.Vessel?.VesselStatus;
                if (status == null) continue;

                if (m.ScopeHeld && status.ActionHandler != null &&
                    status.ActionHandler.TryGetInputForAction<SniperScopeActionSO>(out var scopeInput))
                    status.ActionHandler.StopShipControllerActionsReplicated(scopeInput);

                var pilot = status.AIPilot;
                if (pilot != null)
                {
                    pilot.ClearExternalTargetProvider();
                    pilot.RestoreCycledAbility<CloakSeedWallActionSO>();
                }
            }
            _marksmen.Clear();
        }

        /// <summary>SERVER. Call every frame; it throttles itself to <c>aiTickSeconds</c>.</summary>
        public void Tick()
        {
            if (_marksmen.Count == 0 || _settings == null) return;

            float now = Time.time;
            if (now < _nextTick) return;
            _nextTick = now + _settings.aiTickSeconds;

            var index = PrismSpatialIndex.EnsureInstance();
            if (index == null || !index.IsAvailable) return;

            for (int i = 0; i < _marksmen.Count; i++)
                TickOne(_marksmen[i], index, now);
        }

        // ── Per bot ─────────────────────────────────────────────────────────

        void TickOne(Marksman m, PrismSpatialIndex index, float now)
        {
            var status = m.Player?.Vessel?.VesselStatus;
            var handler = status?.ActionHandler;
            if (status == null || handler == null) return;

            // A pilot handed back to a human (or a vessel mid-swap) is not ours to drive.
            if (!status.AutoPilotEnabled) return;

            if (!ResolveExecutors(m, status)) return;

            // A hunted rival who cloaks is lost; the plan falls back to the forest next tick.
            if (m.Quarry != null && CloakSeedWallActionExecutor.IsCloaked(m.Quarry.Vessel?.VesselStatus))
            {
                m.Quarry = null;
                m.NextPlanAt = now;
            }

            if (TryCloak(m, handler, now)) return;

            EnsureScoped(m, handler, now);

            if (now >= m.NextPlanAt) Plan(m, status, index, now);

            ChooseDetour(m, status);

            if (!m.Scope.IsScoped) return;

            if (m.Shot.CooldownRemaining > 0f)
            {
                m.ReadySince = -1f;
                return;
            }

            if (m.ReadySince < 0f) m.ReadySince = now;
            float waited = now - m.ReadySince;
            if (waited < SerpentChargeSettingsSO.Pick(_settings.aiReactionSecondsByIntensity, _intensity, 0.3f))
                return;

            var tf = status.ShipTransform;
            if (tf == null) return;

            int live = ScoreLine(status, m.Shot, index, tf.position, tf.forward);

            int required = _settings.aiMinShotValue;
            if (waited < SerpentChargeSettingsSO.Pick(_settings.aiPatienceSecondsByIntensity, _intensity, 3f))
            {
                float fraction = SerpentChargeSettingsSO.Pick(_settings.aiFireFractionByIntensity, _intensity, 0.6f);
                required = Mathf.Max(required, Mathf.CeilToInt(fraction * m.PlannedValue));
            }
            if (live < required) return;

            if (!handler.TryGetInputForAction<SniperShotActionSO>(out var shotInput)) return;
            // The shot fires on PRESS (its StopAction is a no-op), and the release keeps the
            // handler's pressed-set honest. RT also carries the cloak, which declines while scoped.
            handler.PerformShipControllerActionsReplicated(shotInput);
            handler.StopShipControllerActionsReplicated(shotInput);

            m.ReadySince = -1f;
            m.CloakedThisReload = false;
            m.NextPlanAt = now;   // the round just changed the arena - re-plan on the next tick
        }

        /// <summary>
        /// The cloak, once per reload that leaves time for it: lower the scope, then (next tick,
        /// once the latch is down - RT is the RIFLE while scoped) press the cloak through the
        /// replicated input. Returns true while it is busy doing that, so the scope is not
        /// re-raised under it; <see cref="EnsureScoped"/> raises it again afterwards, well inside
        /// the reload.
        /// </summary>
        bool TryCloak(Marksman m, R_VesselActionHandler handler, float now)
        {
            if (m.Cloak == null || m.CloakedThisReload || !m.Cloak.CanCloak) return false;
            if (m.Shot.CooldownRemaining < _settings.aiCloakMinCooldownSeconds) return false;

            if (m.Scope.IsScoped || m.ScopeHeld)
            {
                if (handler.TryGetInputForAction<SniperScopeActionSO>(out var scopeInput))
                    handler.StopShipControllerActionsReplicated(scopeInput);
                m.ScopeHeld = false;
                m.ScopePressedAt = now;   // EnsureScoped waits its retry interval before re-raising
                return true;
            }

            // One attempt per reload whether or not it takes, so a refused press never stalls the
            // scope.
            m.CloakedThisReload = true;
            if (handler.TryGetInputForAction<CloakSeedWallActionSO>(out var cloakInput))
            {
                handler.PerformShipControllerActionsReplicated(cloakInput);
                handler.StopShipControllerActionsReplicated(cloakInput);
            }
            return true;
        }

        bool ResolveExecutors(Marksman m, IVesselStatus status)
        {
            // Re-resolved whenever a cached one dies: a vessel swap hands a pilot a different hull,
            // and a destroyed component still passes a reference test (Unity's == catches it).
            if (m.Scope != null && m.Shot != null) return true;

            var root = status.Transform;
            if (root == null) return false;
            m.Scope = root.GetComponentInChildren<SniperScopeActionExecutor>(true);
            m.Shot = root.GetComponentInChildren<SniperShotActionExecutor>(true);
            m.Cloak = root.GetComponentInChildren<CloakSeedWallActionExecutor>(true);   // optional
            m.ScopeHeld = false;
            return m.Scope != null && m.Shot != null;
        }

        void EnsureScoped(Marksman m, R_VesselActionHandler handler, float now)
        {
            if (m.Scope.IsScoped) return;
            if (now - m.ScopePressedAt < _settings.aiScopeRetrySeconds) return;
            if (!handler.TryGetInputForAction<SniperScopeActionSO>(out var scopeInput)) return;

            // A press that never took (input muted for the countdown, a dropped round trip) is
            // released first, so the handler's pressed-set and the latch agree before re-pressing.
            if (m.ScopeHeld) handler.StopShipControllerActionsReplicated(scopeInput);
            handler.PerformShipControllerActionsReplicated(scopeInput);
            m.ScopeHeld = true;
            m.ScopePressedAt = now;
        }

        // ── Planning ────────────────────────────────────────────────────────

        void Plan(Marksman m, IVesselStatus status, PrismSpatialIndex index, float now)
        {
            m.NextPlanAt = now + Mathf.Max(0.1f,
                SerpentChargeSettingsSO.Pick(_settings.aiSolveSecondsByIntensity, _intensity, 1f));

            var tf = status.ShipTransform;
            if (tf == null) return;
            Vector3 origin = tf.position;
            Vector3 forward = tf.forward;
            Domains own = status.Domain;
            float range = m.Shot.RangeUnits;
            float minAim = _settings.aiMinAimDistance;

            // ── Candidate aim points ──
            _candidates.Clear();
            int lines = Mathf.Max(1, SerpentChargeSettingsSO.Pick(_settings.aiCandidateLinesByIntensity, _intensity, 8));
            if (m.HasPlan && m.Quarry == null) _candidates.Add(m.AimPoint);   // the incumbent stays in the running
            if (_arena)
                Sample(m, index, _arena.GetExplosionTarget(own), _settings.aiStandSampleRadius, (lines + 1) / 2, own);
            Sample(m, index, origin + forward * _settings.aiAheadSampleDistance, _settings.aiAheadSampleRadius,
                   lines / 2, own);

            float bestUtility = 0f;
            int bestValue = 0;
            Vector3 bestPoint = default;
            IPlayer bestQuarry = null;

            for (int i = 0; i < _candidates.Count; i++)
            {
                Vector3 to = _candidates[i] - origin;
                float d = to.magnitude;
                if (d < minAim || d > range) continue;
                Vector3 dir = to / d;
                int value = ScoreLine(status, m.Shot, index, origin, dir);
                float utility = value * TurnFactor(forward, dir);
                if (utility <= bestUtility) continue;
                bestUtility = utility;
                bestValue = value;
                bestPoint = _candidates[i];
                bestQuarry = null;
            }

            // ── Rival pilots ──
            float hunt = SerpentChargeSettingsSO.Pick(_settings.aiPilotHuntRangeByIntensity, _intensity, 0f);
            if (hunt > 0f && _rule != null && _rule.PilotSnipePoints > 0)
            {
                var players = _gameData.Players;
                for (int i = 0; i < players.Count; i++)
                {
                    var rival = players[i];
                    if (rival == null || rival.Domain == own) continue;
                    if (CloakSeedWallActionExecutor.IsCloaked(rival.Vessel?.VesselStatus)) continue;
                    var rtf = rival.Vessel?.Transform;
                    if (rtf == null) continue;

                    Vector3 to = rtf.position - origin;
                    float d = to.magnitude;
                    if (d < minAim || d > Mathf.Min(hunt, range)) continue;
                    Vector3 dir = to / d;
                    // ScoreLine prices the pilot itself (it is inside its own line) plus the mass
                    // the round would take on the way.
                    int value = ScoreLine(status, m.Shot, index, origin, dir);
                    float utility = value * TurnFactor(forward, dir);
                    if (utility <= bestUtility) continue;
                    bestUtility = utility;
                    bestValue = value;
                    bestPoint = rtf.position;
                    bestQuarry = rival;
                }
            }

            if (bestValue <= 0)
            {
                // Nothing in reach worth a round: fly at the densest stand and look again soon.
                m.HasPlan = _arena;
                m.Quarry = null;
                m.PlannedValue = 0;
                if (_arena) m.AimPoint = _arena.GetExplosionTarget(own);
                return;
            }

            // Aim error, fixed for this plan: an offset perpendicular to the line whose angle is
            // the intensity's jitter. Against a 1.5° cone, 2.5° misses a pilot outright and shaves
            // a stand; 0.25° is a marksman.
            Vector3 line = bestPoint - origin;
            float jitter = SerpentChargeSettingsSO.Pick(_settings.aiAimJitterDegreesByIntensity, _intensity, 0f);
            Vector3 offset = jitter > 0f
                ? Perpendicular(line.normalized, m.Rng) * (line.magnitude * Mathf.Tan(jitter * Mathf.Deg2Rad)
                                                           * (float)m.Rng.NextDouble())
                : Vector3.zero;

            m.HasPlan = true;
            m.PlannedValue = bestValue;
            m.Quarry = bestQuarry;
            m.QuarryOffset = offset;
            m.AimPoint = bestPoint + offset;
        }

        void Sample(Marksman m, PrismSpatialIndex index, Vector3 centre, float radius, int count, Domains own)
        {
            if (count <= 0) return;
            index.QuerySphere(centre, radius, _samples);
            int n = _samples.Count;
            if (n == 0) return;

            // A few draws per wanted point, so a sphere that is mostly the bot's own colour still
            // yields hostile aim points without walking the whole list.
            int added = 0;
            for (int attempt = 0; attempt < count * 3 && added < count; attempt++)
            {
                var prism = _samples[m.Rng.Next(n)];
                if (prism == null || prism.destroyed || prism.Domain == own) continue;
                _candidates.Add(prism.transform.position);
                added++;
            }
            _samples.Clear();
        }

        float TurnFactor(Vector3 forward, Vector3 dir)
        {
            float angle = Vector3.Angle(forward, dir);
            return Mathf.Lerp(1f, _settings.aiTurnPenaltyFloor, angle / 180f);
        }

        static Vector3 Perpendicular(Vector3 axis, System.Random rng)
        {
            Vector3 a = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 b = Vector3.Cross(axis, a);
            float theta = (float)(rng.NextDouble() * Math.PI * 2.0);
            return a * Mathf.Cos(theta) + b * Mathf.Sin(theta);
        }

        // ── The round, predicted ────────────────────────────────────────────

        /// <summary>
        /// What a round fired from <paramref name="origin"/> along <paramref name="dir"/> would be
        /// worth to this bot's domain, priced the way the rule pays it: one per hostile prism the
        /// cone contains, plus <see cref="SerpentChargeScoringRuleSO.PilotSnipePoints"/> for every
        /// rival pilot inside the cone and within the round's reach (the farthest prism it kills,
        /// or the full range when it kills none - <c>SniperShotActionExecutor.StripVessels</c>).
        /// </summary>
        int ScoreLine(IVesselStatus status, SniperShotActionExecutor shot, PrismSpatialIndex index,
                      Vector3 origin, Vector3 dir)
        {
            float range = shot.RangeUnits;
            float half = shot.ConeHalfAngleDegrees;
            float minRadius = shot.MinPathRadius;
            Domains own = status.Domain;
            bool pierce = status.ElementalAbilityHandler != null &&
                          status.ElementalAbilityHandler.IsUpgradeActive(Element.Charge);

            index.QueryCone(origin, dir, range, half, minRadius, _hits);
            int prisms = 0;
            float farthest = 0f;
            for (int i = 0; i < _hits.Count; i++)
            {
                var prism = _hits[i];
                if (prism == null || prism.destroyed || prism.Domain == own) continue;
                if (!pierce && prism.prismProperties is { IsSuperShielded: true }) continue;
                prisms++;
                float along = Vector3.Dot(prism.transform.position - origin, dir);
                if (along > farthest) farthest = along;
            }
            _hits.Clear();

            int value = prisms;
            if (_rule == null || _rule.PilotSnipePoints <= 0) return value;

            float reach = prisms > 0 ? farthest : range;
            float tanHalf = Mathf.Tan(Mathf.Deg2Rad * Mathf.Clamp(half, 0f, 89f));
            var players = _gameData.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var rival = players[i];
                if (rival == null || rival.Domain == own) continue;
                // A cloaked pilot is invisible to the bot, so it is worth nothing in a line it is
                // weighing - it may still be hit by a round fired for the forest, and that pays.
                if (CloakSeedWallActionExecutor.IsCloaked(rival.Vessel?.VesselStatus)) continue;
                var rtf = rival.Vessel?.Transform;
                if (rtf == null) continue;
                if (PrismSpatialIndex.ConeContains(rtf.position, origin, dir, reach, tanHalf, minRadius))
                    value += _rule.PilotSnipePoints;
            }
            return value;
        }

        // ── Steering ────────────────────────────────────────────────────────

        void ChooseDetour(Marksman m, IVesselStatus status)
        {
            m.Detour = null;
            float range = SerpentChargeSettingsSO.Pick(_settings.aiChargeDetourRangeByIntensity, _intensity, 0f);
            if (range <= 0f || _crystals == null) return;
            if (m.Shot.CooldownRemaining < _settings.aiDetourMinCooldownSeconds) return;

            var tf = status.ShipTransform;
            var crystals = _crystals();
            if (tf == null || crystals == null) return;

            float bestSq = range * range;
            for (int i = 0; i < crystals.Count; i++)
            {
                var c = crystals[i];
                if (c == null || c.HasBeenCollected) continue;
                float dSq = (c.transform.position - tf.position).sqrMagnitude;
                if (dSq >= bestSq) continue;
                bestSq = dSq;
                m.Detour = c.transform;
            }
        }

        /// <summary>Where AIPilot steers this bot this frame: a crystal it is detouring for, the
        /// rival it is hunting (followed live), or its planned line.</summary>
        Vector3 SteerPoint(Marksman m)
        {
            if (m.Detour != null) return m.Detour.position;

            if (m.Quarry != null && !CloakSeedWallActionExecutor.IsCloaked(m.Quarry.Vessel?.VesselStatus))
            {
                var qtf = m.Quarry.Vessel?.Transform;
                if (qtf != null) return qtf.position + m.QuarryOffset;
                m.Quarry = null;
            }

            if (m.HasPlan) return m.AimPoint;

            var self = m.Player?.Vessel?.Transform;
            if (_arena) return _arena.transform.position;
            return self != null ? self.position + self.forward * 500f : Vector3.zero;
        }
    }
}
