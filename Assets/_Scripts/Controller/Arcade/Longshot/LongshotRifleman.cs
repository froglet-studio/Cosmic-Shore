using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The AI pilot of Longshot: a RIFLEMAN that lines up along a lance and fires its sniper round down the run, so the
    /// one round breaks every prism on it. Server-only. Plan step: <c>space-arcade</c> (Docs/AI_SYSTEM/AI_PLAN.md - each
    /// vessel's Space AI, on its Space arcade game).
    ///
    /// <para><b>Input only - the Serpent's kit is untouched.</b> The rifleman steers through
    /// <see cref="AIPilot.SetExternalTargetProvider"/> and presses LT (the scope) and RT (the round, while scoped) through
    /// <see cref="R_VesselActionHandler.PerformShipControllerActionsReplicated"/> so every peer resolves the shot - Hood's
    /// marksman's shape. The autopilot's own ability cycler is kept off the cloak and the stop toggle (RT unscoped is the
    /// cloak), so the rifleman decides every press.</para>
    ///
    /// <para><b>The loop.</b></para>
    /// <list type="number">
    /// <item><b>Pick a run</b>: the straight run with the most standing prisms per unit of distance to its nearer end -
    /// or, while its domain trails, the nearest rival, whose trail behind it is a lance of the rival's colour.</item>
    /// <item><b>Line up</b> <see cref="LongshotSettingsSO.aiStandOff"/> back along the run's line, then follow a carrot
    /// on the line (<see cref="LongshotSettingsSO.aiCarrot"/> ahead), which pulls the hull onto it.</item>
    /// <item><b>Scope</b> once within <see cref="LongshotSettingsSO.aiScopeLateral"/> of the line and the rifle is ready;
    /// wait the rung's settle; <b>fire</b> as soon as the sniper's own cone (<see cref="LongshotField.InCone"/>) would
    /// take the rung's <see cref="LongshotSettingsSO.aiFireFraction"/> of the run's standing prisms. Give up after
    /// <see cref="LongshotSettingsSO.aiShotGiveUpSeconds"/> and line up again.</item>
    /// </list>
    ///
    /// <para><b>What it does not model</b> (LONGSHOT.md): leading a moving rival, shooting from beyond the run's line
    /// (a long angled shot through two runs), or the cloak and the wall.</para>
    ///
    /// <para><b>Difficulty is the seat's AI level, never the intensity</b> (<c>AIDifficulty.cs</c>).</para>
    /// </summary>
    public sealed class LongshotRifleman
    {
        const float PressHoldSeconds = 0.2f;
        const float LineUpReach = 60f;
        const float RivalTrailLength = 500f;

        /// <summary>Answers where run <c>s</c> is, how many of its prisms stand, and how many stand inside a cone.</summary>
        public interface IFieldView
        {
            int SegmentCount { get; }
            Vector3 SegmentStart(int segment);
            Vector3 SegmentEnd(int segment);
            int Standing(int segment);
            int StandingInCone(int segment, Vector3 apex, Vector3 dir, float range, float halfAngleDegrees, float minRadius);
        }

        enum Phase { Pick = 0, LineUp = 1, Track = 2, Scoping = 3, Fired = 4 }

        sealed class Rifleman
        {
            public IPlayer Player;
            public int Level;
            public IVesselStatus Status;
            public AIPilot Autopilot;
            public R_VesselActionHandler Handler;
            public SniperScopeActionExecutor Scope;
            public SniperShotActionExecutor Rifle;
            public Vector3 Steer;

            public Phase Phase;
            public int Segment = -1;
            public IPlayer Rival;
            public Vector3 A, B;           // the run's near end (where the round enters) and far end
            public float RetargetAt;
            public float PhaseAt;
            public float NextDecisionAt;
            public bool HuntRival;
            public bool ScopeHeld, TriggerHeld;
        }

        readonly List<Rifleman> _riflemen = new();
        LongshotSettingsSO _settings;
        IFieldView _field;
        GameDataSO _gameData;
        Func<Domains, int, bool> _shouldHuntRival;

        public bool IsArmed => _riflemen.Count > 0;

        /// <summary>Arms every AI Serpent on the roster. <paramref name="shouldHuntRival"/> answers whether a domain at
        /// an AI level should go for a rival's trail right now.</summary>
        public void Arm(GameDataSO gameData, LongshotSettingsSO settings, IFieldView field,
                        Func<Domains, int, bool> shouldHuntRival)
        {
            Disarm();
            if (gameData == null || settings == null || field == null) return;
            _gameData = gameData;
            _settings = settings;
            _field = field;
            _shouldHuntRival = shouldHuntRival;

            var players = gameData.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p == null || !p.IsInitializedAsAI) continue;
                var status = p.Vessel?.VesselStatus;
                if (status == null || !status.Transform || status.ActionHandler == null) continue;
                // TryGetComponent, not VesselStatus.AIPilot: that getter ADDS an autopilot to a hull without one.
                var autopilot = status is Component c && c && c.TryGetComponent(out AIPilot ai) ? ai : null;
                if (autopilot == null) continue;
                var root = status.Transform;
                var rifle = root.GetComponentInChildren<SniperShotActionExecutor>(true);
                if (!rifle) continue;   // the card seats only Serpents

                var rifleman = new Rifleman
                {
                    Player = p,
                    Level = (int)gameData.AIDifficultyFor(p),
                    Status = status,
                    Autopilot = autopilot,
                    Handler = status.ActionHandler,
                    Scope = root.GetComponentInChildren<SniperScopeActionExecutor>(true),
                    Rifle = rifle,
                    Steer = root.position + root.forward * 200f,
                };
                _riflemen.Add(rifleman);
                // RT unscoped is the cloak: the cycler would cloak on its own clock and waste the trigger.
                autopilot.SetCycledAbilityFilter(a => a is not CosmicShore.UI.CloakSeedWallActionSO
                                                      && a is not ToggleTranslationModeActionSO);
                var captured = rifleman;
                autopilot.SetExternalTargetProvider(() => captured.Steer);
            }
        }

        /// <summary>Releases every press and hands every autopilot back. Safe to call twice.</summary>
        public void Disarm()
        {
            for (int i = 0; i < _riflemen.Count; i++)
            {
                var r = _riflemen[i];
                Release(r, InputEvents.RightStickAction, ref r.TriggerHeld);
                Release(r, InputEvents.LeftStickAction, ref r.ScopeHeld);
                if (r.Autopilot)
                {
                    r.Autopilot.ClearExternalTargetProvider();
                    r.Autopilot.SetCycledAbilityFilter(null);
                }
            }
            _riflemen.Clear();
        }

        public void Tick()
        {
            if (_settings == null || _field == null) return;
            float now = Time.time;
            for (int i = 0; i < _riflemen.Count; i++)
            {
                var r = _riflemen[i];
                var self = r.Player?.Vessel?.Transform;
                if (!self || !r.Handler || !r.Autopilot || !r.Autopilot.AutoPilotEnabled || !r.Rifle)
                {
                    LowerScope(r);
                    continue;
                }
                if (now >= r.NextDecisionAt)
                {
                    r.NextDecisionAt = now + _settings.aiDecisionSeconds;
                    r.HuntRival = _shouldHuntRival != null && _shouldHuntRival(r.Player.Domain, r.Level);
                }
                Plan(r, self, now);
            }
        }

        void Plan(Rifleman r, Transform self, float now)
        {
            Vector3 at = self.position;
            if (r.Phase != Phase.Pick && r.Phase != Phase.Fired && !RefreshTarget(r, now))
            {
                LowerScope(r);
                r.Phase = Phase.Pick;
            }

            switch (r.Phase)
            {
                case Phase.Pick:
                    if (!Pick(r, at, now)) { r.Steer = at + self.forward * 200f; return; }
                    Enter(r, Phase.LineUp, now);
                    goto case Phase.LineUp;

                case Phase.LineUp:
                {
                    Vector3 dir = (r.B - r.A).normalized;
                    Vector3 lineUp = r.A - dir * _settings.aiStandOff;
                    r.Steer = lineUp;
                    if ((lineUp - at).sqrMagnitude <= LineUpReach * LineUpReach) Enter(r, Phase.Track, now);
                    return;
                }

                case Phase.Track:
                {
                    Vector3 dir = (r.B - r.A).normalized;
                    float t = Vector3.Dot(at - r.A, dir);
                    if (t > (r.B - r.A).magnitude * 0.5f) { Enter(r, Phase.LineUp, now); return; }   // flew past half the run
                    r.Steer = r.A + dir * (t + _settings.aiCarrot);
                    float lateral = (at - (r.A + dir * t)).magnitude;
                    if (lateral > _settings.aiScopeLateral || r.Rifle.CooldownRemaining > 0f) return;
                    Press(r, InputEvents.LeftStickAction, ref r.ScopeHeld);
                    Enter(r, Phase.Scoping, now);
                    return;
                }

                case Phase.Scoping:
                {
                    Vector3 dir = (r.B - r.A).normalized;
                    float t = Vector3.Dot(at - r.A, dir);
                    r.Steer = r.A + dir * (t + _settings.aiCarrot);
                    if (now - r.PhaseAt > _settings.aiShotGiveUpSeconds)
                    {
                        LowerScope(r);
                        Enter(r, Phase.LineUp, now);
                        return;
                    }
                    float settle = LongshotSettingsSO.Rung(_settings.aiScopeSettleSeconds, r.Level);
                    if ((r.Scope && !r.Scope.IsScoped) || now - r.PhaseAt < settle) return;
                    if (!WouldTake(r, self)) return;
                    Press(r, InputEvents.RightStickAction, ref r.TriggerHeld);
                    Enter(r, Phase.Fired, now);
                    return;
                }

                case Phase.Fired:
                    if (now - r.PhaseAt < PressHoldSeconds) return;
                    LowerScope(r);
                    r.Segment = -1;
                    r.Rival = null;
                    Enter(r, Phase.Pick, now);
                    return;
            }
        }

        /// <summary>Would the round, fired now along the nose, take the rung's share of the target?</summary>
        bool WouldTake(Rifleman r, Transform self)
        {
            float half = r.Rifle.ConeHalfAngleDegrees;
            float range = r.Rifle.RangeUnits;
            const float minRadius = 6f;   // SniperShotAction.minPathRadius (the generator holds the asset to it)
            if (r.Rival != null)
            {
                var rivalTf = r.Rival.Vessel?.Transform;
                return rivalTf && LongshotField.InCone(rivalTf.position, self.position, self.forward, range, half, minRadius);
            }
            int standing = _field.Standing(r.Segment);
            if (standing <= 0) return false;
            int take = _field.StandingInCone(r.Segment, self.position, self.forward, range, half, minRadius);
            return take >= Mathf.Max(1, Mathf.CeilToInt(standing * LongshotSettingsSO.Rung(_settings.aiFireFraction, r.Level)));
        }

        /// <summary>Re-reads the target's line (a rival's trail moves with the rival); false drops it.</summary>
        bool RefreshTarget(Rifleman r, float now)
        {
            if (now >= r.RetargetAt && r.Phase != Phase.Scoping) return false;
            if (r.Rival != null)
            {
                var tf = r.Rival.Vessel?.Transform;
                if (!tf) return false;
                r.B = tf.position;
                r.A = tf.position - tf.forward * RivalTrailLength;
                return true;
            }
            return r.Segment >= 0 && _field.Standing(r.Segment) > 0;
        }

        bool Pick(Rifleman r, Vector3 at, float now)
        {
            r.RetargetAt = now + Mathf.Max(2f, LongshotSettingsSO.Rung(_settings.aiRetargetSeconds, r.Level));
            r.Rival = null;
            r.Segment = -1;

            if (r.HuntRival && NearestRival(r, at) is { } rival)
            {
                r.Rival = rival;
                return RefreshTarget(r, now);
            }

            // The run worth the most per unit of travel: standing prisms over the distance to its nearer end.
            float best = 0f;
            for (int s = 0; s < _field.SegmentCount; s++)
            {
                int standing = _field.Standing(s);
                if (standing <= 0) continue;
                Vector3 a = _field.SegmentStart(s), b = _field.SegmentEnd(s);
                float da = (a - at).magnitude, db = (b - at).magnitude;
                float value = standing / (Mathf.Min(da, db) + 200f);
                if (value <= best) continue;
                best = value;
                r.Segment = s;
                // The round enters at the nearer end and runs out along the run.
                r.A = da <= db ? a : b;
                r.B = da <= db ? b : a;
            }
            return r.Segment >= 0;
        }

        IPlayer NearestRival(Rifleman r, Vector3 at)
        {
            IPlayer best = null;
            float bestSqr = float.MaxValue;
            var players = _gameData.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p == null || p == r.Player || p.Domain == r.Player.Domain) continue;
                var tf = p.Vessel?.Transform;
                if (!tf) continue;
                float d = (tf.position - at).sqrMagnitude;
                if (d >= bestSqr) continue;
                bestSqr = d;
                best = p;
            }
            return best;
        }

        static void Enter(Rifleman r, Phase phase, float now)
        {
            r.Phase = phase;
            r.PhaseAt = now;
        }

        void LowerScope(Rifleman r)
        {
            Release(r, InputEvents.RightStickAction, ref r.TriggerHeld);
            Release(r, InputEvents.LeftStickAction, ref r.ScopeHeld);
        }

        static void Press(Rifleman r, InputEvents input, ref bool held)
        {
            if (held) return;
            r.Handler.PerformShipControllerActionsReplicated(input);
            held = true;
        }

        static void Release(Rifleman r, InputEvents input, ref bool held)
        {
            if (!held) return;
            if (r.Handler) r.Handler.StopShipControllerActionsReplicated(input);
            held = false;
        }
    }
}
