using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Termite queen's TIME ability: <b>the pheromone clock</b> — the deck's elixir. A PASSIVE
    /// ability with no input: pheromone refills at a steady rate into a small tank and every card
    /// the queen plays is paid for out of it. Design record: <c>R_VesselActions/TERMITE.md</c> §5.2.
    ///
    /// <para>It is Time's because a clock is the one thing Time owns everywhere in the fleet
    /// (the Butterfly's Fold, the Dolphin's boost ring, the Serpent's pellet regen): at rest the
    /// tank refills at Clash Royale's own elixir rate, one point every 2.8 seconds, and at Time 10
    /// it refills twice as fast. The tank's SIZE and the cards' COSTS do not move — Time buys
    /// tempo, never a bigger hand.</para>
    ///
    /// <para><b>The regen is evaluated at the REPLICATED level</b>, because every peer runs its
    /// own copy of the clock to mirror the owner's plays on the HUD-less replicas, and a clock
    /// that ran at each machine's own local level would drift from the owner's by exactly the
    /// element levels that never replicate. Admission itself is the owner's alone
    /// (<see cref="R_VesselActionHandler.OwnerPressGate"/>), so a drifted replica can only
    /// DISPLAY a slightly different number, never refuse or allow a different play.</para>
    ///
    /// <para>Wired directly on <see cref="TermiteDeckExecutor"/>: a passive ability is bound to no
    /// input event, so the action handler's binding sweep can never resolve it (the lesson the
    /// Dolphin's crystal seeding recorded).</para>
    /// </summary>
    [CreateAssetMenu(fileName = "TermitePheromoneAction",
        menuName = "ScriptableObjects/Vessel Actions/Termite Pheromone")]
    public class TermitePheromoneActionSO : ShipActionSO
    {
        [Tooltip("The most pheromone the queen can hold.")]
        [SerializeField, Min(1f)] float maxPheromone = 10f;

        [Tooltip("Pheromone in the tank when the queen spawns.")]
        [SerializeField, Min(0f)] float startPheromone = 5f;

        [Tooltip("TIME -> tempo: pheromone per second. 0.357 at rest is Clash Royale's one elixir " +
                 "per 2.8 s; Time 10 doubles it. Floored so a deep Time deficit slows the deck " +
                 "without ever stopping it.")]
        [SerializeField] ElementalFloat regenPerSecond = ElementalFloat.Multiplier(0.357f, 0.714f, Element.Time, 0.18f);

        public float MaxPheromone => Mathf.Max(1f, maxPheromone);
        public float StartPheromone => Mathf.Clamp(startPheromone, 0f, MaxPheromone);

        /// <summary>Pheromone per second right now, identically on every peer.</summary>
        public float RegenPerSecond(IVesselStatus status) => Mathf.Max(0f, regenPerSecond.EvaluateReplicated(status));

        /// <summary>Passive: nothing happens on a press (none is ever bound).</summary>
        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus) { }
        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus) { }
    }
}
