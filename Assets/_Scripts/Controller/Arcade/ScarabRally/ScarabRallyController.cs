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

        protected override RaceCourseSource CreateCourseSource() => new ScarabRallyCourseSource();
    }
}
