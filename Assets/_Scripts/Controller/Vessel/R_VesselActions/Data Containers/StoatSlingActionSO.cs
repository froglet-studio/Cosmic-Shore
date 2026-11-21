using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// One trigger of the Stoat's slingshot (<c>R_VesselActions/STOAT.md</c>), bound to
    /// <c>InputEvents.LeftStickAction</c> (LT) or <c>RightStickAction</c> (RT). Both edges carry
    /// meaning — the press starts the squeeze, the release slings — so this is a real SO with a
    /// body, the <c>GibbonArmActionSO</c> shape. The asset is SHARED and STATELESS: which side it
    /// is comes from <see cref="side"/>, and every per-vessel number (the hold, the last pair)
    /// lives on <see cref="StoatSlingExecutor"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "StoatSlingAction", menuName = "ScriptableObjects/Vessel Actions/Stoat Sling")]
    public sealed class StoatSlingActionSO : ShipActionSO
    {
        [Tooltip("The trigger this asset is bound to. The ATTRACTOR lands on this side, the repulsor on the other.")]
        [SerializeField] StoatSlingExecutor.Side side = StoatSlingExecutor.Side.Left;

        public StoatSlingExecutor.Side Side => side;

        // Implicit-bool, never ?. — a destroyed executor is a Unity null that ?. would call into.
        public override void StartAction(ActionExecutorRegistry executors, IVesselStatus status)
        {
            var exec = executors ? executors.Get<StoatSlingExecutor>() : null;
            if (exec) exec.BeginHold(side);
        }

        public override void StopAction(ActionExecutorRegistry executors, IVesselStatus status)
        {
            var exec = executors ? executors.Get<StoatSlingExecutor>() : null;
            if (exec) exec.Release(side);
        }
    }
}
