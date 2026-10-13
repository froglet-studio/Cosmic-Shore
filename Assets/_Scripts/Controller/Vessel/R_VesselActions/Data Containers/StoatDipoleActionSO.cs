using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// One trigger of the Stoat's field dipole (<c>R_VesselActions/STOAT_DIPOLE.md</c>), bound to LT or
    /// RT. The press opens the pair (or joins the one the other trigger opened); the release lets this
    /// trigger's share of the separation go. Shared and stateless: the side comes from
    /// <see cref="side"/>, everything else lives on <see cref="StoatDipoleExecutor"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "StoatDipoleAction", menuName = "ScriptableObjects/Vessel Actions/Stoat Dipole")]
    public sealed class StoatDipoleActionSO : ShipActionSO
    {
        [Tooltip("The trigger this asset is bound to. Its squeeze pulls the SINK toward this side; both together pull it straight ahead.")]
        [SerializeField] StoatDipoleExecutor.Side side = StoatDipoleExecutor.Side.Left;

        public StoatDipoleExecutor.Side Side => side;

        // Implicit-bool, never ?. — a destroyed executor is a Unity null that ?. would call into.
        public override void StartAction(ActionExecutorRegistry executors, IVesselStatus status)
        {
            var exec = executors ? executors.Get<StoatDipoleExecutor>() : null;
            if (exec) exec.BeginHold(side);
        }

        public override void StopAction(ActionExecutorRegistry executors, IVesselStatus status)
        {
            var exec = executors ? executors.Get<StoatDipoleExecutor>() : null;
            if (exec) exec.Release(side);
        }
    }
}
