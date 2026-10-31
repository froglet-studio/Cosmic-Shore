using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The AI pilot of Serpent Gulag: a SNIPER that keeps its distance and digs in. Server-only.
    ///
    /// <para><b>Input only.</b> <see cref="AIPilot"/> writes a stick and a throttle, and the rifle fires only while
    /// the scope is up, so without this every AI Serpent would fly the match unable to score - an all-AI domain that
    /// cannot play (the Tollway rule). The marksman steers with <see cref="AIPilot.SetExternalTargetProvider"/> and
    /// presses LT (scope), RT (the rifle while scoped, the cloak unscoped) and the stop toggle (B) through
    /// <see cref="R_VesselActionHandler.PerformShipControllerActionsReplicated"/>, so every peer resolves what it does.
    /// Nothing about the Serpent changes: it uses the kit as shipped.</para>
    ///
    /// <para><b>The loop (Shombith, 2026-10-31: "the AI should learn to keep distance as a sniper and try to build its
    /// defenses rather and not rush towards me").</b></para>
    /// <list type="number">
    /// <item><b>Pick a post</b> at the rung's preferred range from the rival it hunts. Where a nest stands near that
    /// range, the post is BEHIND it (the nest between the AI and the rival, its plates within the wall's reach);
    /// otherwise straight back along the line it already holds. Already inside the band: it stays where it is.</item>
    /// <item><b>Fly there</b> - never at the rival.</item>
    /// <item><b>Dig in</b>: press B. The stopped Serpent's latest trail prism becomes a held seed and a wall grows from
    /// the loose mass beside it for as long as it stays stopped, and it is immune to elemental debuffs meanwhile
    /// (SERPENT_SEED_WALL.md, the stance). It turns in place to track the rival (turning is tripled while stopped).</item>
    /// <item><b>Snipe</b> from the stance: scope with the rival near its nose, wait the rung's settle, fire when its
    /// AIM POINT (true position + lead + the plan's fixed error) is inside the 1.5 deg cone.</item>
    /// <item><b>Relocate</b> when its hold runs out, or <b>retreat</b> the moment a rival comes inside its keep-away
    /// distance: press B again and pick a new post away from them. After a shot it may cloak (the rung's chance).</item>
    /// <item><b>Forage</b> the crystal field while its rifle recovers - only when it is not dug in and no rival is
    /// inside its keep-away distance of the crystal.</item>
    /// </list>
    ///
    /// <para><b>Difficulty is the seat's AI level, never the intensity</b> (intensity is the map, difficulty the
    /// opponent, AIDifficulty.cs). Easy to Hard are the patient sniper above, sharper at each step. <b>Legendary
    /// plays aggressively</b> (Shombith, 2026-10-31): it pushes in to a short preferred range, only breaks off when a
    /// rival is nearly on it, and digs in only briefly before moving again - still a sniper, never a rammer.</para>
    ///
    /// <para><b>What it does not model</b> (SERPENTGULAG.md): it sees cloaked pilots (no public cloak state exists on
    /// the vessel); and a post behind a nest is reached by flying at it, so it may clip a nest's plate on the way (a
    /// plain prism costs a Serpent nothing to ram).</para>
    /// </summary>
    public sealed class SerpentGulagMarksman
    {
        enum ShotPhase { Idle, Scoping, Fired, CloakPending, Cloaking }

        const float PressHoldSeconds = 0.2f;
        const float CloakDelaySeconds = 0.35f;

        sealed class Hunter
        {
            public IPlayer Player;
            /// <summary>The seat's AI difficulty, 1 (Easy) to 4 (Legendary): the index into every ladder.
            /// Difficulty is the OPPONENT, intensity the MAP (AIDifficulty.cs), so the ladders never read intensity.</summary>
            public int Level;
            public IVesselStatus Status;
            public AIPilot Autopilot;
            public R_VesselActionHandler Handler;
            public SniperScopeActionExecutor Scope;
            public SniperShotActionExecutor Rifle;
            public System.Random Rng;

            public IPlayer Rival;
            public float NextRetargetAt;
            public Vector3 ErrorAxis;
            public float ErrorDegrees;
            public Vector3 Steer;

            // The post and the stance.
            public bool HasPost;
            public Vector3 Post;
            public int PostNest = -1;
            public int LastNest = -1;
            public bool WantStance;
            public float DugInAt = -1f;
            public float NextStanceTryAt;
            public float ReleaseStanceAt = -1f;

            public ShotPhase Shot;
            public float ShotPhaseAt;
            public float NextShotAt;
            public bool ScopeHeld, TriggerHeld, StanceHeld;
        }

        readonly List<Hunter> _hunters = new();
        readonly List<Vector3> _nests = new();
        SerpentGulagSettingsSO _settings;
        GameDataSO _gameData;
        Func<Vector3, Vector3?> _nearestCrystal;
        Vector3 _centre;
        InputEvents _stanceInput;
        bool _hasStanceInput;

        public bool IsArmed => _hunters.Count > 0;

        /// <summary>Arms every AI pilot on the roster. <paramref name="nearestCrystal"/> answers the nearest live
        /// crystal of the field (or null), from this machine's copy of it; <paramref name="nests"/> the centre of
        /// every sniper nest, in world space (empty at intensity 1).</summary>
        public void Arm(GameDataSO gameData, SerpentGulagSettingsSO settings, Vector3 centre,
                        Func<Vector3, Vector3?> nearestCrystal, IReadOnlyList<Vector3> nests)
        {
            Disarm();
            if (gameData == null || settings == null) return;
            _gameData = gameData;
            _settings = settings;
            _centre = centre;
            _nearestCrystal = nearestCrystal;
            _nests.Clear();
            if (nests != null) _nests.AddRange(nests);

            var players = gameData.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p == null || !p.IsInitializedAsAI) continue;
                var status = p.Vessel?.VesselStatus;
                if (status == null) continue;
                // TryGetComponent, not VesselStatus.AIPilot: that getter ADDS an autopilot to a hull without one.
                var autopilot = status is Component c && c && c.TryGetComponent(out AIPilot ai) ? ai : null;
                if (autopilot == null || status.ActionHandler == null) continue;

                // The stop toggle is found by capability, not by a hard-wired button.
                if (!_hasStanceInput)
                    _hasStanceInput = status.ActionHandler.TryGetBoundAction<ToggleTranslationModeActionSO>(out _, out _stanceInput);

                var root = status.Transform;
                var hunter = new Hunter
                {
                    Player = p,
                    Level = (int)gameData.AIDifficultyFor(p),
                    Status = status,
                    Autopilot = autopilot,
                    Handler = status.ActionHandler,
                    Scope = root ? root.GetComponentInChildren<SniperScopeActionExecutor>(true) : null,
                    Rifle = root ? root.GetComponentInChildren<SniperShotActionExecutor>(true) : null,
                    Rng = new System.Random(unchecked((int)p.PlayerNetId * 7919 + 77)),
                    Steer = centre,
                };
                _hunters.Add(hunter);

                // The cycler pressed the cloak on its own clock; here RT is the rifle while scoped, so the hunter
                // cloaks only when it means to.
                autopilot.SetCycledAbilityFilter(a => a is not CloakSeedWallActionSO && a is not ToggleTranslationModeActionSO);
                var captured = hunter;
                autopilot.SetExternalTargetProvider(() => captured.Steer);
            }
        }

        /// <summary>Releases every press the hunters hold and hands the autopilots back. Safe to call twice. A hunter
        /// left stopped stays stopped until the turn's end lifts the stance, as a human's would.</summary>
        public void Disarm()
        {
            for (int i = 0; i < _hunters.Count; i++)
            {
                var h = _hunters[i];
                if (h.Handler)
                {
                    if (h.TriggerHeld) h.Handler.StopShipControllerActionsReplicated(InputEvents.RightStickAction);
                    if (h.ScopeHeld) h.Handler.StopShipControllerActionsReplicated(InputEvents.LeftStickAction);
                    if (h.StanceHeld && _hasStanceInput) h.Handler.StopShipControllerActionsReplicated(_stanceInput);
                }
                if (h.Autopilot)
                {
                    h.Autopilot.ClearExternalTargetProvider();
                    h.Autopilot.SetCycledAbilityFilter(null);
                }
            }
            _hunters.Clear();
        }

        public void Tick()
        {
            if (_settings == null) return;
            float now = Time.time;
            for (int i = 0; i < _hunters.Count; i++)
            {
                var h = _hunters[i];
                var self = h.Player?.Vessel?.Transform;
                if (!self || !h.Handler || !h.Autopilot || !h.Autopilot.AutoPilotEnabled) continue;

                if (now >= h.NextRetargetAt || !IsLiveRival(h.Rival, h.Player))
                    Retarget(h, self, now);

                Vector3 aim = AimPoint(h, self);
                Plan(h, self, aim, now);
                TickStance(h, now);
                TickShot(h, self, aim, now);
            }
        }

        bool Stopped(Hunter h) => h.Status != null && h.Status.IsTranslationRestricted;

        // ── Choosing a rival ───────────────────────────────────────────────

        void Retarget(Hunter h, Transform self, float now)
        {
            h.NextRetargetAt = now + Mathf.Max(0.1f, SerpentGulagSettingsSO.Rung(_settings.aiRetargetSeconds, h.Level));
            var previous = h.Rival;
            h.Rival = ChooseRival(h, self);
            if (!ReferenceEquals(previous, h.Rival)) h.HasPost = false;   // a new rival is a new post

            // A fresh aim error for the plan: a fixed angle off the true line, in a random direction across it.
            h.ErrorDegrees = SerpentGulagSettingsSO.Rung(_settings.aiAimErrorDegrees, h.Level) * (float)h.Rng.NextDouble();
            float spin = (float)(h.Rng.NextDouble() * Math.PI * 2.0);
            h.ErrorAxis = new Vector3(Mathf.Cos(spin), Mathf.Sin(spin), 0f);
        }

        IPlayer ChooseRival(Hunter h, Transform self)
        {
            float huntRange = SerpentGulagSettingsSO.Rung(_settings.aiHuntRange, h.Level);
            IPlayer best = null;
            float bestCost = float.MaxValue;
            var players = _gameData.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var candidate = players[i];
                if (!IsLiveRival(candidate, h.Player)) continue;
                Vector3 to = candidate.Vessel.Transform.position - self.position;
                float distance = to.magnitude;
                if (distance > huntRange) continue;
                // A rival dead ahead costs its distance; one straight behind costs ~3x it.
                float facing = distance > 1e-3f ? Vector3.Dot(self.forward, to / distance) : 1f;
                float cost = distance / Mathf.Lerp(0.35f, 1f, 0.5f * (facing + 1f));
                if (!candidate.IsInitializedAsAI) cost /= _settings.aiHumanFocus;
                if (cost >= bestCost) continue;
                bestCost = cost;
                best = candidate;
            }
            return best;
        }

        static bool IsLiveRival(IPlayer candidate, IPlayer self)
        {
            if (candidate == null || self == null || ReferenceEquals(candidate, self)) return false;
            if (candidate.Domain == self.Domain) return false;   // the rifle never strips a team-mate
            return candidate.Vessel?.Transform != null;
        }

        /// <summary>The nearest rival's distance (u), whoever is hunted - a sniper backs off from anyone closing in.</summary>
        float NearestRivalDistance(Hunter h, Vector3 from, out Vector3 rivalPosition)
        {
            float best = float.MaxValue;
            rivalPosition = from;
            var players = _gameData.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (!IsLiveRival(p, h.Player)) continue;
                var pos = p.Vessel.Transform.position;
                float d = Vector3.Distance(pos, from);
                if (d >= best) continue;
                best = d;
                rivalPosition = pos;
            }
            return best;
        }

        // ── Where to be ────────────────────────────────────────────────────

        Vector3 AimPoint(Hunter h, Transform self)
        {
            var rivalTf = h.Rival?.Vessel?.Transform;
            if (!rivalTf) return _centre;
            var status = h.Rival.Vessel.VesselStatus;
            Vector3 lead = status != null ? status.Course * status.Speed * _settings.aiLeadSeconds : Vector3.zero;
            Vector3 truth = rivalTf.position + lead;

            // The error, laid across the line of sight at the rival's range.
            Vector3 line = truth - self.position;
            float range = line.magnitude;
            if (range < 1e-3f || h.ErrorDegrees <= 0f) return truth;
            var frame = Quaternion.LookRotation(line / range, Vector3.up);
            return truth + frame * h.ErrorAxis * (range * Mathf.Tan(h.ErrorDegrees * Mathf.Deg2Rad));
        }

        /// <summary>Decide the stance and the steering: retreat, hold, relocate, forage or travel to the post.</summary>
        void Plan(Hunter h, Transform self, Vector3 aim, float now)
        {
            float keepAway = SerpentGulagSettingsSO.Rung(_settings.aiKeepAwayDistance, h.Level);
            float nearest = NearestRivalDistance(h, self.position, out var threat);

            // Somebody is closing in: give up the post and back off. A sniper retreats; it does not turn to fight.
            if (nearest < keepAway)
            {
                h.WantStance = false;
                h.DugInAt = -1f;
                if (h.HasPost && h.PostNest >= 0) h.LastNest = h.PostNest;
                h.HasPost = false;
                Vector3 away = self.position - threat;
                h.Steer = away.sqrMagnitude > 1e-4f
                    ? self.position + away.normalized * SerpentGulagSettingsSO.Rung(_settings.aiPreferredRange, h.Level)
                    : _centre;
                return;
            }

            // Dug in: track the rival from the stance until the hold runs out.
            if (Stopped(h) && h.WantStance)
            {
                if (h.DugInAt < 0f) h.DugInAt = now;
                h.Steer = h.Rival?.Vessel?.Transform ? aim : self.position + self.forward * 100f;
                if (now - h.DugInAt >= SerpentGulagSettingsSO.Rung(_settings.aiHoldSeconds, h.Level) &&
                    h.Shot == ShotPhase.Idle)
                {
                    h.WantStance = false;      // relocate: a sniper who has fired from one place is found there
                    h.DugInAt = -1f;
                    if (h.PostNest >= 0) h.LastNest = h.PostNest;
                    h.HasPost = false;
                }
                return;
            }

            // Reloading in the open: top up Charge at the field, if nobody is waiting there.
            float cooling = h.Rifle ? h.Rifle.CooldownRemaining : 0f;
            float forageAt = SerpentGulagSettingsSO.Rung(_settings.aiForageMinCooldownSeconds, h.Level);
            if (!h.WantStance && h.Shot == ShotPhase.Idle && cooling >= forageAt && _nearestCrystal != null)
            {
                var crystal = _nearestCrystal(self.position);
                if (crystal.HasValue && NearestRivalDistance(h, crystal.Value, out _) >= keepAway)
                {
                    h.Steer = crystal.Value;
                    h.HasPost = false;
                    return;
                }
            }

            if (!h.HasPost) ChoosePost(h, self.position);
            h.Steer = h.Post;

            // Arrived: dig in.
            if ((self.position - h.Post).sqrMagnitude <= _settings.aiPostArrivalRadius * _settings.aiPostArrivalRadius)
                h.WantStance = true;
        }

        /// <summary>
        /// A post at the preferred range from the rival: behind the nest whose far side is nearest that range (the
        /// nest between the AI and the rival), else straight back along the current line, else right here when the
        /// AI is already in the band.
        /// </summary>
        void ChoosePost(Hunter h, Vector3 self)
        {
            h.HasPost = true;
            h.PostNest = -1;
            var rivalTf = h.Rival?.Vessel?.Transform;
            float preferred = SerpentGulagSettingsSO.Rung(_settings.aiPreferredRange, h.Level);
            float keepAway = SerpentGulagSettingsSO.Rung(_settings.aiKeepAwayDistance, h.Level);

            if (!rivalTf)
            {
                // Nobody to hunt: take the nearest nest's far side from the centre, or hold at the preferred range
                // from the middle.
                h.Post = _nests.Count > 0
                    ? BehindNest(NearestNest(self, -1), _centre)
                    : _centre + (self - _centre).normalized * preferred;
                return;
            }

            Vector3 rival = rivalTf.position;
            float here = Mathf.Abs(Vector3.Distance(self, rival) - preferred);

            int bestNest = -1;
            float bestScore = float.MaxValue;
            for (int i = 0; i < _nests.Count; i++)
            {
                if (i == h.LastNest) continue;   // never straight back to the post it just left
                var post = BehindNest(i, rival);
                float toRival = Vector3.Distance(post, rival);
                if (toRival < keepAway) continue;
                float score = Mathf.Abs(toRival - preferred) + 0.5f * Vector3.Distance(self, post);
                if (score >= bestScore) continue;
                bestScore = score;
                bestNest = i;
            }

            if (bestNest >= 0 && Mathf.Abs(Vector3.Distance(BehindNest(bestNest, rival), rival) - preferred) <
                here - _settings.aiRangeBand * 0.25f)
            {
                h.PostNest = bestNest;
                h.Post = BehindNest(bestNest, rival);
                return;
            }

            // In the band already: dig in here. Otherwise back off (or close in) along the line it holds.
            if (here <= _settings.aiRangeBand)
            {
                h.Post = self;
                return;
            }
            Vector3 fromRival = self - rival;
            if (fromRival.sqrMagnitude < 1e-4f) fromRival = self - _centre;
            h.Post = rival + fromRival.normalized * preferred;
        }

        Vector3 BehindNest(int nest, Vector3 threat)
        {
            var n = _nests[nest];
            Vector3 away = n - threat;
            if (away.sqrMagnitude < 1e-4f) away = n - _centre;
            return n + away.normalized * _settings.aiBehindNestDistance;
        }

        int NearestNest(Vector3 from, int exclude)
        {
            int best = 0;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < _nests.Count; i++)
            {
                if (i == exclude) continue;
                float d = (_nests[i] - from).sqrMagnitude;
                if (d >= bestSqr) continue;
                bestSqr = d;
                best = i;
            }
            return best;
        }

        // ── The stance (B) ─────────────────────────────────────────────────

        /// <summary>Press the stop toggle until the stance matches what the plan wants, read back off the hull.</summary>
        void TickStance(Hunter h, float now)
        {
            if (!_hasStanceInput) return;
            if (h.ReleaseStanceAt >= 0f && now >= h.ReleaseStanceAt)
            {
                h.ReleaseStanceAt = -1f;
                Release(h, _stanceInput, ref h.StanceHeld);
            }
            if (Stopped(h) == h.WantStance || now < h.NextStanceTryAt || h.StanceHeld) return;
            // Never toggle with the scope up or a shot in flight: the press would land mid-sequence.
            if (h.Shot != ShotPhase.Idle) return;
            Press(h, _stanceInput, ref h.StanceHeld);
            h.ReleaseStanceAt = now + PressHoldSeconds;
            h.NextStanceTryAt = now + _settings.aiStanceRetrySeconds;
        }

        // ── Shooting ────────────────────────────────────────────────────────

        void TickShot(Hunter h, Transform self, Vector3 aim, float now)
        {
            float keepAway = SerpentGulagSettingsSO.Rung(_settings.aiKeepAwayDistance, h.Level);
            switch (h.Shot)
            {
                case ShotPhase.Idle:
                {
                    if (now < h.NextShotAt || h.StanceHeld || !RifleReady(h)) return;
                    var rivalTf = h.Rival?.Vessel?.Transform;
                    if (!rivalTf) return;
                    float distance = Vector3.Distance(self.position, rivalTf.position);
                    float reach = h.Rifle ? h.Rifle.RangeUnits : 3000f;
                    if (distance > reach) return;
                    if (Vector3.Angle(self.forward, aim - self.position) > _settings.aiScopeOpenAngle) return;
                    Press(h, InputEvents.LeftStickAction, ref h.ScopeHeld);
                    Enter(h, ShotPhase.Scoping, now);
                    return;
                }

                case ShotPhase.Scoping:
                {
                    bool lost = !h.Rival?.Vessel?.Transform;
                    if (lost || now - h.ShotPhaseAt > _settings.aiShotGiveUpSeconds ||
                        NearestRivalDistance(h, self.position, out _) < keepAway)   // somebody is closing in: lower it and go
                    {
                        Release(h, InputEvents.LeftStickAction, ref h.ScopeHeld);
                        Enter(h, ShotPhase.Idle, now);
                        h.NextShotAt = now + 1f;
                        return;
                    }
                    float settle = SerpentGulagSettingsSO.Rung(_settings.aiScopeSettleSeconds, h.Level);
                    if (!IsScoped(h) || now - h.ShotPhaseAt < settle) return;
                    if (!InCone(h, self, aim)) return;
                    Press(h, InputEvents.RightStickAction, ref h.TriggerHeld);
                    Enter(h, ShotPhase.Fired, now);
                    return;
                }

                case ShotPhase.Fired:
                    if (now - h.ShotPhaseAt < PressHoldSeconds) return;
                    Release(h, InputEvents.RightStickAction, ref h.TriggerHeld);
                    Release(h, InputEvents.LeftStickAction, ref h.ScopeHeld);
                    h.NextShotAt = now + 0.5f;
                    float chance = SerpentGulagSettingsSO.Rung(_settings.aiCloakAfterShotChance, h.Level);
                    Enter(h, h.Rng.NextDouble() < chance ? ShotPhase.CloakPending : ShotPhase.Idle, now);
                    return;

                case ShotPhase.CloakPending:
                    // RT is the cloak only once the scope is down.
                    if (now - h.ShotPhaseAt < CloakDelaySeconds || IsScoped(h)) return;
                    Press(h, InputEvents.RightStickAction, ref h.TriggerHeld);
                    Enter(h, ShotPhase.Cloaking, now);
                    return;

                case ShotPhase.Cloaking:
                    if (now - h.ShotPhaseAt < PressHoldSeconds) return;
                    Release(h, InputEvents.RightStickAction, ref h.TriggerHeld);
                    Enter(h, ShotPhase.Idle, now);
                    return;
            }
        }

        static void Enter(Hunter h, ShotPhase phase, float now)
        {
            h.Shot = phase;
            h.ShotPhaseAt = now;
        }

        static void Press(Hunter h, InputEvents input, ref bool held)
        {
            h.Handler.PerformShipControllerActionsReplicated(input);
            held = true;
        }

        static void Release(Hunter h, InputEvents input, ref bool held)
        {
            if (!held) return;
            h.Handler.StopShipControllerActionsReplicated(input);
            held = false;
        }

        static bool IsScoped(Hunter h) => h.Scope && h.Scope.IsScoped;

        /// <summary>The shipped rifle's own recovery is the only limit on a shot.</summary>
        static bool RifleReady(Hunter h) => !h.Rifle || h.Rifle.CooldownRemaining <= 0f;

        /// <summary>Is the AI's aim point inside the rifle's cone (its radius floored at 6 u, as the rifle's own
        /// path is)? The aim point carries the plan's error, so a wide error is a real miss.</summary>
        static bool InCone(Hunter h, Transform self, Vector3 aim)
        {
            float half = h.Rifle ? h.Rifle.ConeHalfAngleDegrees : 1.5f;
            Vector3 to = aim - self.position;
            float along = Vector3.Dot(to, self.forward);
            if (along <= 0f) return false;
            float lateral = (to - self.forward * along).magnitude;
            return lateral <= Mathf.Max(6f, along * Mathf.Tan(half * Mathf.Deg2Rad));
        }
    }
}
