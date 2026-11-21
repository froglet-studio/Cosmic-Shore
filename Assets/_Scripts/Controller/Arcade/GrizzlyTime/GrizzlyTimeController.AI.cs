using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Grizzly Time's AI (Docs/AI_SYSTEM/AI_PLAN.md step <c>time-arcade</c>; hand-set rules, a seed for the trained
    /// Grizzly Time genome): WHERE an AI Grizzly flies instead of the ring, and WHEN it presses Rush. Input only
    /// (Tools/Build/check_ai_no_state_writes.py scans this file): it answers an aim point and presses one control through
    /// the replicated press; the bombs stay the vessel's own autopilot and nothing here writes to a vessel.
    ///
    /// <para><b>Everything is ticked from <see cref="TryOverrideAim"/></b>, the one hook that runs every frame on the
    /// machine that FLIES the hull: the server for an AI seat, a guest for its own hull handed to the AI. Until 2026-11-20
    /// the gate clock was stamped in <c>OnServerTick</c>, which a guest never runs, so a guest-flown hull was "rescued"
    /// every 18 s whatever its progress.</para>
    /// </summary>
    public partial class GrizzlyTimeController
    {
        /// <summary>A hook not asked for this long (s) was not flying the hull (the AI was switched off and on again): its
        /// gate clock starts afresh.</summary>
        const float AIClockGapSeconds = 1f;

        /// <summary>One AI's race state: when it last cleared a gate, which gate's keg it gave up on, whether it is being
        /// steered back to its ring after a stall, and when it last pressed Rush.</summary>
        sealed class RacerAI
        {
            public int Threaded = -1;
            public float ThreadedAt;
            public float SeenAt = float.NegativeInfinity;
            public int GaveUpOn = -1;
            public bool Rescue;
            public float RescueSince;
            public float RushAt = float.NegativeInfinity;
            /// <summary>The gate whose standing keg this AI is ramming (-1: none).</summary>
            public int RamFor = -1;
        }

        readonly Dictionary<IPlayer, RacerAI> _aiState = new();

        RacerAI RacerFor(IPlayer pilot)
        {
            if (!_aiState.TryGetValue(pilot, out var state))
            {
                state = new RacerAI { ThreadedAt = Time.time };
                _aiState[pilot] = state;
            }
            return state;
        }

        protected override void OnRacerArmed(IPlayer pilot)
        {
            if (pilot != null) _aiState[pilot] = new RacerAI { ThreadedAt = Time.time };
        }

        /// <summary>
        /// Where an AI Grizzly flies instead of the ring (<see cref="KegAim"/>), with its gate clock and its Rush ticked on
        /// the way: this is the only hook that runs on whichever machine flies the hull.
        /// </summary>
        protected override bool TryOverrideAim(IPlayer pilot, out Vector3 target)
        {
            target = default;
            var self = HullOf(pilot);
            var stats = pilot?.RoundStats;
            if (!self || stats == null || _course.Count == 0) return false;
            int index = stats.SwitchesThreaded;
            if (index < 0 || index >= RaceLength) return false;
            int ring = RingIndexFor(index);
            if (ring < 0 || ring >= _course.Count) return false;
            var state = RacerFor(pilot);
            float now = Time.time;

            // The gate clock: a new gate, a turn that is not running, or a hook that was not being asked all start it
            // afresh, so only a hull that is flying and not clearing gates can stall.
            bool running = gameData != null && gameData.IsTurnRunning;
            bool newLeg = index != state.Threaded;
            if (!running || newLeg || now - state.SeenAt > AIClockGapSeconds)
            {
                state.Threaded = index;
                state.ThreadedAt = now;
                state.Rescue = false;
            }
            state.SeenAt = now;

            bool overridden = KegAim(pilot, self.position, index, ring, state, now, out target);
            if (!running || state.Rescue) return overridden;
            if (overridden && state.RamFor == index)
                TickRush(pilot, self, state, target, (target - self.position).magnitude, now, ram: true);
            else if (aiRushes && TryRushLine(pilot, self.position, overridden, target, newLeg, out Vector3 steer, out float run))
                TickRush(pilot, self, state, steer, run, now, ram: false);
            return overridden;
        }

        /// <summary>
        /// The point the hull is really steering at, and how much straight it has before it must turn. The keg aim when
        /// this hook answered. Otherwise the autopilot's own last steering point (one frame old: it already holds the
        /// platform's approach point, the point through the mouth and any difficulty belief), with the run cut at the
        /// ring, so a Rush ends before the turn-in and never carries the hull through a mouth it was not lined up on.
        /// The ring's centre would be the wrong question: on the approach the nose is on the approach point, 13-23
        /// degrees off the ring's bearing. False on the first frame of a leg (the steering point is still the last
        /// gate's), before the mode has reported, and while a difficulty mistake hides or moves the ring: the press
        /// must not read what the belief hides.
        /// </summary>
        bool TryRushLine(IPlayer pilot, Vector3 at, bool overridden, Vector3 kegAim, bool newLeg, out Vector3 steer, out float run)
        {
            steer = kegAim;
            run = (kegAim - at).magnitude;
            if (overridden) return true;
            var status = pilot.Vessel.VesselStatus;
            var ai = status != null ? status.AIPilot : null;
            if (newLeg || !ai || !ai.Objective.Has || ai.Objective.Mistake) return false;
            steer = ai.TargetPosition;
            run = Mathf.Min((steer - at).magnitude, (ai.ObjectivePosition - at).magnitude);
            return true;
        }

        /// <summary>
        /// The aim (Docs/GrizzlyTime/POWDER_KEGS_SPEC.md section 5):
        /// <list type="number">
        /// <item><b>Stall rescue.</b> <see cref="GrizzlyTimeKegs.AIStallSeconds"/> without a gate: a run-up point back along
        /// the ring's axis, then the mouth (handed back to the ring logic within <see cref="GrizzlyTimeKegs.AIStallHandBack"/>
        /// u of it).</item>
        /// <item><b>An open keg</b> (anyone's blast or ram, its own included) within
        /// <see cref="GrizzlyTimeKegs.AIKegAimRange"/>: pass beside it down its exit lane, inside the credit radius.</item>
        /// <item>A <b>standing</b> keg, plain or shielded: the mouth. It could ram the keg open, but the mouth is open, and
        /// an AI breaks only what it cannot pass. (Until 2026-11-21 it passed beside a standing PLAIN keg at I1-I2 for its
        /// bombs to open; with the 40 u blast that opened about one keg a race and ran the ladder backwards.)</item>
        /// </list>
        /// </summary>
        bool KegAim(IPlayer pilot, Vector3 at, int index, int ring, RacerAI state, float now, out Vector3 target)
        {
            target = default;
            if (state.RamFor != index) state.RamFor = -1;
            if (state.Rescue || now - state.ThreadedAt >= GrizzlyTimeKegs.AIStallSeconds)
            {
                var gate = _course[ring];
                Vector3 runUp = gate.Position - RaceCourseGeometry.SafeNormalize(gate.Axis, Vector3.forward) * GrizzlyTimeKegs.AIStallRunUp;
                if (!state.Rescue)
                {
                    CSDebug.LogVerbose(CSLogChannel.ArcadeMatch, $"[GrizzlyTime] stall rescue for an AI at gate {index}");
                    state.Rescue = true;
                    state.RescueSince = now;
                }
                bool reached = (at - runUp).sqrMagnitude <= GrizzlyTimeKegs.AIStallHandBack * GrizzlyTimeKegs.AIStallHandBack;
                if (!reached && now - state.RescueSince < GrizzlyTimeKegs.AIStallSeconds)
                {
                    target = runUp;
                    return true;
                }
                // At the run-up (or the rescue itself ran out): back to the ring logic for the MOUTH - this gate's keg is
                // given up, so the keg that stalled it cannot pull it back - with a fresh clock.
                state.Rescue = false;
                state.GaveUpOn = index;
                state.ThreadedAt = now;
                return false;
            }

            if (ring >= _kegs.Count || state.GaveUpOn == index) return false;
            var site = _kegs[ring];
            Vector3 pass = site.Keg.Centre + site.Keg.ExitDir * GrizzlyTimeKegs.AIKegPassOffset;
            if ((at - site.Keg.Centre).sqrMagnitude > GrizzlyTimeKegs.AIKegAimRange * GrizzlyTimeKegs.AIKegAimRange) return false;
            // A standing keg: ram it open with Rush when the line through its core is the shorter way round
            // (2026-11-21, Garrett: teach the AI to open kegs). Passing beside one for the bomb autopilot to open is not
            // tried: since the 20-80 blast a core needs a bomb frozen within 40 u, which the autopilot's fire clock does
            // not aim, and that pass cost about 55 s a race at I1-I2 and ran the ladder backwards.
            if (site.Standing)
            {
                Vector3 core = site.Keg.Centre;
                if (state.RamFor == index)
                {
                    if ((at - core).sqrMagnitude <= GrizzlyTimeKegs.AIRamGiveUp * GrizzlyTimeKegs.AIRamGiveUp)
                    {
                        state.GaveUpOn = index;
                        state.RamFor = -1;
                        return false;
                    }
                    target = core;
                    return true;
                }
                if (!aiRamsKegs || !RushReady(pilot)) return false;
                Vector3 next = _course[(ring + 1) % _course.Count].Position;
                if (!GrizzlyTimeKegs.AIRamPays(at, core, _course[ring].Position, next, aiKegRamSlack)) return false;
                state.RamFor = index;
                target = core;
                return true;
            }
            state.RamFor = -1;
            target = pass;
            return true;
        }

        /// <summary>
        /// The AI's Rush (<see cref="GrizzlyTimeKegs.AIShouldRush"/>): pace on a straight it is lined up on, pressed and
        /// let go in the same call (Rush is fire-and-forget) through the replicated press on the hull's own bound control,
        /// as the Urchin and Tiger Moth drivers do, so no release is ever left pending. Only the machine that owns the
        /// hull's input presses. The Rush's cost, cooldown and control are read off the vessel's own action, and the
        /// Energy's gain per tick off its resource: every peer runs the press against its OWN Energy, which ticks on its
        /// own phase, so the AI waits for one tick of headroom and a peer one tick behind still accepts it. A press the
        /// vessel refuses (no charge banked) does nothing and is tried again after the cooldown.
        /// </summary>
        /// <summary>Is a Rush banked for this AI's hull now (Energy for one press with a tick to spare)? Read off the
        /// vessel's own action and resource, as <see cref="TickRush"/> reads them.</summary>
        bool RushReady(IPlayer pilot)
        {
            var status = pilot.Vessel.VesselStatus;
            var handler = status != null ? status.ActionHandler : null;
            if (!handler || !handler.TryGetBoundAction<GrizzlyRushActionSO>(out var rush, out _)) return false;
            var resources = status.ResourceSystem ? status.ResourceSystem.Resources : null;
            if (resources == null || rush.EnergyIndex < 0 || rush.EnergyIndex >= resources.Count) return false;
            var energy = resources[rush.EnergyIndex];
            return energy.CurrentAmount >= rush.EnergyCost + energy.resourceGainRate - 1e-4f;
        }

        void TickRush(IPlayer pilot, Transform self, RacerAI state, Vector3 steer, float run, float now, bool ram)
        {
            var status = pilot.Vessel.VesselStatus;
            var handler = status != null ? status.ActionHandler : null;
            if (!handler || handler.IsSpawned && !handler.IsOwner) return;
            if (!handler.TryGetBoundAction<GrizzlyRushActionSO>(out var rush, out var input)) return;

            var resources = status.ResourceSystem ? status.ResourceSystem.Resources : null;
            var transformer = status.VesselTransformer;
            if (resources == null || rush.EnergyIndex < 0 || rush.EnergyIndex >= resources.Count || !transformer) return;
            var energy = resources[rush.EnergyIndex];

            float nose = Vector3.Angle(self.forward, steer - self.position);
            bool press = ram
                ? GrizzlyTimeKegs.AIShouldRam(nose, run, energy.CurrentAmount, rush.EnergyCost, energy.resourceGainRate,
                    now - state.RushAt, rush.CooldownSeconds)
                : GrizzlyTimeKegs.AIShouldRush(nose, run, transformer.VelocityShift.magnitude, energy.CurrentAmount,
                    rush.EnergyCost, energy.resourceGainRate, now - state.RushAt, rush.CooldownSeconds);
            if (!press) return;

            state.RushAt = now;
            handler.PerformShipControllerActionsReplicated(input);
            handler.StopShipControllerActionsReplicated(input);
        }
    }
}
