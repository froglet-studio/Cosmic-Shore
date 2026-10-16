using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.UI;
using CosmicShore.Utility;
using Unity.Profiling;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Serpent Charge's AI: every autopiloted Serpent becomes a MARKSMAN that hunts rival pilots -
    /// it holds its scope, picks the rival it can best bring to bear, steers its nose at them, and
    /// fires the moment the rifle is ready and a rival is inside the live cone.
    ///
    /// <para><b>Why a mode needs this at all.</b> <see cref="AIPilot"/> writes a stick and a
    /// throttle and nothing else. Its generic ability cycler toggles the Serpent's cloak on a timer
    /// and never touches the scope, and the rifle only fires while the scope is up
    /// (<see cref="SniperShotActionExecutor"/>'s gate). Without this driver every AI Serpent would
    /// fly the whole match unable to score - an opponent that cannot play, which the Tollway rule
    /// calls a defect.</para>
    ///
    /// <para><b>It is a hunter, not a lumberjack.</b> A CHARGE game scores hits on PILOTS
    /// (<see cref="SerpentChargeScoringRuleSO"/>), so the only thing worth a round is a rival in the
    /// cone. The first cut planned fat lines through the forest with dozens of full-range cone
    /// queries a plan; that whole search is gone. A plan is now one pass over the roster (a
    /// distance and an angle per rival), the fire gate is <see cref="PrismSpatialIndex.ConeContains"/>
    /// per rival - pure arithmetic - and the ONE spatial query left runs only when a rival is
    /// already in the cone, to apply the round's reach rule (a strip reaches only as far as the
    /// farthest hostile prism the round kills, or the full range when it kills none -
    /// <c>SniperShotActionExecutor.StripVessels</c>), so a round the forest would stop short is not
    /// spent.</para>
    ///
    /// <para><b>Difficulty edits BELIEF, never the stick</b> (the vessel-AI rule). Every rung is
    /// a per-intensity ladder on <see cref="SerpentChargeSettingsSO"/>: how often the bot re-reads
    /// the field, how wrong its aim point is (an offset of up to N degrees against a 1.5 degree
    /// cone, fixed per plan so it reads as a miss rather than a shiver), how long it takes to react
    /// to a ready rifle, how far it hunts, and how far it will go for a Charge crystal. AIPilot then
    /// flies to that belief exactly as it flies anything else.</para>
    ///
    /// <para><b>It cloaks, and it cannot see you when you do.</b> The right trigger is the cloak
    /// unscoped and the rifle scoped, so the marksman owns it outright: it asks AIPilot's ability
    /// cycler to keep off the cloak (<see cref="AIPilot.SuppressCycledAbility{T}"/>) and, once per
    /// reload long enough to be worth it, lowers its scope, presses the cloak (replicated, so the
    /// hull, its tail and the seed all vanish on every machine) and raises the scope again before
    /// the rifle is ready. A rival whose hull is cloaked
    /// (<see cref="CloakSeedWallActionExecutor.IsCloaked"/>) is not a target and never fired at; the
    /// bot flies to where it last saw them.</para>
    ///
    /// <para><b>Replicated, never local.</b> An AI runs on the server only, and the shot resolves on
    /// EVERY peer, so the scope, the trigger and the cloak go through
    /// <see cref="R_VesselActionHandler.PerformShipControllerActionsReplicated"/>, found by
    /// capability (<see cref="R_VesselActionHandler.TryGetInputForAction{T}"/>).</para>
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

            // The rival being hunted, followed live while visible. The aim error is fixed for the
            // plan's life (a constant error reads as a miss, not as a shiver).
            public IPlayer Quarry;
            public Vector3 AimOffset;

            // Where the last hunted rival was last SEEN - the bot flies there when they cloak.
            public bool HasLastSeen;
            public Vector3 LastSeen;

            public float ReadySince = -1f;
            public Transform Detour;
        }

        // A per-frame AI cost gets a marker (Docs/AI_SYSTEM): Tick is the whole server-side cost per
        // throttled frame, Plan one bot's re-read of the field, Reach the one spatial query left.
        static readonly ProfilerMarker s_TickMarker = new("SerpentCharge.Marksman.Tick");
        static readonly ProfilerMarker s_PlanMarker = new("SerpentCharge.Marksman.Plan");
        static readonly ProfilerMarker s_ReachMarker = new("SerpentCharge.Marksman.Reach");

        readonly List<Marksman> _marksmen = new();
        readonly List<Prism> _hits = new();

        GameDataSO _gameData;
        SerpentChargeSettingsSO _settings;
        Cell _arena;
        Func<IReadOnlyList<ElementalCrystalImpactor>> _crystals;
        int _intensity = 1;
        float _nextTick;

        public bool IsArmed => _marksmen.Count > 0;

        /// <summary>
        /// SERVER. Takes every AI pilot on the roster: installs the steering provider and starts the
        /// fire loop. <paramref name="crystals"/> answers the live Charge crystals (may be empty).
        /// Safe to call again - it disarms first.
        /// </summary>
        public void Arm(GameDataSO gameData, SerpentChargeSettingsSO settings, Cell arena,
                        Func<IReadOnlyList<ElementalCrystalImpactor>> crystals)
        {
            Disarm();
            if (gameData == null || settings == null) return;

            _gameData = gameData;
            _settings = settings;
            _arena = arena;
            _crystals = crystals;
            _intensity = Mathf.Clamp(gameData.SelectedIntensity != null ? gameData.SelectedIntensity.Value : 1, 1, 4);
            _nextTick = 0f;

            float solve = Mathf.Max(0.1f, SerpentChargeSettingsSO.Pick(settings.aiSolveSecondsByIntensity, _intensity, 1f));
            int ordinal = 0;
            foreach (var p in gameData.Players)
            {
                if (p == null || !p.IsInitializedAsAI) continue;
                var pilot = p.Vessel?.VesselStatus?.AIPilot;
                if (pilot == null) continue;

                var m = new Marksman
                {
                    Player = p,
                    // Seeded per bot so two bots do not carry the same aim error.
                    Rng = new System.Random(unchecked(settings.crystalScatterSeed * 7919 + ++ordinal)),
                    // Spread the bots across the re-plan interval rather than all in one frame.
                    NextPlanAt = Time.time + solve * ordinal / 4f,
                };
                _marksmen.Add(m);
                pilot.SetExternalTargetProvider(() => SteerPoint(m));
                // The marksman owns the right trigger (cloak unscoped, rifle scoped) - see the class
                // doc. Restored in Disarm.
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

            using var tickScope = s_TickMarker.Auto();
            for (int i = 0; i < _marksmen.Count; i++)
                TickOne(_marksmen[i], now);
        }

        // ── Per bot ─────────────────────────────────────────────────────────

        void TickOne(Marksman m, float now)
        {
            var status = m.Player?.Vessel?.VesselStatus;
            var handler = status?.ActionHandler;
            if (status == null || handler == null) return;

            // A pilot handed back to a human (or a vessel mid-swap) is not ours to drive.
            if (!status.AutoPilotEnabled) return;

            if (!ResolveExecutors(m, status)) return;

            // A hunted rival who cloaks is lost where they were last seen; re-plan next tick.
            if (m.Quarry != null && !IsVisibleRival(m.Quarry, status.Domain))
            {
                m.Quarry = null;
                m.NextPlanAt = now;
            }

            if (TryCloak(m, handler, now)) return;

            EnsureScoped(m, handler, now);

            if (now >= m.NextPlanAt) Plan(m, status, now);

            ChooseDetour(m, status);

            if (!m.Scope.IsScoped) return;

            if (m.Shot.CooldownRemaining > 0f)
            {
                m.ReadySince = -1f;
                return;
            }

            if (m.ReadySince < 0f) m.ReadySince = now;
            if (now - m.ReadySince < SerpentChargeSettingsSO.Pick(_settings.aiReactionSecondsByIntensity, _intensity, 0.3f))
                return;

            var tf = status.ShipTransform;
            if (tf == null || !RoundWouldStrikeRival(status, m.Shot, tf.position, tf.forward)) return;

            if (!handler.TryGetInputForAction<SniperShotActionSO>(out var shotInput)) return;
            // The shot fires on PRESS (its StopAction is a no-op), and the release keeps the
            // handler's pressed-set honest. RT also carries the cloak, which declines while scoped.
            handler.PerformShipControllerActionsReplicated(shotInput);
            handler.StopShipControllerActionsReplicated(shotInput);

            m.ReadySince = -1f;
            m.CloakedThisReload = false;
            m.NextPlanAt = now;   // re-read the field (and re-roll the aim error) after every round
        }

        /// <summary>
        /// The cloak, once per reload that leaves time for it: lower the scope, then (next tick, once
        /// the latch is down - RT is the RIFLE while scoped) press the cloak through the replicated
        /// input. Returns true while it is busy doing that, so the scope is not re-raised under it;
        /// <see cref="EnsureScoped"/> raises it again afterwards, well inside the reload.
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

        // ── Planning: which rival, and how wrong the bot is about where they are ──

        void Plan(Marksman m, IVesselStatus status, float now)
        {
            using var planScope = s_PlanMarker.Auto();
            m.NextPlanAt = now + Mathf.Max(0.1f,
                SerpentChargeSettingsSO.Pick(_settings.aiSolveSecondsByIntensity, _intensity, 1f));

            var tf = status.ShipTransform;
            if (tf == null) return;
            Vector3 origin = tf.position;
            Vector3 forward = tf.forward;
            Domains own = status.Domain;
            float hunt = Mathf.Min(SerpentChargeSettingsSO.Pick(_settings.aiPilotHuntRangeByIntensity, _intensity, 2000f),
                                   m.Shot.RangeUnits);

            // Best visible rival: the cheapest to bring to bear - distance stretched by how far the
            // bot must turn. Within the hunt range if any rival is; otherwise the nearest visible
            // rival anywhere, so a bot never idles while somebody is out there.
            IPlayer best = null, bestAnywhere = null;
            float bestCost = float.MaxValue, bestAnywhereSq = float.MaxValue;
            var players = _gameData.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var rival = players[i];
                if (!IsVisibleRival(rival, own)) continue;
                Vector3 to = rival.Vessel.Transform.position - origin;
                float dSq = to.sqrMagnitude;
                if (dSq < bestAnywhereSq) { bestAnywhereSq = dSq; bestAnywhere = rival; }

                float d = Mathf.Sqrt(dSq);
                if (d > hunt || d < 1e-3f) continue;
                float cost = d / TurnFactor(forward, to / d);
                if (cost < bestCost) { bestCost = cost; best = rival; }
            }

            m.Quarry = best ?? bestAnywhere;
            if (m.Quarry == null) return;   // nobody visible: SteerPoint flies to the last sighting

            // Aim error, fixed for this plan: an offset perpendicular to the line, of up to the
            // intensity's jitter in angle. Against a 1.5 deg cone, 3 deg misses more than it hits;
            // 0.5 deg is a marksman.
            Vector3 line = m.Quarry.Vessel.Transform.position - origin;
            float jitter = SerpentChargeSettingsSO.Pick(_settings.aiAimJitterDegreesByIntensity, _intensity, 0f);
            m.AimOffset = jitter > 0f && line.sqrMagnitude > 1e-6f
                ? Perpendicular(line.normalized, m.Rng) * (line.magnitude * Mathf.Tan(jitter * Mathf.Deg2Rad)
                                                           * (float)m.Rng.NextDouble())
                : Vector3.zero;
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

        /// <summary>An opposing pilot with a live hull that is NOT cloaked - the only kind the bot
        /// can see, hunt or fire at.</summary>
        static bool IsVisibleRival(IPlayer rival, Domains own)
        {
            if (rival == null || rival.Domain == own) return false;
            var vessel = rival.Vessel;
            if (vessel == null || vessel.Transform == null) return false;
            return !CloakSeedWallActionExecutor.IsCloaked(vessel.VesselStatus);
        }

        // ── The fire gate: would THIS round, fired now, strip a rival? ──────

        /// <summary>
        /// True when a round fired from <paramref name="origin"/> along <paramref name="dir"/> would
        /// strip a visible rival: the rival is inside the cone (arithmetic, per rival) AND within
        /// the round's reach - which only the one spatial query below can answer, so it runs only
        /// once a rival is already in the cone.
        /// </summary>
        bool RoundWouldStrikeRival(IVesselStatus status, SniperShotActionExecutor shot, Vector3 origin, Vector3 dir)
        {
            float range = shot.RangeUnits;
            float half = shot.ConeHalfAngleDegrees;
            float minRadius = shot.MinPathRadius;
            float tanHalf = Mathf.Tan(Mathf.Deg2Rad * Mathf.Clamp(half, 0f, 89f));
            Domains own = status.Domain;

            float nearestRival = float.MaxValue;
            var players = _gameData.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var rival = players[i];
                if (!IsVisibleRival(rival, own)) continue;
                Vector3 p = rival.Vessel.Transform.position;
                if (!PrismSpatialIndex.ConeContains(p, origin, dir, range, tanHalf, minRadius)) continue;
                float along = Vector3.Dot(p - origin, dir);
                if (along < nearestRival) nearestRival = along;
            }
            if (nearestRival == float.MaxValue) return false;

            // The reach rule (SniperShotActionExecutor.StripVessels): the strip reaches as far as the
            // farthest prism the round kills, or the full range when it kills none.
            using var reachScope = s_ReachMarker.Auto();
            var index = PrismSpatialIndex.EnsureInstance();
            if (index == null || !index.IsAvailable) return true;

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

            float reach = prisms > 0 ? farthest : range;
            return nearestRival <= reach;
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
        /// rival it is hunting (followed live, through its aim error, and stood off from when it is
        /// too close to line up), the last place a cloaked rival was seen, or the arena.</summary>
        Vector3 SteerPoint(Marksman m)
        {
            if (m.Detour != null) return m.Detour.position;

            var self = m.Player?.Vessel?.Transform;
            if (m.Quarry != null && self != null && IsVisibleRival(m.Quarry, m.Player.Domain))
            {
                Vector3 quarry = m.Quarry.Vessel.Transform.position;
                m.HasLastSeen = true;
                m.LastSeen = quarry;

                // Too close to line a sniper round up: pull away to a standoff point and come back.
                Vector3 away = self.position - quarry;
                if (away.sqrMagnitude < _settings.aiMinAimDistance * _settings.aiMinAimDistance)
                    return quarry + (away.sqrMagnitude > 1e-6f ? away.normalized : self.forward)
                                  * _settings.aiStandoffDistance;

                return quarry + m.AimOffset;
            }

            if (m.HasLastSeen) return m.LastSeen;
            if (_arena) return _arena.transform.position;
            return self != null ? self.position + self.forward * 500f : Vector3.zero;
        }
    }
}
