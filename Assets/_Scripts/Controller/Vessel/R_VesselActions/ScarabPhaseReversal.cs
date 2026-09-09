using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Scarab's PHASE GRAB, as pure functions (design: SCARAB.md §3.8).
    ///
    /// Hold the phase button and the hull stops being a wall: a strike on a ball sends it back
    /// exactly the way it came instead of wherever the bounce would have put it, and the hull
    /// then stops impeding it so it can leave along that new heading — straight through the ship
    /// that grabbed it. Reversal and pass-through are one act, which is why both rules live here.
    ///
    /// The reversal is deliberately the ONLY thing the hold changes to a ball's motion. It cannot
    /// aim: a reversal sends a ball along <c>−v</c> and nowhere else, so the pilot aims by
    /// choosing WHICH trajectory to intercept and WHERE to be when they do. That is the whole
    /// skill, and it is what makes the move readable to everyone else on the court: a ball that
    /// reverses is a ball retracing its own path.
    ///
    /// <para><b>It rode a fully-held DRIFT for two playtests and no longer does.</b> A drift is an
    /// analog control the pilot is steering with, so a threshold on it inherited every property of
    /// a steering input — it wobbled with the thumb, it competed with the drift's own depth for
    /// meaning, and it made "nothing should happen at full drift" impossible to state. The hold is
    /// a button now (<see cref="ScarabPhaseGrabExecutor"/>). The BLAST's half of the old modifier
    /// — a plate that swept backwards — is retired outright rather than re-bound: the blast now
    /// claims its own mirror image instead, which reaches behind the pilot without ever pointing
    /// the punch the other way.</para>
    /// </summary>
    public static class ScarabPhaseReversal
    {
        /// <summary>
        /// Is there anything to reverse? A ball that is barely moving has no trajectory to send
        /// back, so below <paramref name="minSpeed"/> a phased contact falls through to the
        /// ORDINARY strike rather than doing nothing. "Nothing happens" is the one outcome a
        /// committed input must never produce — it reads as a broken ability, not as a rule.
        /// </summary>
        public static bool CanReverseBall(float ballSpeed, float minSpeed) => ballSpeed >= minSpeed;

        /// <summary>
        /// What the ball leaves with: exactly <c>−v</c>. The speed is untouched, so the reversal
        /// adds no energy and takes none — it is a redirection and only a redirection, which is
        /// what keeps it predictable enough to aim a whole match around. Deliberately NOT the
        /// negated bounce result: "back the way it came" is the rule a player can hold in their
        /// head while the ball is still in flight.
        /// </summary>
        public static Vector3 ReversedBallVelocity(Vector3 ballVelocity) => -ballVelocity;

        /// <summary>
        /// Where the reversed ball is RELEASED: just clear of the striker, along the direction it
        /// is now travelling.
        ///
        /// THIS IS WHAT MAKES THE FLING POSSIBLE AT ALL. The signature case — chasing a ball down
        /// and flinging it back past yourself — sends the ball along a heading that runs straight
        /// THROUGH the hull that just grabbed it, and a ball cannot travel through a vessel: the
        /// ordinary depenetration (which pushes the ball radially AWAY from the striker, i.e. the
        /// way it was already going) shoved it back out in front, and the next contact frame found
        /// it still approaching and reversed it a second time, which reads on screen as an
        /// ordinary bounce. Placing it on the far side is the same guarantee the depenetration
        /// already makes — "the ball never overlaps what struck it" — aimed along where the ball
        /// is NOW GOING instead of where it came from.
        ///
        /// It is a move, never an appearance: the ball is continuously visible, it keeps its speed
        /// and its spin, and the grab-and-fling animation covers the transit. In the head-on case
        /// the new heading points away from the hull anyway, so this is very nearly a no-op.
        /// </summary>
        /// <param name="strikerOrigin">The striking vessel's root position.</param>
        /// <param name="reversedVelocity">The ball's post-reversal velocity.</param>
        /// <param name="fallbackDir">Unit direction to use if the reversed velocity is degenerate.</param>
        /// <param name="minClear">Ball radius + the striker's clearance radius.</param>
        public static Vector3 ReversedExitPosition(Vector3 strikerOrigin, Vector3 reversedVelocity,
                                                   Vector3 fallbackDir, float minClear)
        {
            Vector3 dir = reversedVelocity.sqrMagnitude > 1e-8f ? reversedVelocity.normalized : fallbackDir;
            return strikerOrigin + dir * minClear;
        }

        /// <summary>
        /// When the striker stops being able to touch this ball. THE REVERSAL IS AN INVOLUTION —
        /// applying it twice returns the ball to exactly where it started — so it must be
        /// impossible to apply twice on one grab, and the ordinary "is the ball approaching?"
        /// contact gate CANNOT do that job: whenever the vessel closes faster than the ball
        /// travels (which is every deliberate run at a slow ball) the reversed ball is still
        /// approaching, the very next contact frame strikes again, and the two reversals cancel.
        ///
        /// So the grab arms a window during which that vessel and this ball do not interact at
        /// all — no depenetration, no bounce, no second reversal. That is the "phase it through
        /// the vessel" half of the fling, and it is a per-(ball, vessel) latch rather than a
        /// geometric test because the geometry is exactly what is ambiguous during the transit.
        ///
        /// A window is always armed AT A CONTACT — the grab's own, or the first frame a
        /// blast-dragged ball reaches a phasing hull (<see cref="IsBehindStartPlane"/>). That is
        /// deliberate and it is what keeps this rule simple: "no overlap for a while" means "the
        /// ball has left" only if the ball ever arrived, so a window armed ahead of its contact
        /// would need a second rule to survive the wait.
        /// </summary>
        public static float PassThroughExpiry(float now, float seconds) => now + Mathf.Max(0f, seconds);

        /// <summary>Is a previously armed pass-through window still open?</summary>
        public static bool IsPassingThrough(float expiry, float now) => now < expiry;

        /// <summary>
        /// Has the grabbed vessel finished passing through? The window exists to cover the frames
        /// in which the hull is STILL OVERLAPPING the ball, and nothing longer: a pilot who whips
        /// around and comes back is entitled to a fresh grab immediately, and a pass-through that
        /// outlives the contact reads as the ability cutting out. So it ends on whichever comes
        /// first — the contacts stopping (no overlap reported for <paramref name="gap"/>, which
        /// IS the ball having left) or the hard cap.
        ///
        /// The gap is a few frames rather than one, because a hull is a cluster of colliders and
        /// a glancing pass can report no contact for a frame in the middle of one.
        /// </summary>
        public static bool PassThroughLapsed(float lastContactTime, float expiry, float now,
                                             float gap)
            => now >= expiry || now - lastContactTime > gap;

        /// <summary>
        /// Is <paramref name="point"/> on the BEHIND side of the plane a mirrored blast starts on?
        ///
        /// THIS IS THE WHOLE TEST FOR "THE BLAST IS BRINGING THIS BALL TO ME." A mirrored
        /// cavitation plate claims its own reflection through its start plane and throws BOTH
        /// halves the same way (SCARAB.md §3.9), so the forward half sends mass away from the pilot
        /// and the rear half brings mass toward and past them. Only the rear half can ever deliver
        /// a ball to the hull that fired, and only a MIRRORED plate has a rear half at all — an
        /// ordinary cylinder's volume is <c>s ∈ [0, depth]</c>, so a negative <c>s</c> is
        /// impossible there. One dot product therefore answers both questions, and it cannot drift
        /// from the authored flag the way a second read of that flag could.
        ///
        /// Getting this wrong is not a subtle miss: without it a phasing pilot who punches a ball
        /// AWAY marks it too, and the ball they just sent down-range is intangible to them for the
        /// whole cap — so the signature chase-and-grab flies straight through it and does nothing,
        /// which is the one outcome a committed input must never produce.
        /// </summary>
        /// <param name="sweepAxis">Unit sweep direction — for any blast, the direction it throws
        /// what it claims. Callers pass the blast's own impact vector, normalized.</param>
        public static bool IsBehindStartPlane(Vector3 point, Vector3 planeOrigin, Vector3 sweepAxis)
            => Vector3.Dot(point - planeOrigin, sweepAxis) < 0f;

    }
}
