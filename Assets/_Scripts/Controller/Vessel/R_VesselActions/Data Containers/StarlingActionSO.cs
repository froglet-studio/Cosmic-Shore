using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One of the Starlings' four buttons (design: <c>R_VesselActions/STARLING.md</c>): pick the hand's card of one element. The
    /// player only turns the viewing plane and picks cards; the swarm roams the cell on its own. Stateless and shared:
    /// every press goes straight to the hull's own <see cref="StarlingSwarmExecutor"/>, which owns the hand. Press-and-forget,
    /// so there is no release (vessel rule 37).
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
            if (starlings) starlings.Pick(element);
        }

        public override void StopAction(ActionExecutorRegistry executors, IVesselStatus vesselStatus) { }
    }
}
