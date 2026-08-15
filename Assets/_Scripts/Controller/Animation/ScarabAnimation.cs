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
    /// The motion is a beetle's, not an aircraft's:
    /// - <b>Elytra</b> crack open outward under yaw and lift, the way a beetle's wing cases part
    ///   before flight. The OUTSIDE case of a turn opens further than the inside one, so the ship
    ///   banks visually even though the hull's own roll is small.
    /// - <b>Legs</b> tuck in with speed and splay out when you slow — the read for "landing" vs
    ///   "running", and the fastest way to tell throttle state from behind.
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

        [Header("Tuning")]
        [Tooltip("Degrees the wing cases crack open at full stick.")]
        [SerializeField] float elytraFlare = 26f;
        [Tooltip("Degrees the wing cases ride open just from carrying speed.")]
        [SerializeField] float elytraCruiseFlare = 8f;
        [Tooltip("Degrees the horn swings against the nose.")]
        [SerializeField] float hornScaler = 14f;
        [Tooltip("Degrees the legs splay OUT when slow. They tuck toward rest at speed.")]
        [SerializeField] float legSplay = 22f;
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
            // the turn opens further. Roll is folded in at half weight for a rolled entry.
            float baseFlare = elytraCruiseFlare * speed01;
            float turn = Mathf.Clamp(yaw + roll * 0.5f, -1f, 1f);
            RotatePartFromRest(RightElytron, 0f, 0f, -(baseFlare + elytraFlare * Mathf.Max(0f, turn)));
            RotatePartFromRest(LeftElytron, 0f, 0f, baseFlare + elytraFlare * Mathf.Max(0f, -turn));

            // Horn leads the turn and lifts against pitch — small, but it is what stops the nose
            // reading as a welded spike.
            RotatePartFromRest(Horn, -pitch * hornScaler, yaw * hornScaler * 0.5f, 0f);

            // Legs: splayed at rest, tucked at speed. The middle pair lags the outer pairs so the
            // set does not move as one rigid rack.
            float splay = legSplay * (1f - speed01);
            for (int i = 0; i < LeftLegs.Length; i++)
            {
                float stagger = i == 1 ? 0.7f : 1f;
                RotatePartFromRest(LeftLegs[i], 0f, 0f, splay * stagger);
                RotatePartFromRest(RightLegs[i], 0f, 0f, -splay * stagger);
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
