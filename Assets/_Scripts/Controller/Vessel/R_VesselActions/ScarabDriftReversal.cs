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
    }
}
