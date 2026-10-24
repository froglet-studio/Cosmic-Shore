using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>The Flea's two controls (FLEA.md §2). Values are serialized: never renumber.</summary>
    public enum FleaControl
    {
        Crouch = 0,
        Bounce = 1,
    }

    /// <summary>
    /// One Flea control, both HELD: Crouch (LT on a pad, A / left mouse otherwise) charges a hop and hops on release;
    /// Bounce (RT on a pad, X / right mouse otherwise) is the press a perfect bounce is timed by. Shared and stateless: the
    /// state is <see cref="FleaVesselTransformer"/>'s, reached through the vessel's status.
    /// </summary>
    [CreateAssetMenu(fileName = "FleaAction", menuName = "ScriptableObjects/Vessel Actions/Flea Control")]
    public sealed class FleaActionSO : ShipActionSO
    {
        [Tooltip("Which of the Flea's controls this asset is.")]
        [SerializeField] FleaControl control;

        public FleaControl Kind => control;

        public override void StartAction(ActionExecutorRegistry executors, IVesselStatus vesselStatus) => Set(vesselStatus, true);

        public override void StopAction(ActionExecutorRegistry executors, IVesselStatus vesselStatus) => Set(vesselStatus, false);

        void Set(IVesselStatus vesselStatus, bool held)
        {
            if (vesselStatus == null || vesselStatus.VesselTransformer is not FleaVesselTransformer flea || !flea) return;
            if (control == FleaControl.Crouch) flea.SetCrouch(held);
            else flea.SetBouncePress(held);
        }
    }
}
