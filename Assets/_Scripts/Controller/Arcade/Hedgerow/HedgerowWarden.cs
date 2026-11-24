using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The AI pilot of Hedgerow: a WARDEN that runs in across a hedge, stops dead short of it and holds while its seed wall
    /// eats along the hedge, then flies on to the next. Server-only. Plan step: <c>mass-arcade</c>
    /// (Docs/AI_SYSTEM/AI_PLAN.md - each vessel's Mass AI, on its Mass arcade game).
    ///
    /// <para><b>Input only - the Serpent's kit is untouched.</b> The warden steers through
    /// <see cref="AIPilot.SetExternalTargetProvider"/> and presses ONE thing: the stop toggle (the hull's bound
    /// <see cref="ToggleTranslationModeActionSO"/>, found by capability), through
    /// <see cref="R_VesselActionHandler.PerformShipControllerActionsReplicated"/> so every peer resolves it - Hood's
    /// marksman's shape. Stopping makes the Serpent's latest trail prism a held seed, and a wall grows from it, stealing
    /// loose mass from its edge, for as long as the Serpent stays stopped (SERPENT_SEED_WALL.md, "The stance's wall").
    /// The autopilot's own ability cycler is kept off the stop toggle and the cloak, so the warden decides when the hull
    /// stops.</para>
    ///
    /// <para><b>The loop.</b></para>
    /// <list type="number">
    /// <item><b>Pick a target</b> on a slow clock: the nearest hedge with prisms left (kept until it stops there), or -
    /// while its domain trails, or late in the round - a RAID on the densest rival mass
    /// (<see cref="Cell.GetExplosionTarget"/>), where its wall eats the rival's trail.</item>
    /// <item><b>Line up</b> <see cref="HedgerowSettingsSO.aiRunUp"/> back along the approach, then <b>run in</b> across the
    /// target, so the heading at the stop - the wall's normal - puts the wall in the hedge's plane.</item>
    /// <item><b>Stop</b> <see cref="HedgerowSettingsSO.aiStopShort"/> short of it, and <b>hold</b> for the rung's
    /// <see cref="HedgerowSettingsSO.aiHoldSeconds"/> (or until the hedge is eaten), then press again and fly on.</item>
    /// </list>
    ///
    /// <para><b>What it does not model</b> (HEDGEROW.md): the cloak (a super-shielded seed that grows a wall while the
    /// Serpent flies on) - a human's edge; shooting a rival's seed; or a rival closing on it while it is stopped.</para>
    ///
    /// <para><b>Difficulty is the seat's AI level, never the intensity</b> (<c>AIDifficulty.cs</c>).</para>
    /// </summary>
    public sealed class HedgerowWarden
    {
        const float PressHoldSeconds = 0.15f;
        const float MinHoldSeconds = 3f;
        const float LineUpReach = 50f;

        /// <summary>Answers where hedge <c>i</c> is, which way to come at it, and how many of its prisms are still loose.</summary>
        public interface IFieldView
        {
            int RowCount { get; }
            Vector3 RowMidpoint(int row);
            Vector3 RowApproach(int row);
            int Unclaimed(int row);
        }

        enum Phase { Pick = 0, LineUp = 1, Run = 2, Hold = 3 }

        sealed class Warden
        {
            public IPlayer Player;
            public int Level;
            public IVesselStatus Status;
            public AIPilot Autopilot;
            public R_VesselActionHandler Handler;
            public Vector3 Steer;

            public Phase Phase;
            public int Row = -1;
            public bool Raid;
            public Vector3 Target;
            public Vector3 Approach;
            public float RetargetAt;
            public float HoldStart;
            public float HoldUntil;
            public float NextStanceTryAt;
            public float ReleaseStanceAt = -1f;
            public bool StanceHeld;
            public bool WantStance;
        }

        readonly List<Warden> _wardens = new();
        HedgerowSettingsSO _settings;
        IFieldView _field;
        Func<Domains, int, bool> _shouldRaid;
        Func<Domains, Vector3?> _raidTarget;
        InputEvents _stanceInput;
        bool _hasStanceInput;

        public bool IsArmed => _wardens.Count > 0;

        /// <summary>Arms every AI Serpent on the roster. <paramref name="shouldRaid"/> answers whether a domain at an AI
        /// level should raid right now; <paramref name="raidTarget"/> where the densest rival mass is (or null).</summary>
        public void Arm(GameDataSO gameData, HedgerowSettingsSO settings, IFieldView field,
                        Func<Domains, int, bool> shouldRaid, Func<Domains, Vector3?> raidTarget)
        {
            Disarm();
            if (gameData == null || settings == null || field == null) return;
            _settings = settings;
            _field = field;
            _shouldRaid = shouldRaid;
            _raidTarget = raidTarget;

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

                // The stop toggle is found by capability, not by a hard-wired button.
                if (!_hasStanceInput)
                    _hasStanceInput = status.ActionHandler.TryGetBoundAction<ToggleTranslationModeActionSO>(out _, out _stanceInput);

                var warden = new Warden
                {
                    Player = p,
                    Level = (int)gameData.AIDifficultyFor(p),
                    Status = status,
                    Autopilot = autopilot,
                    Handler = status.ActionHandler,
                    Steer = status.Transform.position + status.Transform.forward * 200f,
                };
                _wardens.Add(warden);
                autopilot.SetCycledAbilityFilter(a => a is not CosmicShore.UI.CloakSeedWallActionSO
                                                      && a is not ToggleTranslationModeActionSO);
                var captured = warden;
                autopilot.SetExternalTargetProvider(() => captured.Steer);
            }
        }

        /// <summary>Releases every press and hands every autopilot back. Safe to call twice. A warden left stopped
        /// stays stopped until the turn's end lifts the stance, as a human's would.</summary>
        public void Disarm()
        {
            for (int i = 0; i < _wardens.Count; i++)
            {
                var w = _wardens[i];
                if (w.Handler && w.StanceHeld && _hasStanceInput) w.Handler.StopShipControllerActionsReplicated(_stanceInput);
                w.StanceHeld = false;
                if (w.Autopilot)
                {
                    w.Autopilot.ClearExternalTargetProvider();
                    w.Autopilot.SetCycledAbilityFilter(null);
                }
            }
            _wardens.Clear();
        }

        public void Tick()
        {
            if (_settings == null || _field == null) return;
            float now = Time.time;
            for (int i = 0; i < _wardens.Count; i++)
            {
                var w = _wardens[i];
                var self = w.Player?.Vessel?.Transform;
                if (!self || !w.Handler || !w.Autopilot || !w.Autopilot.AutoPilotEnabled) continue;
                Plan(w, self, now);
                TickStance(w, now);
            }
        }

        bool Stopped(Warden w) => w.Status != null && w.Status.IsTranslationRestricted;

        void Plan(Warden w, Transform self, float now)
        {
            Vector3 at = self.position;
            switch (w.Phase)
            {
                case Phase.Pick:
                    if (!Pick(w, at, now)) { w.Steer = at + self.forward * 200f; return; }
                    w.Phase = Phase.LineUp;
                    goto case Phase.LineUp;

                case Phase.LineUp:
                {
                    if (Stale(w, now)) { w.Phase = Phase.Pick; return; }
                    Vector3 lineUp = Stop(w) - w.Approach * _settings.aiRunUp;
                    w.Steer = lineUp;
                    if ((lineUp - at).sqrMagnitude <= LineUpReach * LineUpReach) w.Phase = Phase.Run;
                    return;
                }

                case Phase.Run:
                {
                    if (Stale(w, now)) { w.Phase = Phase.Pick; return; }
                    // Aim through the target so the heading at the stop is the approach.
                    w.Steer = w.Target + w.Approach * 100f;
                    Vector3 to = w.Target - at;
                    float ahead = Vector3.Dot(to, w.Approach);
                    float lateral = (to - w.Approach * ahead).magnitude;
                    if (ahead < 0f || lateral > _settings.aiStopLateral * 2f)
                    {
                        w.Phase = Phase.LineUp;   // overshot or drifted off: line up again
                        return;
                    }
                    if (ahead > StopShort(w) || lateral > _settings.aiStopLateral) return;
                    w.WantStance = true;
                    w.HoldStart = now;
                    w.HoldUntil = now + HedgerowSettingsSO.Rung(_settings.aiHoldSeconds, w.Level);
                    w.Phase = Phase.Hold;
                    return;
                }

                case Phase.Hold:
                {
                    w.Steer = w.Target + w.Approach * 100f;   // keep the nose across the hedge while stopped
                    // A hedge eaten bare frees the warden early, but never before its wall had a few seconds to land.
                    bool eaten = !w.Raid && w.Row >= 0 && _field.Unclaimed(w.Row) <= 0 && now >= w.HoldStart + MinHoldSeconds;
                    if (now < w.HoldUntil && !eaten) return;
                    w.WantStance = false;
                    if (Stopped(w)) return;   // wait for the hull to fly again before picking the next target
                    w.Row = -1;
                    w.Phase = Phase.Pick;
                    return;
                }
            }
        }

        float StopShort(Warden w) => w.Raid ? _settings.aiRaidStopShort : _settings.aiStopShort;

        Vector3 Stop(Warden w) => w.Target - w.Approach * StopShort(w);

        /// <summary>A hedge whose prisms are all gone, or a target held past its retarget clock, is dropped.</summary>
        bool Stale(Warden w, float now) =>
            now >= w.RetargetAt || (!w.Raid && (w.Row < 0 || _field.Unclaimed(w.Row) <= 0));

        bool Pick(Warden w, Vector3 at, float now)
        {
            w.RetargetAt = now + Mathf.Max(1f, HedgerowSettingsSO.Rung(_settings.aiRetargetSeconds, w.Level))
                               + _settings.aiDecisionSeconds;
            var domain = w.Player.Domain;
            if (_shouldRaid != null && _shouldRaid(domain, w.Level) && _raidTarget?.Invoke(domain) is Vector3 raid)
            {
                w.Raid = true;
                w.Row = -1;
                w.Target = raid;
                Vector3 from = raid - at;
                w.Approach = from.sqrMagnitude > 1e-3f ? from.normalized : Vector3.forward;
                return true;
            }

            w.Raid = false;
            int best = -1;
            float bestSqr = float.MaxValue;
            for (int r = 0; r < _field.RowCount; r++)
            {
                if (_field.Unclaimed(r) <= 0) continue;
                float d = (_field.RowMidpoint(r) - at).sqrMagnitude;
                if (d >= bestSqr) continue;
                bestSqr = d;
                best = r;
            }
            if (best < 0) return false;
            w.Row = best;
            w.Target = _field.RowMidpoint(best);
            w.Approach = _field.RowApproach(best);
            return true;
        }

        /// <summary>Press the stop toggle until the stance matches what the plan wants, read back off the hull.</summary>
        void TickStance(Warden w, float now)
        {
            if (!_hasStanceInput) return;
            if (w.ReleaseStanceAt >= 0f && now >= w.ReleaseStanceAt)
            {
                w.ReleaseStanceAt = -1f;
                w.Handler.StopShipControllerActionsReplicated(_stanceInput);
                w.StanceHeld = false;
            }
            if (Stopped(w) == w.WantStance || now < w.NextStanceTryAt || w.StanceHeld) return;
            w.Handler.PerformShipControllerActionsReplicated(_stanceInput);
            w.StanceHeld = true;
            w.ReleaseStanceAt = now + PressHoldSeconds;
            w.NextStanceTryAt = now + _settings.aiStanceRetrySeconds;
        }
    }
}
