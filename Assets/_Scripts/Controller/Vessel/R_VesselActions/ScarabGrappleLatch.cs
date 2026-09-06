namespace CosmicShore.Gameplay
{
    /// <summary>
    /// When the Scarab's ball grapple is attached, and when it lets go — as pure predicates, so
    /// the one asymmetry that can strand a ball is testable offline (design: SCARAB.md §4.7).
    ///
    /// IT EXISTS BECAUSE AN EDGE AND A LEVEL ARE NOT INTERCHANGEABLE ACROSS A TICK. The grapple's
    /// first cut attached on an EDGE (the <c>n_State</c> replication callback, which fires once)
    /// and detached on a LEVEL re-read every frame — so any frame that tripped a detach without
    /// <c>n_State</c> also changing left the hull off the orbit for the rest of that grapple, with
    /// the server still spinning the ball and still refusing every other Scarab. A NetworkVariable
    /// only ever carries the value it holds at serialisation time, so a hold that flickers off and
    /// back inside one tick is coalesced away and the server never sees it at all: the owner's own
    /// frame is the only place that transition exists.
    ///
    /// The answer is symmetry in both directions. The OWNER's attachment is a LEVEL
    /// (<see cref="ShouldFollow"/>) evaluated every frame, so a momentarily missing ball or
    /// transformer costs one frame and heals itself. The RELEASE the server reads is a MONOTONIC
    /// COUNTER (<see cref="AdvanceReleaseSeq"/> / <see cref="ServerShouldRelease"/>) that the owner
    /// bumps on the falling edge it can see, so a sub-tick flutter still arrives as "the pilot let
    /// go" instead of vanishing between samples.
    /// </summary>
    public static class ScarabGrappleLatch
    {
        /// <summary>
        /// Should the OWNER's hull be riding the orbit this frame? A pure level: it is asked every
        /// frame and answers both attach and detach, so the two can never disagree about which
        /// state we are in.
        /// </summary>
        /// <param name="armed">The pilot's drift is fully held right now.</param>
        /// <param name="stateActive">The replicated grapple state names a ball and a valid orbit.</param>
        /// <param name="ballUsable">The ball resolved, and is neither hidden nor frozen.</param>
        /// <param name="hasTransformer">The vessel's transformer exists to be driven.</param>
        /// <param name="alreadyReleased">The pilot has already let THIS grapple go — sticky until
        /// the replicated state changes. It is what stops a hold that flutters back on from
        /// re-attaching to an orbit the server is already in the middle of ending: the hull would
        /// rejoin for the frame or two the release takes to arrive, and the camera would blend off
        /// the ball and straight back onto it. Letting go is a decision, not a level.</param>
        public static bool ShouldFollow(bool armed, bool stateActive, bool ballUsable,
                                        bool hasTransformer, bool alreadyReleased)
            => armed && stateActive && ballUsable && hasTransformer && !alreadyReleased;

        /// <summary>
        /// The owner's release counter after this frame. It advances on the FALLING EDGE of the
        /// hold — the transition the owner is the only peer that can observe — and never otherwise,
        /// so it is monotonic and a peer can compare two samples without having watched the frames
        /// between them. Deliberately unsigned and compared with <c>!=</c> downstream, so a wrap
        /// after 4.29 billion releases still reads as a change.
        /// </summary>
        public static uint AdvanceReleaseSeq(uint seq, bool wasArmed, bool armed)
            => wasArmed && !armed ? unchecked(seq + 1u) : seq;

        /// <summary>
        /// Should the SERVER end the grapple and fling? Either the hold is currently down (the
        /// ordinary case, and the one that survives a disconnect — a departed owner's variable
        /// stops being true), or the owner's release counter has moved since the grab, which is
        /// how a hold that dropped and returned inside one tick still reads as a release.
        /// </summary>
        public static bool ServerShouldRelease(bool armed, uint seqAtGrab, uint seqNow)
            => !armed || seqNow != seqAtGrab;
    }
}
