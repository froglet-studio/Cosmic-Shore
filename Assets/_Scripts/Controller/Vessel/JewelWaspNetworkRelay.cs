using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Replicates the Jewel Wasp's possession, which only the SERVER can decide in a networked match: the creatures are
    /// simulated there (<c>FaunaNetworkSync.IsSimAuthority</c>), so the wasp that stings and steers one has to be decided
    /// there too, whoever owns the hull. Lives on the Jewel Wasp's root beside its NetworkObject (design:
    /// <c>R_VesselActions/JEWEL_WASP.md</c> §6.1).
    ///
    /// <para><b>What reaches the server already.</b> Every press reaches every peer through the action handler's
    /// ServerRpc / ClientRpc pair, and the sticks, throttle and triggers are owner-write mirrors on
    /// <c>InputStatus</c>, so the server's copy of a client's hull flies the host from the client's own sticks. This relay
    /// carries only what the server decides and the other machines cannot work out: the mode, where the hull nests, the
    /// recharges the pilot's HUD shows and the camera's stand-back; and one thing the OWNER says, whether its autopilot is
    /// flying (so the server runs the wasp's AI for a client hull on autopilot).</para>
    ///
    /// <para><b>Who places the hull.</b> The hull's transform is owner-authoritative (<c>ClientNetworkTransform</c>), so
    /// the OWNER puts its own hull at the nest the server publishes and holds it there; every other peer sees it through
    /// the transform sync. Every peer reads the mode to dress the model (the nested scale, the gaster's curl, the folded
    /// wings) and to keep the nested hull out of its own prism field.</para>
    ///
    /// <para>Off the network (single player, a menu hull, an un-spawned editor test) <see cref="IsDecider"/> is true and
    /// the executor runs exactly as before.</para>
    /// </summary>
    public sealed class JewelWaspNetworkRelay : NetworkBehaviour
    {
        public const byte ModeMask = 0x07;
        public const byte ArrivedBit = 0x08;      // stinging, and already at the heart (the sting's beat has begun)
        public const byte IncorporealBit = 0x10;  // the hull is inside its host (or in its leave grace): no colliders
        public const byte BurstingBit = 0x20;     // the host's speed burst is running

        readonly NetworkVariable<byte> n_state = new(
            readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);
        readonly NetworkVariable<Vector3> n_nestPosition = new(
            readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);
        readonly NetworkVariable<Quaternion> n_nestRotation = new(Quaternion.identity,
            readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);
        // x: the attack's recharge, y: the burst's, z: the sting's (each 0..1 remaining); w: the camera's stand-back (u)
        readonly NetworkVariable<Vector4> n_view = new(
            readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

        // the OWNER's autopilot (a client hull flown by its AI): the server runs the wasp's AI for it, since the flag itself
        // lives on the owner's AIPilot and is not replicated
        readonly NetworkVariable<bool> n_ownerAutoPilot = new(
            readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);

        /// <summary>True where the possession is DECIDED: the server, or anywhere while the hull is not network-spawned.</summary>
        public bool IsDecider => !IsSpawned || IsServer;

        /// <summary>True while the hull is spawned on a network, so the decider's state is the replicated one.</summary>
        public bool IsNetworked => IsSpawned;

        public byte State => n_state.Value;
        public Vector3 NestPosition => n_nestPosition.Value;
        public Quaternion NestRotation => n_nestRotation.Value;
        public Vector4 View => n_view.Value;

        /// <summary>True while the hull's OWNER is flown by its autopilot (read by the server to run the wasp's AI).</summary>
        public bool OwnerAutoPilot => IsSpawned && n_ownerAutoPilot.Value;

        /// <summary>Owner only: say whether the autopilot flies this hull. Writes nothing that has not changed.</summary>
        public void PublishOwnerAutoPilot(bool on)
        {
            if (!IsSpawned || !IsOwner || n_ownerAutoPilot.Value == on) return;
            n_ownerAutoPilot.Value = on;
        }

        /// <summary>Server only: publish this frame's decision. Writes nothing that has not changed.</summary>
        public void Publish(byte state, Vector3 nestPosition, Quaternion nestRotation, Vector4 view)
        {
            if (!IsSpawned || !IsServer) return;
            if (state != n_state.Value) n_state.Value = state;
            if ((state & ModeMask) != 0)
            {
                if ((nestPosition - n_nestPosition.Value).sqrMagnitude > 1e-4f) n_nestPosition.Value = nestPosition;
                if (Quaternion.Angle(nestRotation, n_nestRotation.Value) > 0.2f) n_nestRotation.Value = nestRotation;
            }
            if ((view - n_view.Value).sqrMagnitude > 1e-4f) n_view.Value = view;
        }
    }
}
