using System;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>The six cards of the Termite queen's deck — the 2024 prototype's card art
    /// (<c>_Graphics/VesselButtons/TermiteCard_*</c>), one mechanic each. Explicit values: never
    /// reorder, never reuse (Unity serializes the number).</summary>
    public enum TermiteCard
    {
        Autothysis  = 1,
        TeamCrystal = 2,
        QueenDrones = 3,
        MoundDrones = 4,
        Teleport    = 5,
        NewMound    = 6,
    }

    /// <summary>One card: what it is, what it costs in pheromone, and the base amount of whatever
    /// it makes (a blast diameter, a drone count, a jump length, a mound's seed prisms, how far
    /// ahead a crystal is planted). The slot's element multiplies the amount.</summary>
    [Serializable]
    public struct TermiteCardSpec
    {
        public TermiteCard Card;
        [Tooltip("Pheromone the card costs. The tank holds 10 (TermitePheromoneActionSO).")]
        [Min(0f)] public float Cost;
        [Tooltip("The card's base amount, BEFORE the slot element's multiplier: Autothysis = one " +
                 "blast's diameter, Team Crystal = how far ahead of the queen it is planted, Queen " +
                 "/ Mound Drones = drones released, Teleport = the jump length when no point is " +
                 "commanded, New Mound = the mound's seed prisms.")]
        [Min(0f)] public float Amount;
    }

    /// <summary>
    /// One ELEMENT SLOT of the Termite queen's deck: two cards that alternate, Clash Royale's
    /// cycle cut down to a pair. Pressing the slot's input plays whichever card is FACE UP and
    /// turns the other one over, so a slot is always "this card now, that card next" and the HUD
    /// can show both. Design record: <c>R_VesselActions/TERMITE.md</c> §5.
    ///
    /// <para>The pairs follow the elements' fleet-wide meanings: CHARGE is violence (Autothysis ↔
    /// Team Crystal), MASS is bodies (Queen Drones ↔ Mound Drones), SPACE is place (Teleport ↔ New
    /// Mound). TIME is not a card — it is the pheromone clock that pays for them
    /// (<see cref="TermitePheromoneActionSO"/>).</para>
    ///
    /// <para><b>Admission is decided ONCE, on the owning machine, before the press is sent</b>
    /// (<see cref="R_VesselActionHandler.OwnerPressGate"/>, registered by
    /// <see cref="TermiteDeckExecutor"/>), so a card is played on every peer or on none. By the
    /// time <see cref="StartAction"/> runs the play has already been paid for.</para>
    ///
    /// <para>The asset is SHARED by every Termite in a match and holds no per-vessel state; which
    /// card is face up lives on the executor.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "TermiteCardAction",
        menuName = "ScriptableObjects/Vessel Actions/Termite Card Slot")]
    public class TermiteCardActionSO : ShipActionSO
    {
        [Tooltip("The element this slot belongs to — its flower sits above the slot's card on the " +
                 "HUD and its level scales both cards' amounts.")]
        [SerializeField] Element slotElement = Element.Charge;

        [Tooltip("The card face up when the queen spawns.")]
        [SerializeField] TermiteCardSpec cardA;

        [Tooltip("The card on the back — face up after cardA is played, and so on, alternating.")]
        [SerializeField] TermiteCardSpec cardB;

        [Tooltip("The slot element's multiplier on both cards' Amount. Evaluated at the REPLICATED " +
                 "level, because every card changes the world every peer simulates (mass laid, " +
                 "drones released, blasts detonated) and element levels never replicate.")]
        [SerializeField] ElementalFloat power = ElementalFloat.Multiplier(1f, 2f, Element.Charge, 0.5f);

        public Element SlotElement => slotElement;
        public TermiteCardSpec CardA => cardA;
        public TermiteCardSpec CardB => cardB;

        /// <summary>Card <paramref name="faceB"/> ? B : A.</summary>
        public TermiteCardSpec Face(bool faceB) => faceB ? cardB : cardA;

        /// <summary>The slot element's multiplier right now, as every peer computes it.</summary>
        public float Power(IVesselStatus status) => Mathf.Max(0.05f, power.EvaluateReplicated(status));

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<TermiteDeckExecutor>()?.Play(this, vesselStatus);

        /// <summary>Release: nothing. A card is played on the press.</summary>
        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus) { }
    }
}
