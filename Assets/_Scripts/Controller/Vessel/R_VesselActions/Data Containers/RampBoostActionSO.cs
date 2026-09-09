using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Boost that builds with CONSTANT ACCELERATION while the pilot flies straight (the Rhino's
    /// full-speed-straight run): the cruise speed climbs at a fixed rate toward the boosted
    /// throttle target instead of the default exponential lerp, via
    /// <see cref="VesselTransformer.SetSpeedTrackingRate"/>.
    ///
    /// <para><b>THE RAMP IS A SLOPE, NOT A SWITCH.</b> The gesture engages at
    /// <see cref="StraightLineGesture.EngageThreshold"/> and that is where FULL power stops being
    /// free — past it the boost multiplier is LERPED DOWN toward 1 as the pilot steers harder,
    /// reaching plain cruise at <see cref="StraightnessGraceBand"/>. So a pilot does not choose
    /// between "the line" and "the boost"; they choose HOW MUCH boost this corner is worth, and
    /// the answer is different for every corner and every entry speed.</para>
    ///
    /// <para>That single lerp is what turns two existing formulas into a continuous
    /// speed/radius curve. Turn rate is linear in stick and
    /// <c>VesselTransformer.MaxTurnRateDegreesPerSecond</c> grows with speed, so at stick
    /// fraction <c>s</c> the vessel settles at a speed this action chose and flies a radius
    /// <c>v / (s·ω(v))</c> — a smooth trade from "top speed, wide arc" to "crawl, pivot in place",
    /// with an optimum per corner. Before the grading there were exactly two points on that curve
    /// and therefore one binary decision, which is what a play test reported as nothing to master.
    /// Authoring <c>StraightnessGraceBand</c> at the engage threshold restores the old cliff
    /// exactly.</para>
    ///
    /// <para>Two rates rather than one, because abandoning the gesture and steering inside it are
    /// different acts: <see cref="BleedPerSecond"/> is how fast speed tracks DOWN to the target a
    /// steering pilot just chose (it has to be brisk, or steering costs nothing inside a corner's
    /// half-second), while <see cref="ReturnPerSecond"/> is the long coast after the ramp
    /// disengages entirely — speed as a resource you spend.</para>
    ///
    /// <para>The visual sell (FOV + Panini quasi dolly zoom, proportional to live speed) is owned
    /// by the fleet-wide <c>VesselSpeedTunnel</c> platform law (Docs/SPEED_TUNNEL.md), which reads
    /// speed — so it tracks the ramp up, the graded bleed and the coast automatically, and this
    /// action neither wires nor knows about it.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "RampBoostAction", menuName = "ScriptableObjects/Vessel Actions/Ramp Boost")]
    public class RampBoostActionSO : ShipActionSO
    {
        [Header("Ramp")]
        [Tooltip("BoostMultiplier applied while the ramp is engaged — sets the top-speed target " +
                 "the acceleration climbs toward (the TIME elemental multiplier stacks on top in " +
                 "the transformer as usual).")]
        [SerializeField] float maxBoostMultiplier = 4f;

        [Tooltip("Constant acceleration while engaged, in speed units per second.")]
        [SerializeField] float accelerationPerSecond = 40f;

        [Tooltip("Constant deceleration after the ramp DISENGAGES, in speed units per second — " +
                 "the coast back to the input-driven throttle speed. Deliberately slow: speed is " +
                 "a resource you spent winding up, so losing it should be felt over seconds.")]
        [SerializeField] float returnPerSecond = 400f;

        [Header("Grading")]
        [Tooltip("Deviation from full-speed-straight at which the ramp contributes NOTHING (the " +
                 "vessel's plain cruise). Between the engage threshold (0.3) and this, the boost " +
                 "multiplier lerps down toward 1, so steering costs speed continuously instead of " +
                 "dropping the ramp off a cliff. Set this to 0.3 to restore the old binary latch.\n\n" +
                 "Deviation is a SUM over throttle shortfall and all three rotation axes, so at " +
                 "full throttle a single-axis turn makes it exactly the stick fraction: 1.0 means " +
                 "'stick hard over gives you plain cruise'.")]
        [SerializeField] float straightnessGraceBand = 1f;

        [Tooltip("Constant deceleration WHILE STILL IN THE GESTURE, in speed units per second — " +
                 "how fast speed tracks down to the lower target a steering pilot just chose. " +
                 "Brisk on purpose: a corner lasts a fraction of a second, and at the coast rate " +
                 "a turn would cost nothing inside one.")]
        [SerializeField] float bleedPerSecond = 300f;

        [Header("Feel")]
        [Tooltip("SFX played once when the ramp engages.")]
        [SerializeField] GameplaySFXCategory engageSFX = GameplaySFXCategory.BoostActivate;

        public float MaxBoostMultiplier => maxBoostMultiplier;
        public float AccelerationPerSecond => accelerationPerSecond;
        public float ReturnPerSecond => returnPerSecond;
        public float BleedPerSecond => bleedPerSecond;

        /// <summary>
        /// Deviation at which the ramp is fully off, floored at the engage threshold so a
        /// mis-authored asset degrades to the binary latch rather than to a divide-by-zero.
        /// </summary>
        public float StraightnessGraceBand =>
            Mathf.Max(straightnessGraceBand, StraightLineGesture.EngageThreshold);

        /// <summary>
        /// How much of the ramp a pilot at <paramref name="deviation"/> keeps: 1 across the
        /// full-power plateau, falling linearly to 0 at <see cref="StraightnessGraceBand"/>.
        /// Pure, so <c>RhinoRampGradingTests</c> can assert the curve without a vessel.
        /// </summary>
        public float Straightness01(float deviation)
        {
            float band = StraightnessGraceBand;
            if (band <= StraightLineGesture.EngageThreshold)
                return deviation < StraightLineGesture.EngageThreshold ? 1f : 0f;
            return 1f - Mathf.Clamp01(
                (deviation - StraightLineGesture.EngageThreshold) /
                (band - StraightLineGesture.EngageThreshold));
        }

        /// <summary>
        /// The boost multiplier a pilot at <paramref name="deviation"/> earns — lerped between
        /// <b>1</b> (plain cruise, so the hand-off to disengagement is seamless rather than a
        /// step) and <see cref="MaxBoostMultiplier"/>.
        /// </summary>
        public float MultiplierFor(float deviation) =>
            Mathf.Lerp(1f, maxBoostMultiplier, Straightness01(deviation));
        public GameplaySFXCategory EngageSFX => engageSFX;

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<RampBoostActionExecutor>()?.Begin(this, vesselStatus);

        /// <summary>
        /// The strategy's release at the engage threshold. On a graded ramp this is the TOP of
        /// the slope, so it is handed to the executor as a lapse rather than an end; the ramp
        /// itself disengages when the pilot leaves <see cref="StraightnessGraceBand"/>.
        /// </summary>
        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<RampBoostActionExecutor>()?.ReleaseGesture();
    }
}
