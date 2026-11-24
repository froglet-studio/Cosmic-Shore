using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Brittle Star Time - the Brittle Star-only circuit race, and its TIME card. A closed loop of
    /// switch rings flown in LAPS; the first domain whose lead runner threads the last gate of the
    /// last lap wins, scored on finish TIME.
    ///
    /// <para><b>The mode is four questions about Speed Mode and the turn, one per intensity.</b> The Brittle Star
    /// cruises at 45 u/s; Speed Mode (A) runs it at six times that, 270 u/s, on a ~145 u circle against cruise's ~30 u.
    /// I1 Gearbox: hold Speed or shift down. I2 Feather slalom: tap A for a speed between. I3 Coast-in hairpins: let go
    /// early and coast in. I4 Corkscrew: start at Time 5 and roll (Speed Roll) to hold Speed through a coil. See
    /// <see cref="BrittleStarTimeCourse"/> and BRITTLESTARTIME.md.</para>
    ///
    /// <para><b>The Shift Line (2026-11-16, BRITTLESTARTIME.md section 0).</b> Three partials on top of the race:
    /// <list type="bullet">
    /// <item><c>.Line</c> - a let-go gate (two columns of shielded Blue posts) before every ring whose corner is tighter
    /// than Speed Mode carries (<see cref="BrittleStarTimeLine"/>), laid on every peer from the broadcast course; it lights
    /// in the local pilot's colour on a clean shift.</item>
    /// <item><c>.Coach</c> - the server's race coach for every AI Brittle Star (<see cref="BrittleStarTimeCoach"/>): its
    /// Speed wish through the hull's own autopilot control, its line, its nose follow by difficulty.</item>
    /// <item><c>.Heat</c> - the local pilot's threads graded and called (<see cref="BrittleStarTimeHeat"/>): shock rings
    /// off the gates, streaks, lap splits, a best lap, a clean lap, and a start hint per rung.</item>
    /// </list></para>
    ///
    /// <para><b>What the mode does NOT add:</b> no ability and no vessel change - every Brittle Star file, prefab, action
    /// SO and config is untouched. The course source (<see cref="BrittleStarTimeCourseSource"/>) supplies the shapes and
    /// the laps per rung, and the card's starting elements the Time 5 start.</para>
    /// </summary>
    public partial class BrittleStarTimeController : GateRaceController
    {
        [Header("Brittle Star Time circuit")]
        [Tooltip("Laps of the ring set that make one I1 race. The authored gate target is the I1 RACE " +
                 "length, so the circuit is laid with target/laps rings - one number authored " +
                 "once, in the end-condition overrides, and the two can never disagree. Higher rungs fly more laps " +
                 "of their shorter shapes (BrittleStarTimeCourse.LapsByIntensity).")]
        [SerializeField, Min(1)] int laps = BrittleStarTimeCourseSource.DefaultLaps;

        protected override string ModeName => "Brittle Star Time";

        /// <summary>Regatta's gate steering, which made the AI race Switchback and Warpline: commit from
        /// the approach point and aim through the ring's centre.</summary>
        protected override bool AICommitFromApproachPoint => true;

        protected override RaceCourseSource CreateCourseSource() =>
            new BrittleStarTimeCourseSource { Laps = laps };

        /// <summary>The Brittle Star's full Speed on this rung: x6 cruise, x7 at I4's Time 5 start.</summary>
        float TopSpeed => Intensity >= 4 ? BrittleStarTimeCourse.SpeedModeSpeedAtTime5 : BrittleStarTimeCourse.SpeedModeSpeed;

        protected override void OnCourseRaised() => RaiseLetGoGates();

        protected override void OnServerTick() => TickCoach();

        protected override void OnRacerArmed(IPlayer pilot) => ArmCoach(pilot);

        // GateRaceController.Update runs the race and a subclass must not declare Update; LateUpdate is free. Declared
        // once, here, for every partial.
        void LateUpdate()
        {
            TickLetGoGates();
            TickHeat();
        }

        void OnDisable() => ReleaseEveryCoachedWish();

        public override void OnNetworkDespawn()
        {
            ReleaseEveryCoachedWish();
            ClearLetGoGates();
            ClearHeat();
            base.OnNetworkDespawn();
        }
    }
}
