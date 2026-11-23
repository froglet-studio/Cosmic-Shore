using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>The jacks' button controls (values are serialized on the control assets - never renumber).</summary>
    public enum JacksControl
    {
        Call = 0,
        Teleport = 1,
        Decoy = 2,
        TeleportLeft = 3,
        TeleportRight = 4,
        Wide = 5,
    }

    /// <summary>
    /// One of the jacks' button controls, read while they fly (design: <c>R_VesselActions/JACKS.md</c>). Stateless and shared: every
    /// press goes straight to the hull's own <see cref="JacksExecutor"/>, which owns all state. All are press-and-forget
    /// (vessel rule 37): the CALL (the Mass ability), WIDE (the Space ability), and the JUMP to a decoy (the Time ability:
    /// the left trigger's, the right trigger's, or - a phone's both-thumbs lift - the one placed last). The flying jacks'
    /// own DECOY (the old Time level-5 upgrade) was retired on 2026-10-25 for the second decoy; its value stays (it is
    /// serialized) and does nothing.
    /// </summary>
    [CreateAssetMenu(fileName = "JacksAction", menuName = "ScriptableObjects/Vessel Actions/Jacks Control")]
    public sealed class JacksActionSO : ShipActionSO
    {
        [Tooltip("Which of the jacks' button controls this asset is.")]
        [SerializeField] JacksControl control;

        public JacksControl Kind => control;

        public override void StartAction(ActionExecutorRegistry executors, IVesselStatus vesselStatus)
        {
            var crew = executors ? executors.Get<JacksExecutor>() : null;
            if (!crew) return;
            switch (control)
            {
                case JacksControl.Call: crew.Call(); break;
                case JacksControl.Teleport: crew.Teleport(); break;
                case JacksControl.TeleportLeft: crew.Teleport(0); break;
                case JacksControl.TeleportRight: crew.Teleport(1); break;
                case JacksControl.Wide: crew.ToggleWide(); break;
                case JacksControl.Decoy: break;   // the flying decoy, retired 2026-10-25 (Time 5 is the second decoy); unbound
            }
        }

        public override void StopAction(ActionExecutorRegistry executors, IVesselStatus vesselStatus) { }
    }
}
