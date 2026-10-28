using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The Meerkat's Charge ability, <b>Sentinel mode</b> (Garrett's design, approved by Yash 2026-10-28;
    /// <c>R_VesselActions/MEERKAT.md</c> §10), on Y (<c>InputEvents.Button4Action</c>): each press toggles it. While it is on,
    /// each new soliton leaves a sentinel at its way in (<see cref="MeerkatSolitonKit"/>). Shared and stateless: the mode
    /// lives on the executor.
    ///
    /// <para><b>A press-and-forget toggle</b> (vessel rule 37): <see cref="StopAction"/>, which fires on every release and on
    /// the AI's Start-then-Stop, does nothing, or a press would undo itself.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "MeerkatSentinelModeAction", menuName = "ScriptableObjects/Vessel Actions/Meerkat Sentinel Mode")]
    public sealed class MeerkatSentinelModeActionSO : ShipActionSO
    {
        // Implicit-bool, never ?. - a destroyed executor is a Unity null that ?. would call into.
        public override void StartAction(ActionExecutorRegistry executors, IVesselStatus status)
        {
            var kit = executors ? executors.Get<MeerkatSolitonKit>() : null;
            if (kit) kit.ToggleSentinelMode();
        }

        public override void StopAction(ActionExecutorRegistry executors, IVesselStatus status) { }
    }
}
