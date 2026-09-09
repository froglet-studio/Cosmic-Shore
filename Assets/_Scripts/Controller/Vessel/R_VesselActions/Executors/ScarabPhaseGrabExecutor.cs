using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Scarab's <b>Phase Grab</b> — the hold that turns the hull from a wall into a hand.
    ///
    /// While it is held, two things are true of every Astro League ball this vessel touches, and
    /// they are one act rather than two: a hull strike <b>REVERSES</b> the ball (same speed,
    /// exactly 180 degrees, so it retraces its own flight) and the hull then <b>stops impeding</b>
    /// it, so the ball leaves along its new heading straight through the ship that grabbed it.
    /// Neither half works without the other — the fling's whole direction runs through the hull,
    /// and a reversal you then bounce off is just a bounce.
    ///
    /// <para><b>It used to ride a fully-held DRIFT and no longer does</b> (SCARAB.md §3.8). The
    /// drift is an analog control the pilot steers with, so the modifier inherited every property
    /// of a steering input: it wobbled with the thumb, it competed with the drift's own depth for
    /// meaning, and it made "nothing may happen at full drift" impossible to state. Two playtests
    /// went into making a threshold on that channel behave like a button. It is a button now, and
    /// the drift is just the drift.</para>
    ///
    /// <para><b>It needs no networking of its own, and that is the whole reason it is an ACTION.</b>
    /// The ball is server-simulated, so the machine that resolves a strike is the SERVER — and a
    /// held analog trigger is local state that never crosses the wire, which is exactly why the
    /// drift-held version had to carry a <c>NetworkVariable</c> to work for anyone but the host.
    /// <see cref="R_VesselActionHandler"/> already round-trips every press and release through
    /// <c>SendButtonPressed_ServerRpc</c> → <c>SendButtonPressed_ClientRpc</c> →
    /// <c>PerformShipControllerActions</c>, so <see cref="Engage"/> and <see cref="Release"/> run
    /// on EVERY peer including the server, for every pilot. The server reads this flag off its own
    /// replica and is correct by construction. <see cref="EchoSightActionExecutor"/> records the
    /// same observation for the Dolphin's telegraph.</para>
    ///
    /// <para><b>Permissions and photons only.</b> Phasing destroys nothing, moves nothing and
    /// spends nothing — it withdraws the hull's claim on a ball for as long as it is held. No
    /// cooldown and no resource cost, because a modifier the pilot has to ration is one they stop
    /// reaching for.</para>
    ///
    /// <para><b>It lives on the vessel ROOT</b> even though its siblings sit under
    /// <c>VesselActions</c>, and is registered explicitly in
    /// <see cref="ActionExecutorRegistry"/>'s list so <c>Get&lt;T&gt;</c> still resolves it by
    /// type. The reason is the reader: <c>AstroLeagueBall</c> asks this question on a contact
    /// path, and it already asks the sibling question ("is this striker mid-juke?") with a root
    /// <c>TryGetComponent</c>. One walk, not two, and the same shape for both.</para>
    /// </summary>
    public class ScarabPhaseGrabExecutor : ShipActionExecutorBase
    {
        /// <summary>True while the pilot is holding the phase button. Read by
        /// <c>AstroLeagueBall</c> on whichever machine is resolving the contact — the server for a
        /// spawned ball, the local machine on the non-networked path.</summary>
        public bool IsPhasing { get; private set; }

        public void Engage() => IsPhasing = true;

        public void Release() => IsPhasing = false;

        /// <summary>
        /// A vessel that is despawned, pooled or swapped mid-hold must not come back phasing. The
        /// release edge is an INPUT event, so it simply never arrives for a ship that stopped being
        /// driven — the same class of bug as a stranded event subscription, and the fix is the
        /// same: tear the state down where the object goes quiet rather than trusting the edge.
        /// </summary>
        void OnDisable() => IsPhasing = false;
    }
}
