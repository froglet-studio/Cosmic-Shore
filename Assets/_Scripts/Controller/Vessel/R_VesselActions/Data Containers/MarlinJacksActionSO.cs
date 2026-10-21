using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>The marlin's button controls (values are serialized on the control assets - never renumber).</summary>
    public enum MarlinJacksControl
    {
        Call = 0,
        Teleport = 1,
        Decoy = 2,
    }

    /// <summary>
    /// One of the marlin's button controls (design: <c>R_VesselActions/MARLIN_JACKS.md</c>). Stateless and shared: every
    /// press goes straight to the hull's own <see cref="MarlinJacksExecutor"/>, which owns all state. All three are
    /// press-and-forget (vessel rule 37): the CALL (a long cooldown), the TELEPORT to the decoy (Garrett: "a trigger or lift
    /// a thumb"), and the marlin's own DECOY (the Time level-5 upgrade; nothing without it).
    /// </summary>
    [CreateAssetMenu(fileName = "MarlinJacksAction", menuName = "ScriptableObjects/Vessel Actions/Marlin and Jacks Control")]
    public sealed class MarlinJacksActionSO : ShipActionSO
    {
        [Tooltip("Which of the marlin's button controls this asset is.")]
        [SerializeField] MarlinJacksControl control;

        public MarlinJacksControl Kind => control;

        public override void StartAction(ActionExecutorRegistry executors, IVesselStatus vesselStatus)
        {
            var crew = executors ? executors.Get<MarlinJacksExecutor>() : null;
            if (!crew) return;
            switch (control)
            {
                case MarlinJacksControl.Call: crew.Call(); break;
                case MarlinJacksControl.Teleport: crew.Teleport(); break;
                case MarlinJacksControl.Decoy: crew.DropMarlinDecoy(); break;
            }
        }

        public override void StopAction(ActionExecutorRegistry executors, IVesselStatus vesselStatus) { }
    }
}
