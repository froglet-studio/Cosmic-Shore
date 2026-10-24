using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Replicates the Rhino sword's OWNER-AUTHORITATIVE state: stored energy, the energize phase
    /// and whether an omni-crystal surge is holding it. Lives on the Rhino root beside its
    /// NetworkObject; <see cref="ShieldSkimmerScaleDriver"/> publishes on the owning machine and
    /// mirrors everywhere else.
    ///
    /// <para><b>Why it has to be replicated.</b> Every peer simulates its own prism field, and the
    /// blade's effect on it is decided by state that used to be computed per peer: energy banked
    /// from that peer's own kills (so a peer that missed one cut held less energy - a shorter
    /// blade, and an energize that could fail on one machine and succeed on another) and an
    /// energize phase run from each peer's own clock. An energized blade POPS super-shielded mass,
    /// so a divergent phase is a divergent prismscape. The owner's verdict is now the only one.</para>
    ///
    /// <para>The triggers themselves already replicate (InputStatus's owner-write trigger mirrors),
    /// and the blade's POSE is read from them on every peer (<see cref="ShieldSwipeActionExecutor"/>),
    /// so this relay carries only the state the triggers cannot imply.</para>
    ///
    /// <para>Off the network (a menu vessel, an un-spawned editor test) <see cref="IsAuthority"/> is
    /// true and the driver runs its own state machine exactly as before.</para>
    /// </summary>
    public sealed class RhinoSwordNetworkRelay : NetworkBehaviour
    {
        // Energy is quantized before it is written, so a meter creeping by a hair a frame does not
        // dirty the variable every tick. 1/1024 is far below anything the blade length or the HUD
        // can draw.
        const float EnergyQuantum = 1f / 1024f;
        const byte SurgeBit = 0x10;
        const byte PhaseMask = 0x0F;

        readonly NetworkVariable<float> n_energy = new(
            readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);

        // Low nibble: RhinoSwordEnergizePhase. SurgeBit: an omni-crystal surge is holding the blade.
        readonly NetworkVariable<byte> n_state = new(
            readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);

        /// <summary>True where the sword's state is DECIDED: the owning machine (the pilot's, or the
        /// server for an AI), or anywhere at all while the vessel is not network-spawned.</summary>
        public bool IsAuthority => !IsSpawned || IsOwner;

        public float Energy01 => n_energy.Value;
        public RhinoSwordEnergizePhase Phase => (RhinoSwordEnergizePhase)(n_state.Value & PhaseMask);
        public bool IsSurging => (n_state.Value & SurgeBit) != 0;

        /// <summary>Owner only: publish this frame's state. Writes nothing that has not changed.</summary>
        public void Publish(float energy01, RhinoSwordEnergizePhase phase, bool surging)
        {
            if (!IsSpawned || !IsOwner) return;

            float quantized = Mathf.Round(Mathf.Clamp01(energy01) / EnergyQuantum) * EnergyQuantum;
            if (!Mathf.Approximately(quantized, n_energy.Value))
                n_energy.Value = quantized;

            byte state = (byte)(((byte)phase & PhaseMask) | (surging ? SurgeBit : 0));
            if (state != n_state.Value)
                n_state.Value = state;
        }
    }
}
