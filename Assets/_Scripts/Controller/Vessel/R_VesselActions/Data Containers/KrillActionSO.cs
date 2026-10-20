using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One of the Krill's four buttons (design: <c>R_VesselActions/KRILL.md</c>): pick the hand's card of one element. The
    /// player only turns the viewing plane and picks cards; the swarm roams the cell on its own. Stateless and shared:
    /// every press goes straight to the hull's own <see cref="KrillSwarmExecutor"/>, which owns the hand. Press-and-forget,
    /// so there is no release (vessel rule 37).
    /// </summary>
    [CreateAssetMenu(fileName = "KrillPick", menuName = "ScriptableObjects/Vessel Actions/Krill Pick")]
    public sealed class KrillActionSO : ShipActionSO
    {
        [Tooltip("The element whose card in the hand this button picks.")]
        [SerializeField] Element element = Element.Mass;

        public Element Element => element;

        public override void StartAction(ActionExecutorRegistry executors, IVesselStatus vesselStatus)
        {
            var krill = executors ? executors.Get<KrillSwarmExecutor>() : null;
            if (krill) krill.Pick(element);
        }

        public override void StopAction(ActionExecutorRegistry executors, IVesselStatus vesselStatus) { }
    }
}
