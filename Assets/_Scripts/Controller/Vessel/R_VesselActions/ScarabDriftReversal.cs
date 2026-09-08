using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Scarab's REVERSE modifier, as pure functions (design: SCARAB.md §3.8).
    ///
    /// A fully-held drift does not give the Scarab a second ability — it gives every act it
    /// already has the opposite SIGN. A hull strike on a ball sends the ball back exactly the way
    /// it came instead of wherever the bounce would have put it; the cavitation plate destroys the
    /// same cylinder but throws its mass the other way. One hold, one meaning, two consumers,
    /// which is why both rules live here rather than one in the ball and one in the blast.
    ///
    /// The reversal is deliberately the ONLY thing the hold changes. It cannot aim: a reversal
    /// sends a ball along <c>−v</c> and nowhere else, so the pilot aims by choosing WHICH
    /// trajectory to intercept and WHERE to be when they do. That is the whole skill, and it is
    /// what makes the move readable to everyone else on the court: a ball that reverses is a ball
    /// retracing its own path.
    /// </summary>
    public static class ScarabDriftReversal
    {
        /// <summary>Where a reversed swept plate is spawned, and which way it faces.</summary>
        public readonly struct SweepPose
        {
            public readonly Vector3 Position;
            public readonly Vector3 Forward;
            public SweepPose(Vector3 position, Vector3 forward) { Position = position; Forward = forward; }
        }

        /// <summary>
        /// The spawn pose for a REVERSED cavitation plate: start at the far end of the cylinder the
        /// ordinary blast would sweep, and travel back along it toward the hull.
        ///
        /// IT IS A SPAWN TRANSFORM, NOT A SECOND CODE PATH, and that is the point. The plate's
        /// governing law is "everything it claims leaves along the sweep"
        /// (<see cref="AOECylindricalExplosion"/>), so reversing the sweep reverses the debris for
        /// free — with the law preserved rather than special-cased, and with the swept VOLUME
        /// provably identical (see <c>ScarabDriftReversalTests</c>): the same cylinder, traversed
        /// the other way. It also hands the player the read for nothing: instead of a wall leaving
        /// the hull, a wall arrives at it, and the mass it takes flies back past the pilot.
        /// </summary>
        /// <param name="origin">Where the ordinary plate would start — the hull.</param>
        /// <param name="dir">The dash direction (unit); the ordinary sweep direction.</param>
        /// <param name="length">The plate's own sweep length in world units.</param>
        public static SweepPose ReversedSweep(Vector3 origin, Vector3 dir, float length)
            => new(origin + dir * length, -dir);

        /// <summary>
        /// Is there anything to reverse? A ball that is barely moving has no trajectory to send
        /// back, so below <paramref name="minSpeed"/> a held-drift contact falls through to the
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
        public static bool PassThroughLapsed(float lastContactTime, float expiry, float now, float gap)
            => now >= expiry || now - lastContactTime > gap;

        /// <summary>
        /// Is the drift trigger BURIED right now — the Scarab's REVERSE modifier — given whether
        /// it was already buried a frame ago?
        ///
        /// A BARE COMPARISON IS THE WRONG SHAPE FOR A HELD CONTROL. A physical analog trigger
        /// pressed to its stop does not sit still: it wobbles a few percent under a thumb that is
        /// also working a stick, and every dip below the line drops the modifier for as long as
        /// the dip lasts. The pilot's intent ("I am burying this") does not flicker, so the
        /// predicate must not either — the reversal reads as cutting out at random, which is
        /// exactly the reported symptom.
        ///
        /// So the hold LATCHES: it engages high (the pilot has to genuinely bury the trigger) and
        /// releases much lower (they have to genuinely let it up). The release point is deep
        /// inside the SHARP drift band — the trigger's top half, above where the drift blend has
        /// already saturated — so letting go of the reversal is never confusable with easing off
        /// the drift, and the deepest part of the same travel that gives the sharpest drift is
        /// also what arms the reverse. One control, one continuous meaning.
        ///
        /// It also has to be latched because the answer CROSSES THE WIRE: a remote pilot's hold
        /// reaches the server as a replicated level sampled at the network tick, so a dip that a
        /// bare comparison would show for two frames can be the value a whole tick carries.
        /// </summary>
        /// <param name="wasHeld">The latch's state on the previous frame.</param>
        /// <param name="hold01">This frame's trigger depth, 0..1.</param>
        /// <param name="engage">Depth at or above which an unheld trigger becomes held.</param>
        /// <param name="release">Depth BELOW which a held trigger stops being held. It needs no
        /// guard against being authored at or above <paramref name="engage"/>: at equal the two
        /// branches ARE the same comparison, and above it the held branch is the STRICTER of the
        /// two — so a miswired asset degrades to chatter or to the bare comparison, never to a
        /// modifier stuck on. (A guard for that case was written and removed: it could not change
        /// an answer, and a branch that cannot fire tells a reader a failure mode exists.)</param>
        public static bool LatchDriftHold(bool wasHeld, float hold01, float engage, float release)
            => hold01 >= (wasHeld ? release : engage);
    }
}
