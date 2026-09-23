using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A thing that drags a WAKE through the mass it passes (Docs/PRISM_ANIMATION.md §4.7.3).
    ///
    /// <para><b>It asks for a CAPABILITY, never for a type.</b> <see cref="PrismWakeSource"/> knows
    /// nothing about missiles or balls: it asks whichever component on its own GameObject answers
    /// this interface how fast it is going and how big it is, and publishes a wake from that. The
    /// two shipped carriers are the Sparrow's skyburst missile (<c>Projectile</c>) and the Scarab's
    /// ball (<c>AstroLeagueBall</c>), and they have nothing else in common — one is a pooled local
    /// object whose mover teleports it, the other a replicated rigidbody. The interface is what
    /// lets both be right about their own motion.</para>
    ///
    /// <para><b>Why not read the transform?</b> The source DOES fall back to a frame-to-frame
    /// position delta when nothing answers this, and that fallback is correct but blind in two
    /// ways a carrier is not: it cannot tell a real move from a POOL REPOSITION, and it reads the
    /// local transform on a peer that is only receiving replicated positions. A carrier answers
    /// with the velocity its own simulation believes in — which for the ball is the replicated
    /// <c>n_Velocity</c> on a client — so a peer's wake runs on the owner's numbers.</para>
    ///
    /// <para><b>The RADIUS is live, not measured once.</b> Both carriers change size while they
    /// travel — a skyburst swells up to 20x in the first fifth of its flight, a forged ball is
    /// sized after it spawns — and the wake's reach and train length are multiples of this radius,
    /// so it is asked for every frame rather than cached. A bigger thing leaves a bigger wake,
    /// which is the whole point of scaling it this way.</para>
    /// </summary>
    public interface IPrismWakeCarrier
    {
        /// <summary>
        /// This frame's world velocity and wake radius, or false for "no wake right now" — a round
        /// that has not launched, a ball sitting at rest, anything mid-teardown. Returning false is
        /// the ordinary way to switch a wake off; the source eases it out rather than cutting it.
        /// </summary>
        bool TryGetWakeMotion(out Vector3 velocity, out float radius);
    }
}
