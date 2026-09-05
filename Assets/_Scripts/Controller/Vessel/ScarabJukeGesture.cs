namespace CosmicShore.Gameplay
{
    /// <summary>What one frame's stick deflection means for the juke in flight.</summary>
    public enum ScarabJukeGestureAction
    {
        /// <summary>Nothing to do this frame.</summary>
        None,
        /// <summary>A new push crossed the engage threshold — fire the dash at this strength.</summary>
        Begin,
        /// <summary>The push in flight reached the stick's limit — upgrade it to a committed dash
        /// (steal window, full displacement, and the cavitation plate if the drift is not held).</summary>
        Commit,
        /// <summary>The stick came back inside the release band — the push is over.</summary>
        End,
    }

    /// <summary>
    /// The Scarab juke's gesture state machine, as a pure function of this frame's deflection and
    /// the gesture already in flight (design: R_VesselActions/SCARAB.md §3.7).
    ///
    /// IT EXISTS BECAUSE THE OBVIOUS READING IS WRONG. A stick does not arrive at its destination:
    /// a push sweeps through every intermediate magnitude on its way, so "is the stick at the
    /// perimeter THIS frame" answers a question about the pilot's THUMB SPEED, not about their
    /// intent. Deciding the juke's whole character on the frame it first passes the engage
    /// threshold meant a fast flick was committed (it crossed engage and perimeter in one frame)
    /// while a slower push of exactly the same distance was filed as a nudge — and then the roll it
    /// started locked out re-entry for its own duration, so reaching the limit half a beat later
    /// did nothing at all. The plate came out for quick hands only.
    ///
    /// So a push is ONE GESTURE with two moments: it BEGINS immediately at whatever it has reached
    /// (a dodge must never wait on input smoothing) and it COMMITS whenever it reaches the limit,
    /// however long that takes. The gesture ends only when the stick returns inside the release
    /// band, which also means holding the stick pinned dashes exactly once.
    /// </summary>
    public static class ScarabJukeGesture
    {
        /// <summary>Float-compare guard for the engage test. Deliberately tight.</summary>
        public const float ThresholdEpsilon = 0.005f;

        /// <summary>
        /// How close to the perimeter still counts as "the stick is at its limit". Far looser than
        /// <see cref="ThresholdEpsilon"/> because it is a HARDWARE margin rather than a float
        /// guard: a worn stick that tops out a hair under full must still commit, since committing
        /// is what fires the blast.
        /// </summary>
        public const float PerimeterEpsilon = 0.03f;

        /// <summary>
        /// Where a gesture ENDS — half the threshold that starts one. The hysteresis matters: a
        /// stick resting near the engage threshold would otherwise chatter out a stream of jukes.
        /// </summary>
        public static float ReleaseThreshold(float engageThreshold) => engageThreshold * 0.5f;

        /// <summary>Is this deflection at the stick's limit (within the hardware margin)?</summary>
        public static bool AtLimit(float deflection, float perimeterThreshold)
            => deflection >= perimeterThreshold - PerimeterEpsilon;

        /// <param name="deflection">This frame's radial stick magnitude, already clamped to 0..1.</param>
        /// <param name="gestureActive">Is a push already in flight?</param>
        /// <param name="gestureCommitted">Has the push in flight already committed?</param>
        public static ScarabJukeGestureAction Resolve(
            float deflection, bool gestureActive, bool gestureCommitted,
            float engageThreshold, float perimeterThreshold)
        {
            if (deflection < ReleaseThreshold(engageThreshold))
                return ScarabJukeGestureAction.End;

            if (!gestureActive)
                return deflection >= engageThreshold - ThresholdEpsilon
                    ? ScarabJukeGestureAction.Begin
                    : ScarabJukeGestureAction.None;

            return !gestureCommitted && AtLimit(deflection, perimeterThreshold)
                ? ScarabJukeGestureAction.Commit
                : ScarabJukeGestureAction.None;
        }
    }
}
