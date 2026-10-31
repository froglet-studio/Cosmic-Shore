using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The AI pilot of Serpent Gulag: a marksman that hunts rival pilots with the shipped rifle. Server-only.
    ///
    /// <para><b>Input only.</b> <see cref="AIPilot"/> writes a stick and a throttle, and the rifle fires only while
    /// the scope is up, so without this every AI Serpent would fly the match unable to score - an all-AI domain
    /// that cannot play (the Tollway rule). The hunter steers with <see cref="AIPilot.SetExternalTargetProvider"/>
    /// and presses LT (the scope) and RT (the rifle while scoped, the cloak unscoped) through
    /// <see cref="R_VesselActionHandler.PerformShipControllerActionsReplicated"/>, so every peer resolves the
    /// shot - the press shape Serpent Time's AI uses for its wall shots. Nothing about the Serpent changes.</para>
    ///
    /// <para><b>The loop.</b> While its rifle is recovering by more than the rung's forage threshold it flies to
    /// the nearest crystal of the field (Charge shortens the next recovery). Otherwise it hunts the rival it can
    /// bring to bear most cheaply (distance over a turn factor, humans preferred), steering at that pilot plus a
    /// lead and a fixed aim error for the plan's life. Inside <see cref="SerpentGulagSettingsSO.aiMinAimDistance"/>
    /// it breaks away to the standoff distance and turns back in. With the rival near its nose it raises the
    /// scope, waits the rung's settle time, and fires when its AIM POINT is inside the rifle's cone - so an aim
    /// error wider than the cone is a real miss. After a shot it may cloak (the rung's chance) to vanish from the
    /// pilot it fired on.</para>
    ///
    /// <para><b>What it does not model</b> (SERPENTGULAG.md): it sees cloaked pilots (no public cloak state exists
    /// on the vessel, and the kit is not changed for a mode), and it does not perch in the nests.</para>
    /// </summary>
    public sealed class SerpentGulagMarksman
    {
        enum ShotPhase { Idle, Scoping, Fired, CloakPending, Cloaking }

        const float PressHoldSeconds = 0.2f;
        const float CloakDelaySeconds = 0.35f;

        sealed class Hunter
        {
            public IPlayer Player;
            public AIPilot Autopilot;
            public R_VesselActionHandler Handler;
            public SniperScopeActionExecutor Scope;
            public SniperShotActionExecutor Rifle;
            public System.Random Rng;

            public IPlayer Rival;
            public float NextRetargetAt;
            public Vector3 ErrorAxis;
            public float ErrorDegrees;
            public bool BreakingAway;
            public Vector3 Steer;

            public ShotPhase Shot;
            public float ShotPhaseAt;
            public float NextShotAt;
            public bool ScopeHeld, TriggerHeld;
        }

        readonly List<Hunter> _hunters = new();
        SerpentGulagSettingsSO _settings;
        GameDataSO _gameData;
        Func<Vector3, Vector3?> _nearestCrystal;
        Vector3 _centre;
        int _intensity = 1;

        public bool IsArmed => _hunters.Count > 0;

        /// <summary>Arms every AI pilot on the roster. <paramref name="nearestCrystal"/> answers the nearest live
        /// crystal of the field (or null), from this machine's copy of it.</summary>
        public void Arm(GameDataSO gameData, SerpentGulagSettingsSO settings, Vector3 centre,
                        Func<Vector3, Vector3?> nearestCrystal)
        {
            Disarm();
            if (gameData == null || settings == null) return;
            _gameData = gameData;
            _settings = settings;
            _centre = centre;
            _nearestCrystal = nearestCrystal;
            _intensity = gameData.SelectedIntensity != null ? Mathf.Clamp(gameData.SelectedIntensity.Value, 1, 4) : 1;

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

                var root = status.Transform;
                var hunter = new Hunter
                {
                    Player = p,
                    Autopilot = autopilot,
                    Handler = status.ActionHandler,
                    Scope = root ? root.GetComponentInChildren<SniperScopeActionExecutor>(true) : null,
                    Rifle = root ? root.GetComponentInChildren<SniperShotActionExecutor>(true) : null,
                    Rng = new System.Random(unchecked((int)p.PlayerNetId * 7919 + 77)),
                    Steer = centre,
                };
                _hunters.Add(hunter);

                // The cycler pressed the cloak on its own clock; here RT is the rifle while scoped, so the
                // hunter cloaks only when it means to.
                autopilot.SetCycledAbilityFilter(a => a is not CloakSeedWallActionSO);
                var captured = hunter;
                autopilot.SetExternalTargetProvider(() => captured.Steer);
            }
        }

        /// <summary>Releases every press the hunters hold and hands the autopilots back. Safe to call twice.</summary>
        public void Disarm()
        {
            for (int i = 0; i < _hunters.Count; i++)
            {
                var h = _hunters[i];
                if (h.Handler)
                {
                    if (h.TriggerHeld) h.Handler.StopShipControllerActionsReplicated(InputEvents.RightStickAction);
                    if (h.ScopeHeld) h.Handler.StopShipControllerActionsReplicated(InputEvents.LeftStickAction);
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
                Steer(h, self, aim);
                TickShot(h, self, aim, now);
            }
        }

        // ── Choosing ────────────────────────────────────────────────────────

        void Retarget(Hunter h, Transform self, float now)
        {
            h.NextRetargetAt = now + Mathf.Max(0.1f, SerpentGulagSettingsSO.Rung(_settings.aiRetargetSeconds, _intensity));
            h.Rival = ChooseRival(h, self);

            // A fresh aim error for the plan: a fixed angle off the true line, in a random direction across it.
            h.ErrorDegrees = SerpentGulagSettingsSO.Rung(_settings.aiAimErrorDegrees, _intensity) * (float)h.Rng.NextDouble();
            float spin = (float)(h.Rng.NextDouble() * Math.PI * 2.0);
            h.ErrorAxis = new Vector3(Mathf.Cos(spin), Mathf.Sin(spin), 0f);
        }

        IPlayer ChooseRival(Hunter h, Transform self)
        {
            float huntRange = SerpentGulagSettingsSO.Rung(_settings.aiHuntRange, _intensity);
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

        // ── Steering ────────────────────────────────────────────────────────

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

        void Steer(Hunter h, Transform self, Vector3 aim)
        {
            float cooling = h.Rifle ? h.Rifle.CooldownRemaining : 0f;
            float forageAt = SerpentGulagSettingsSO.Rung(_settings.aiForageMinCooldownSeconds, _intensity);
            if (h.Shot == ShotPhase.Idle && cooling >= forageAt && _nearestCrystal != null)
            {
                var crystal = _nearestCrystal(self.position);
                if (crystal.HasValue) { h.Steer = crystal.Value; h.BreakingAway = false; return; }
            }

            var rivalTf = h.Rival?.Vessel?.Transform;
            if (!rivalTf) { h.Steer = _centre; h.BreakingAway = false; return; }

            Vector3 away = self.position - rivalTf.position;
            float distance = away.magnitude;
            if (distance < _settings.aiMinAimDistance) h.BreakingAway = true;
            else if (distance > _settings.aiStandoffDistance) h.BreakingAway = false;

            h.Steer = h.BreakingAway && distance > 1e-3f
                ? self.position + away / distance * _settings.aiStandoffDistance
                : aim;
        }

        // ── Shooting ────────────────────────────────────────────────────────

        void TickShot(Hunter h, Transform self, Vector3 aim, float now)
        {
            switch (h.Shot)
            {
                case ShotPhase.Idle:
                {
                    if (now < h.NextShotAt || h.BreakingAway || !RifleReady(h)) return;
                    var rivalTf = h.Rival?.Vessel?.Transform;
                    if (!rivalTf) return;
                    float distance = Vector3.Distance(self.position, rivalTf.position);
                    float reach = h.Rifle ? h.Rifle.RangeUnits : 3000f;
                    if (distance < _settings.aiMinAimDistance || distance > reach) return;
                    if (Vector3.Angle(self.forward, aim - self.position) > _settings.aiScopeOpenAngle) return;
                    Press(h, InputEvents.LeftStickAction, ref h.ScopeHeld);
                    Enter(h, ShotPhase.Scoping, now);
                    return;
                }

                case ShotPhase.Scoping:
                {
                    bool lost = !h.Rival?.Vessel?.Transform || h.BreakingAway;
                    if (lost || now - h.ShotPhaseAt > _settings.aiShotGiveUpSeconds)
                    {
                        Release(h, InputEvents.LeftStickAction, ref h.ScopeHeld);
                        Enter(h, ShotPhase.Idle, now);
                        h.NextShotAt = now + 1f;
                        return;
                    }
                    float settle = SerpentGulagSettingsSO.Rung(_settings.aiScopeSettleSeconds, _intensity);
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
                    float chance = SerpentGulagSettingsSO.Rung(_settings.aiCloakAfterShotChance, _intensity);
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
