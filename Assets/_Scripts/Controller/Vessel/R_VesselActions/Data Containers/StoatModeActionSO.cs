using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The Stoat's mode asset. Until 2026-11-02 it was the B switch, Mass → Time → Gate; Mass mode (the field dipole) and
    /// Gate mode (the warp gate) were removed from the kit then, and with them the switch and every row this asset held
    /// (the thick trail, Armoured Wake, the gate, the autopilot's switching, three FMOD slots). What they were and how to
    /// bring them back: <c>Docs/Studios/STOAT_FIELD_TRAJECTORY_PARKED.md</c>.
    ///
    /// <para>Still an action, so a pad or card bound to it presses nothing rather than breaking; the Stoat binds it to no
    /// input. <see cref="StoatModeExecutor"/> keeps a reference to it.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "StoatModeAction", menuName = "ScriptableObjects/Vessel Actions/Stoat Mode")]
    public sealed class StoatModeActionSO : ShipActionSO
    {
        /// <summary>No mode to switch to: the Stoat flies Time mode only.</summary>
        public override void StartAction(ActionExecutorRegistry executors, IVesselStatus status) { }

        /// <summary>No release semantics (vessel rule 37).</summary>
        public override void StopAction(ActionExecutorRegistry executors, IVesselStatus status) { }
    }
}
