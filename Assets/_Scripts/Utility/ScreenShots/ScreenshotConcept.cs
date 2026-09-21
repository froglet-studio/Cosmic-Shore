using System;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// One named way to point a camera at the local vessel — "sidecar", "oncoming", "static
    /// tracking cam". A concept is nothing but RANGES; the director rolls a value inside each one
    /// per capture, so the same concept never gives the same photograph twice.
    ///
    /// <para>There is deliberately <b>no per-concept camera code and no enum of shot types</b>.
    /// Every shot in the library is the same solve — a spherical offset around the subject, an aim
    /// point, a lens — and the only structural difference between "over the shoulder" and "static
    /// tracking cam" is <see cref="worldAligned"/>: whether azimuth is measured from the vessel's
    /// own course or from world north. A shot type expressed as a subclass or a switch arm is a
    /// shot type nobody can author without a programmer; expressed as ranges, a new one is a row in
    /// the list.</para>
    /// </summary>
    [Serializable]
    public class ScreenshotConcept
    {
        [Tooltip("Shown in the log line and baked into the filename, so a folder of captures tells " +
                 "you which concepts are earning their place.")]
        public string name = "Concept";

        [Tooltip("Relative odds of being drawn. 0 retires the concept without deleting it.")]
        [Min(0f)] public float weight = 1f;

        [Tooltip("SOLO frames the local vessel. PAIR frames two vessels flying near each other, " +
                 "both at the same distance from the lens and symmetric about frame centre - see " +
                 "ScreenshotFraming.SolvePair. A Pair concept is only ever drawn when the arena " +
                 "actually holds a pair inside the config's separation band.")]
        public ScreenshotFramingKind framing = ScreenshotFramingKind.Solo;

        [Tooltip("OFF: azimuth is measured from the vessel's own course, so the shot follows it " +
                 "through a turn (chase, sidecar, oncoming). ON: azimuth is measured from world " +
                 "north, so the camera is a fixed vantage the vessel happens to fly past - which " +
                 "is what makes a 'static tracking cam' read as a camera planted in the world.")]
        public bool worldAligned;

        [Tooltip("SOLO: degrees around the subject. 0 = directly AHEAD of it, 180 = directly " +
                 "behind. A range spanning 360 gives a free orbit.\n" +
                 "PAIR: degrees around the LINE JOINING the two vessels, 0 = on the side their " +
                 "shared travel points. That circle is the whole vantage set a two-shot allows, " +
                 "which is why elevation is unused there.")]
        public Vector2 azimuthDegrees = new Vector2(150f, 210f);

        [Tooltip("Degrees above (+) or below (-) the subject's horizon. SOLO only - a PAIR shot " +
                 "ignores it, because lifting the camera off the pair's perpendicular bisector " +
                 "plane is exactly the move that stops the two being equidistant.")]
        public Vector2 elevationDegrees = new Vector2(5f, 20f);

        [Tooltip("World units from the subject (from the pair's MIDPOINT on a Pair concept, where " +
                 "it is a floor - the solve pushes further back if that is what it takes to fit " +
                 "both ships). NOTE: past ~150u the vessel vision band starts re-shading hulls " +
                 "into flat domain-coloured silhouettes (Docs/VESSEL_VISION.md) - deliberate and " +
                 "striking for an establishing shot, wrong for a hero shot.")]
        public Vector2 distance = new Vector2(15f, 30f);

        [Tooltip("Vertical field of view. Low is a long lens (compressed, picks the subject out of " +
                 "the arena); high is wide (the subject small inside the world it is flying through).")]
        public Vector2 fieldOfView = new Vector2(55f, 70f);

        [Tooltip("Dutch tilt, in degrees. A couple of degrees reads as energy; a lot reads as a " +
                 "mistake. On a PAIR shot this is how far off horizontal the two ships sit, since " +
                 "the solve lays them level across the frame before it is applied.")]
        public Vector2 rollDegrees = new Vector2(-4f, 4f);

        [Tooltip("Seconds of the subject's own velocity to aim AHEAD of it. Pushes the vessel " +
                 "toward the back of frame and leaves the space it is flying into - the lead room " +
                 "that makes a moving subject read as moving. SOLO only - a PAIR shot ignores it, " +
                 "because swinging the aim off the midpoint slides both ships toward one edge and " +
                 "loses the symmetry that is the whole point of a two-shot.")]
        public Vector2 aimLeadSeconds = new Vector2(0f, 0.25f);

        [Tooltip("Extra pitch applied after aiming, in degrees. Positive drops the subject toward " +
                 "the lower third of frame and gives the shot some sky.")]
        public Vector2 framingPitchDegrees = new Vector2(-3f, 3f);

        /// <summary>
        /// A SOLO concept with no reachable distance can never produce a shot. A PAIR concept is
        /// exempt from the distance test on purpose: there <see cref="distance"/> is a floor the
        /// fit calculation overrides whenever two ships would not both be in frame, so an authored
        /// zero is the legitimate way to say "as close as they will both fit" rather than a
        /// concept that can never fire.
        /// </summary>
        public bool IsUsable =>
            weight > 0f &&
            (framing == ScreenshotFramingKind.Pair ||
             Mathf.Max(distance.x, distance.y) >= ScreenshotFraming.MinimumDistance);
    }
}
