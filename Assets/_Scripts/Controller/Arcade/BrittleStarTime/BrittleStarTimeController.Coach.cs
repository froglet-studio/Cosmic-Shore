using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Brittle Star Time's race coach (server; <see cref="BrittleStarTimeCoach"/>): when each AI Brittle Star holds Speed
    /// Mode, which line it flies, and how fast its nose follows, by its seat's difficulty.
    ///
    /// <para><b>Input only.</b> The wish goes through <see cref="BrittleStarExecutor.SetAutopilotSpeedWish"/>, which the
    /// hull's own autopilot acts on by pressing the same replicated Speed Mode control a person presses - the path the
    /// hull's AI boost policy uses. In this mode the policy's own straight law is stood down by its mode variant
    /// (<c>Resources/AIBoostPolicies/BrittleStarAIBoostPolicy_BrittleStarTime</c>, engage runway 999 s), so the coach is
    /// the only writer; and since the driver's <c>Release</c> and the executor's own initialisation null the wish, the
    /// coach states it EVERY tick (the setter returns at once on an unchanged value). Rival mass ahead still wins: the
    /// autopilot's own wall rule (Charge, Space) is untouched. The vessel is not changed.</para>
    ///
    /// <para><b>The line</b> is <see cref="BrittleStarTimeLine.SteerPoint"/>, answered through <see cref="TryRaceLine"/> -
    /// asked after the platform's difficulty belief and its missed-mouth break-off, so an Easy pilot's mistakes and a
    /// missed ring are still flown the gate way. <b>The nose follow</b> per difficulty and rung is a mode setting on the
    /// AI's own steering, set on every arming as Redline and Switchback set theirs.</para>
    ///
    /// <para><b>A scaffold, not the genome</b> (Plan step: time-arcade): the coach's numbers are hand-set and searched
    /// offline (<c>brittle_star_time_course_harness --tune</c>) while Froglet Compute is paused; the universal Brittle Star
    /// Time genome replaces them when the plan trains it.</para>
    /// </summary>
    public partial class BrittleStarTimeController
    {
        [Header("Race coach (server)")]
        [Tooltip("Coach every AI Brittle Star's Speed Mode, line and nose follow (BrittleStarTimeCoach). Off = the hull's " +
                 "own AI exactly as before.")]
        [SerializeField] bool coachOn = true;

        sealed class Coached
        {
            public BrittleStarExecutor Star;
            public bool Lit;
            public int Seat;
        }

        readonly Dictionary<IPlayer, Coached> _coached = new();

        /// <summary>An AI seat's skill (its lobby difficulty).</summary>
        BrittleStarTimeCoach.Skill SkillOf(IPlayer pilot) =>
            BrittleStarTimeCoach.SkillFor(gameData ? (int)gameData.AIDifficultyFor(pilot) : (int)AIDifficulty.Medium);

        static bool IsAutopilot(IPlayer p) =>
            p?.Vessel?.VesselStatus is Component status && status &&
            status.TryGetComponent(out AIPilot autopilot) && autopilot.AutoPilotEnabled;

        void ArmCoach(IPlayer pilot)
        {
            if (!coachOn) return;
            // Set on EVERY arming: the transformer keeps the last mode's rate otherwise (ResetTransformer leaves it).
            var transformer = pilot?.Vessel?.VesselStatus?.VesselTransformer;
            if (transformer == null) return;
            transformer.AINoseFollowPerSecond = BrittleStarTimeCoach.NoseFollowFor(Intensity, SkillOf(pilot));
        }

        Coached CoachedOf(IPlayer pilot, int seat)
        {
            if (_coached.TryGetValue(pilot, out var c) && c.Star) return c;
            var hull = pilot?.Vessel?.Transform;
            var star = hull ? hull.GetComponentInChildren<BrittleStarExecutor>(true) : null;
            if (!star) return null;
            c = new Coached { Star = star, Seat = seat };
            _coached[pilot] = c;
            return c;
        }

        void TickCoach()
        {
            if (!coachOn || gameData == null) return;
            if (!gameData.IsTurnRunning || _course.Count == 0)
            {
                if (_coached.Count > 0) ReleaseEveryCoachedWish();
                return;
            }
            var players = gameData.Players;
            if (players == null) return;
            for (int seat = 0; seat < players.Count; seat++)
            {
                var pilot = players[seat];
                if (!IsAutopilot(pilot))
                {
                    // A seat a person took back: its last wish goes, so the hull's own rules hold again.
                    if (pilot != null && _coached.TryGetValue(pilot, out var was))
                    {
                        if (was?.Star) was.Star.SetAutopilotSpeedWish(null);
                        _coached.Remove(pilot);
                    }
                    continue;
                }
                var c = CoachedOf(pilot, seat);
                var hull = pilot.Vessel?.Transform;
                if (c == null || !hull) continue;
                int threaded = pilot.RoundStats?.SwitchesThreaded ?? 0;
                if (threaded >= RaceLength)
                {
                    c.Lit = false;
                    c.Star.SetAutopilotSpeedWish(false);
                    continue;
                }
                c.Lit = BrittleStarTimeCoach.WantsSpeed(_course, RingIndexFor(threaded), hull.position, hull.forward,
                                                        TopSpeed, c.Seat, SkillOf(pilot), Intensity, c.Lit);
                c.Star.SetAutopilotSpeedWish(c.Lit);
            }
        }

        void ReleaseEveryCoachedWish()
        {
            foreach (var c in _coached.Values)
                if (c?.Star) c.Star.SetAutopilotSpeedWish(null);
            _coached.Clear();
        }

        /// <summary>The coached AI's line (<see cref="BrittleStarTimeLine.SteerPoint"/>), and the ring after as its next
        /// leg. Server only, as all AI steering.</summary>
        protected override bool TryRaceLine(IPlayer pilot, Vector3 self, out Vector3 aim, out Vector3 after)
        {
            aim = after = default;
            if (!coachOn || _course.Count == 0 || pilot == null) return false;
            int threaded = pilot.RoundStats?.SwitchesThreaded ?? 0;
            if (threaded >= RaceLength) return false;
            int ring = RingIndexFor(threaded);
            aim = BrittleStarTimeLine.SteerPoint(_course, ring, self);
            after = _course[RingIndexFor(Mathf.Min(threaded + 1, RaceLength - 1))].Position;
            return true;
        }
    }
}
