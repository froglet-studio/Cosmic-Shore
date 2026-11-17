using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Scarab Rally - the Scarab-only ring race, and the Scarab's TIME card. No ball: an open chain
    /// of switch rings is walked through the cell (Switchback's course), every pilot flies it in
    /// ORDER, and the first domain whose lead runner threads the last ring wins (golf: finish time).
    ///
    /// <para><b>The race is the Scarab's own handling.</b> The throttle is an integrator (hold RT and
    /// you keep gaining speed up to 216 u/s; let go and you shed 120 u/s every second), the drift
    /// decouples the nose from the course and keeps 100% of the speed, and the juke is a ~40 u
    /// sideways hop on the right stick. So a hairpin is a choice - lift and lose the speed, or drift it
    /// and keep it - and a mouth that sits off your line is a juke, not a correction. See
    /// <see cref="ScarabRallyCourse"/> for the ladder and SCARABRALLY.md for the numbers.</para>
    ///
    /// <para><b>What the mode does NOT add:</b> no ability, no cell, no metric, no course generator.
    /// Everything is <see cref="SwitchbackController"/>'s - the walk, the I3 walls and the I4 danger
    /// prisms (<see cref="SwitchbackFeatures"/>), the Regatta steering for the AI - with
    /// <see cref="ScarabRallyCourse"/> supplying the Scarab's cut.</para>
    /// </summary>
    public class ScarabRallyController : SwitchbackController
    {
        protected override string ModeName => "Scarab Rally";

        /// <summary>The I4 gauntlet is built for the Dolphin's key blast (sealed eyes, plugged tunnels): Scarab Rally keeps
        /// I4 as it was before it (SWITCHBACK.md "The I4 gauntlet").</summary>
        protected override bool BlastRunGauntlet => false;

        protected override RaceCourseSource CreateCourseSource() => new ScarabRallyCourseSource();

        /// <summary>Switchback became Blast Run two days after this race was cut (2026-10-29), and this class inherited it:
        /// block fields across every blocked leg, doors of danger rakes and Dolphin key crystals (which a Scarab's skimmer
        /// forges into a ball sitting on the line). Those ask for the Dolphin's blast and punish the Scarab's own verbs, a
        /// drift's wide line and a held throttle. So I1-I3 fly the bare ring course they were designed as
        /// (<see cref="ScarabRallyCourse.LaysBlastRun"/>); I4 keeps the field it had (the elemental loop's ladder step).</summary>
        protected override bool LaysBlastRun(int intensity) => ScarabRallyCourse.LaysBlastRun(intensity);

        /// <summary>A racing AI flies the rings and nothing else (Yash, 2026-10-26). Switchback's AI detours for Blast Run's
        /// keys; a Scarab has no key blast, and a crystal it touches becomes a ball in its own path.</summary>
        protected override bool AIDetoursForCrystals => false;

        [Header("AI nose")]
        [Tooltip("The Scarab AI's nose follow rate per second, one row per intensity (I1..I4), by difficulty: Scarab Rally's own " +
                 "table, read instead of Switchback's (which is tuned for the Dolphin). 1.5 = the fleet lag, higher = faster. " +
                 "Legendary trained in the Scarab's own race (time_ai_search.py, Scarab run3, 2026-11-17): 15/s at I1-I2 and " +
                 "18/s at I3-I4; on 32 fresh flights I4 went 164 to 120 s and I1 from Time 10 103 to 87 s, no DNF.")]
        [SerializeField] SwitchbackNoseFollow[] aiScarabNoseFollowByIntensity =
        {
            new(1.9f, 2.2f, 3f, 15f),    // I1
            new(2f, 2.4f, 3f, 15f),      // I2
            new(2.1f, 2.2f, 2.8f, 18f),  // I3
            new(1.8f, 2.2f, 3.5f, 18f),  // I4 (no gauntlet in Scarab Rally)
        };
        [Tooltip("u from the ring inside which the Scarab AI stops dodging (Switchback's aiNoDodgeNearRing, Scarab Rally's own " +
                 "value). Trained with the nose table above: 224.")]
        [SerializeField, Min(0f)] float aiScarabNoDodgeNearRing = 224f;

        protected override SwitchbackNoseFollow[] AINoseFollowTable => aiScarabNoseFollowByIntensity;
        protected override float AINoDodgeNearRing => aiScarabNoDodgeNearRing;

        [Header("AI juke")]
        [Tooltip("The AI jukes when, flying on as it is, it would cross the next mouth's plane outside the mouth by no " +
                 "more than one juke (~40 u), and that crossing is at least this many seconds away (the dash takes 0.5 s).")]
        [SerializeField, Min(0f)] float aiJukeMinSeconds = 0.6f;
        [Tooltip("...and at most this many seconds away: further off, the steering still has time to fix the line.")]
        [SerializeField, Min(0f)] float aiJukeMaxSeconds = 1.4f;
        [Tooltip("How far inside the rim (u) the AI wants to cross: a line that grazes the rim counts as a miss.")]
        [SerializeField, Min(0f)] float aiJukeMargin = 6f;

        /// <summary>The Scarab AI holds full throttle and steers, so a mouth twisted off its line at I3 was a lift or a
        /// miss (TIME_GAME_FUN_SCORES.md round 3). The juke is the Scarab's own fix for that, and the autopilot can press
        /// it (<see cref="ScarabJukeController.TryAutopilotDash"/>, the same committed dash a person's stick fires), so
        /// the race tells its AI when. Server only, AI hulls only.</summary>
        protected override void OnServerTick()
        {
            base.OnServerTick();
            if (gameData == null || !gameData.IsTurnRunning || _course.Count == 0) return;
            var players = gameData.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                var vessel = p?.Vessel;
                var status = vessel?.VesselStatus;
                // TryGetComponent, not VesselStatus.AIPilot: that getter ADDS an autopilot to a person's hull.
                if (status is not Component c || !c || !c.TryGetComponent(out AIPilot autopilot) || !autopilot.AutoPilotEnabled)
                    continue;
                if (!vessel.Transform.TryGetComponent(out ScarabJukeController juke) || !juke.IsJukeArmed) continue;
                int index = p.RoundStats?.SwitchesThreaded ?? 0;
                if (index < 0 || index >= RaceLength) continue;
                var gate = _course[RingIndexFor(index)];
                if (ScarabRallyCourse.JukeShove(vessel.Transform.position, status.Course * status.Speed, gate,
                        aiJukeMinSeconds, aiJukeMaxSeconds, aiJukeMargin, out var shove))
                    juke.TryAutopilotDash(shove);
            }
        }
    }
}
