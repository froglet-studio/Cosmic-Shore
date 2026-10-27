using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Replicates the Meerkat's tunnels (<c>R_VesselActions/MEERKAT.md</c>): the shape of each one its owner forms and the
    /// moment its rider comes out, so every peer runs the same tunnel life (<see cref="MeerkatWormholeExecutor"/>) and sees
    /// the same bridge and bent space. Lives on the Meerkat root beside its NetworkObject.
    ///
    /// <para><b>Why it has to be replicated.</b> RT's press replicates, but what it forms does not follow from it: the
    /// reticle is shaped by the owner's right stick and LT, which no other peer reads. So the owner publishes the formed
    /// tunnel's frame and shape (<see cref="PublishFormed"/>) and its rider's way out (<see cref="PublishRiderOut"/>); a
    /// remote peer forms its copy and starts its stay from those. The hull's pose replicates as every vessel's does.</para>
    ///
    /// <para>Off the network (a menu vessel, an un-spawned editor test) nothing is sent: the executor is its own
    /// authority.</para>
    /// </summary>
    public sealed class MeerkatWormholeNetworkRelay : NetworkBehaviour
    {
        /// <summary>One formed tunnel: its sequence number, frame and shape, and whether its rider is out.</summary>
        public struct TunnelState : INetworkSerializable, System.IEquatable<TunnelState>
        {
            public int Seq;
            public Vector3 A, T0, Side;
            public float L, B, M, Theta, FormT;
            public bool Out;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref Seq);
                serializer.SerializeValue(ref A);
                serializer.SerializeValue(ref T0);
                serializer.SerializeValue(ref Side);
                serializer.SerializeValue(ref L);
                serializer.SerializeValue(ref B);
                serializer.SerializeValue(ref M);
                serializer.SerializeValue(ref Theta);
                serializer.SerializeValue(ref FormT);
                serializer.SerializeValue(ref Out);
            }

            public bool Equals(TunnelState o) => Seq == o.Seq && Out == o.Out && A == o.A && T0 == o.T0 && Side == o.Side &&
                                                  L == o.L && B == o.B && M == o.M && Theta == o.Theta && FormT == o.FormT;
        }

        readonly NetworkVariable<TunnelState> n_tunnel = new(
            readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);

        MeerkatWormholeExecutor _executor;

        internal void Bind(MeerkatWormholeExecutor executor) => _executor = executor;

        public override void OnNetworkSpawn() => n_tunnel.OnValueChanged += OnTunnelChanged;

        public override void OnNetworkDespawn() => n_tunnel.OnValueChanged -= OnTunnelChanged;

        /// <summary>Owner: a tunnel was formed.</summary>
        internal void PublishFormed(MeerkatWormholeExecutor.Tunnel t)
        {
            if (!IsSpawned || !IsOwner) return;
            n_tunnel.Value = new TunnelState
            {
                Seq = t.Seq, A = t.A, T0 = t.T0, Side = t.Side, L = t.L, B = t.B, M = t.M, Theta = t.Theta, FormT = t.FormT,
            };
        }

        /// <summary>Owner: its rider came out of tunnel <paramref name="seq"/>.</summary>
        internal void PublishRiderOut(int seq)
        {
            if (!IsSpawned || !IsOwner) return;
            var s = n_tunnel.Value;
            if (s.Seq != seq || s.Out) return;
            s.Out = true;
            n_tunnel.Value = s;
        }

        void OnTunnelChanged(TunnelState before, TunnelState now)
        {
            if (IsOwner || !_executor) return;
            if (now.Seq != before.Seq)
                _executor.FormRemote(now.Seq, now.A, now.T0, now.Side, now.L, now.B, now.M, now.Theta, now.FormT);
            if (now.Out && (!before.Out || now.Seq != before.Seq)) _executor.RiderOutRemote(now.Seq);
        }
    }
}
