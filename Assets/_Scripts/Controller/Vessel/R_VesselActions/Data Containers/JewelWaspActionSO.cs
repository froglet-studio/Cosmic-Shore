using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>The Jewel Wasp's three controls (values are serialized on the control assets - never renumber).</summary>
    public enum JewelWaspControl
    {
        /// <summary>RT. Free or in a plant: aim at a creature (it lights up) and let go to sting it. In a creature: the
        /// attack at a rival vessel ahead, or the suction at prisms (the Space ability).</summary>
        Right = 0,
        /// <summary>LT. Free or in a plant: aim at a plant and let go to sting it (the Mass ability). In a creature: a
        /// burst of speed (the Time ability).</summary>
        Left = 1,
        /// <summary>A (on screen on a phone): leave the host (the Charge ability; Burst at Charge 5).</summary>
        Leave = 2,
    }

    /// <summary>
    /// One of the Jewel Wasp's controls (design: <c>R_VesselActions/JEWEL_WASP.md</c>). Stateless and shared: every press and
    /// release goes straight to the hull's own <see cref="JewelWaspExecutor"/>, which owns all state.
    /// </summary>
    [CreateAssetMenu(fileName = "JewelWaspAction", menuName = "ScriptableObjects/Vessel Actions/Jewel Wasp Control")]
    public sealed class JewelWaspActionSO : ShipActionSO
    {
        [Tooltip("Which of the Jewel Wasp's controls this asset is.")]
        [SerializeField] JewelWaspControl control;

        public JewelWaspControl Kind => control;

        public override void StartAction(ActionExecutorRegistry executors, IVesselStatus vesselStatus)
        {
            var wasp = executors ? executors.Get<JewelWaspExecutor>() : null;
            if (wasp) wasp.Press(control, true);
        }

        public override void StopAction(ActionExecutorRegistry executors, IVesselStatus vesselStatus)
        {
            var wasp = executors ? executors.Get<JewelWaspExecutor>() : null;
            if (wasp) wasp.Press(control, false);
        }
    }
}
