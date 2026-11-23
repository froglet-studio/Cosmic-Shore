using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One of the Starlings' four buttons (design: <c>R_VesselActions/STARLING.md</c>): pick the spread hand's card of one
    /// element, move or take in an element hand, or with no hand up choose the next draw's element. The
    /// player only turns the viewing plane and picks cards; the swarm roams the cell on its own. Stateless and shared:
    /// every press goes straight to the hull's own <see cref="StarlingSwarmExecutor"/>, which owns the hand. The card is
    /// picked on RELEASE (Garrett, 2026-10-21): a press held past <c>StarlingConfigSO.detailsHoldTime</c>, or made with a
    /// trigger pulled, shows the card's details instead and picks nothing (<see cref="StarlingPickPress"/>). The AI picks
    /// through the executor, never through these buttons, so vessel rule 37's same-frame Start/Stop cannot eat an AI pick.
    /// </summary>
    [CreateAssetMenu(fileName = "StarlingPick", menuName = "ScriptableObjects/Vessel Actions/Starling Pick")]
    public sealed class StarlingActionSO : ShipActionSO
    {
        [Tooltip("The element whose card in the hand this button picks.")]
        [SerializeField] Element element = Element.Mass;

        public Element Element => element;

        public override void StartAction(ActionExecutorRegistry executors, IVesselStatus vesselStatus)
        {
            var starlings = executors ? executors.Get<StarlingSwarmExecutor>() : null;
            if (starlings) starlings.PressPick(element);
        }

        public override void StopAction(ActionExecutorRegistry executors, IVesselStatus vesselStatus)
        {
            var starlings = executors ? executors.Get<StarlingSwarmExecutor>() : null;
            if (starlings) starlings.ReleasePick(element);
        }
    }
}
