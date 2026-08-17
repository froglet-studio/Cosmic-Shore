using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Puppeteers the Scarab's hull (parts built by <see cref="ScarabHullBuilder"/>).
    ///
    /// A vessel that does not move its own parts does not read as flying, however good the flight
    /// model underneath it is — the ship looks like a prop being slid around. The Scarab shipped
    /// carrying <c>MantaAnimationContoller</c> from its Sparrow clone, which resolves Manta/Sparrow
    /// bone names; on the beetle hull those resolve to the inherited FBX's bones, which are now
    /// renderer-disabled. So it was animating an invisible ship while the visible one sat rigid.
    ///
    /// AMPLITUDES ARE FLEET-SCALE ON PURPOSE. The first cut swung its parts 14-26°, which is
    /// invisible at chase-camera distance and read as "no puppeteering at all". <c>RhinoAnimation</c>
    /// is the calibration: it swings wings and engines through <c>yawAnimationScaler = 80</c>
    /// degrees and the fuselage through 25. Vessel puppetry in this game is a big, legible gesture,
    /// not a subtle one — if you can't see it from the chase camera it isn't doing its job.
    ///
    /// The motion is a beetle's, not an aircraft's:
    /// - <b>Elytra</b> crack open outward under yaw and lift, the way a beetle's wing cases part
    ///   before flight. The OUTSIDE case of a turn opens further than the inside one, so the ship
    ///   banks visually even though the hull's own roll is small.
    /// - <b>Legs</b> tuck in with speed and splay out when you slow — the read for "landing" vs
    ///   "running", and the fastest way to tell throttle state from behind. They also row fore/aft
    ///   with pitch, so the set never moves as one rigid rack.
    /// - <b>Horn</b> pitches against the nose, leading the turn slightly, which is what sells the
    ///   hull as having mass rather than being a rigid arrow.
    ///
    /// Parts resolve BY NAME (<see cref="VesselAnimation.ResolvePart"/>) so real Scarab art can
    /// replace the procedural hull without touching this class, as long as it names its pieces the
    /// same way. Unresolved parts cost that limb's motion and nothing else.
    /// </summary>
    class ScarabAnimation : VesselAnimation
    {
        [SerializeField] Transform LeftElytron;
        [SerializeField] Transform RightElytron;
        [SerializeField] Transform Horn;
        [SerializeField] Transform[] LeftLegs = new Transform[3];
        [SerializeField] Transform[] RightLegs = new Transform[3];

        [Header("Tuning (degrees — see the class note on fleet scale)")]
        [Tooltip("Degrees the wing cases crack open at full stick.")]
        [SerializeField] float elytraFlare = 40f;
        [Tooltip("Degrees the wing cases ride open just from carrying speed.")]
        [SerializeField] float elytraCruiseFlare = 14f;
        [Tooltip("Degrees the wing cases sweep back along the hull under throttle.")]
        [SerializeField] float elytraSweep = 16f;
        [Tooltip("Degrees the horn swings against the nose.")]
        [SerializeField] float hornScaler = 34f;
        [Tooltip("Degrees the legs hang DOWN when slow — landing gear out.")]
        [SerializeField] float legHang = 42f;
        [Tooltip("Degrees the legs fold UP against the shell at speed. The two together are the " +
                 "read for throttle state from behind.")]
        [SerializeField] float legTuck = 30f;
        [Tooltip("Degrees the legs row fore/aft with pitch.")]
        [SerializeField] float legRow = 26f;
        [Tooltip("Speed at which the legs are fully tucked. Read off the vessel's live speed, so " +
                 "it follows boosts and slows without extra wiring.")]
        [SerializeField, Min(1f)] float legTuckSpeed = 120f;

        protected override void ResolveParts()
        {
            LeftElytron = ResolvePart(LeftElytron, "elytron.l");
            RightElytron = ResolvePart(RightElytron, "elytron.r");
            Horn = ResolvePart(Horn, "horn");

            for (int i = 0; i < LeftLegs.Length; i++)
                LeftLegs[i] = ResolvePart(LeftLegs[i], $"leg.l{i + 1}");
            for (int i = 0; i < RightLegs.Length; i++)
                RightLegs[i] = ResolvePart(RightLegs[i], $"leg.r{i + 1}");

            // The procedural parts rest at identity, so this is currently a no-op — but it is what
            // lets authored art with angled rest poses drop in later without tearing flat.
            CaptureRestRotations(LeftElytron, RightElytron, Horn);
            CaptureRestRotations(LeftLegs);
            CaptureRestRotations(RightLegs);

            ReportUnresolvedParts();
        }

        protected override void PerformShipPuppetry(float pitch, float yaw, float roll, float throttle)
        {
            // Speed drives the parts that should respond to FLIGHT rather than to input, so they
            // keep moving under a boost or a danger-prism slow that the stick knows nothing about.
            float speed01 = VesselStatus != null
                ? Mathf.Clamp01(VesselStatus.Speed / legTuckSpeed)
                : 0f;

            // Wing cases: a base flare from speed, plus a differential from yaw so the outside of
            // the turn opens further. Roll is folded in at half weight for a rolled entry, and the
            // pair sweeps back together under throttle so hard acceleration visibly closes them up.
            float baseFlare = elytraCruiseFlare * speed01;
            float turn = Mathf.Clamp(yaw + roll * 0.5f, -1f, 1f);
            float sweep = -elytraSweep * Mathf.Clamp01(throttle);
            RotatePartFromRest(RightElytron, 0f, sweep,
                               -(baseFlare + elytraFlare * Mathf.Max(0f, turn)));
            RotatePartFromRest(LeftElytron, 0f, -sweep,
                               baseFlare + elytraFlare * Mathf.Max(0f, -turn));

            // Horn leads the turn and lifts against pitch — small in travel next to the elytra, but
            // it is what stops the nose reading as a welded spike.
            RotatePartFromRest(Horn, -pitch * hornScaler, yaw * hornScaler * 0.5f, 0f);

            // Legs sweep through a signed arc: hanging DOWN when slow, folded UP against the shell
            // at speed. A one-sided "splay toward rest" only ever reaches the pose the mesh was
            // built in, which is out-and-down — legible as neither gear-down nor tucked.
            float leg = Mathf.Lerp(legHang, -legTuck, speed01);
            float row = legRow * pitch;
            for (int i = 0; i < LeftLegs.Length; i++)
            {
                // The middle pair lags the outer pairs and rows against them, so the set never
                // moves as one rigid rack.
                float stagger = i == 1 ? 0.65f : 1f;
                float phase = i == 1 ? -1f : 1f;
                RotatePartFromRest(LeftLegs[i], row * phase, 0f, leg * stagger);
                RotatePartFromRest(RightLegs[i], row * phase, 0f, -leg * stagger);
            }
        }

        protected override void AssignTransforms()
        {
            Transforms.Add(LeftElytron);
            Transforms.Add(RightElytron);
            Transforms.Add(Horn);
            for (int i = 0; i < LeftLegs.Length; i++) Transforms.Add(LeftLegs[i]);
            for (int i = 0; i < RightLegs.Length; i++) Transforms.Add(RightLegs[i]);
        }
    }
}
