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
    /// <para><b>The roots</b> (<c>MEERKAT.md</c> §7, protocol version 22): the owner publishes each network of roots it
    /// grows from an omnicrystal - its seed, its size (round 24: the reach share of the cell the owner read from its Mass;
    /// the same float as before, so the layout of the payload, and the protocol version, are unchanged), the hull's frame where it grew, and which omnicrystal it
    /// took (<see cref="PublishRoots"/>) - and its rider's way out (<see cref="PublishRootsOut"/>). Every peer grows the same
    /// roots from those (<see cref="MeerkatRootMath"/> is double precision for exactly this).</para>
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
            public float L, S, R, B, M, Theta, FormT;
            public bool Out;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref Seq);
                serializer.SerializeValue(ref A);
                serializer.SerializeValue(ref T0);
                serializer.SerializeValue(ref Side);
                serializer.SerializeValue(ref L);
                serializer.SerializeValue(ref S);
                serializer.SerializeValue(ref R);
                serializer.SerializeValue(ref B);
                serializer.SerializeValue(ref M);
                serializer.SerializeValue(ref Theta);
                serializer.SerializeValue(ref FormT);
                serializer.SerializeValue(ref Out);
            }

            public bool Equals(TunnelState o) => Seq == o.Seq && Out == o.Out && A == o.A && T0 == o.T0 && Side == o.Side &&
                                                  L == o.L && S == o.S && R == o.R && B == o.B && M == o.M && Theta == o.Theta && FormT == o.FormT;
        }

        /// <summary>One network of roots: its sequence number, seed and size, the frame it grew in, the omnicrystal taken,
        /// and whether its rider is out.</summary>
        public struct RootsState : INetworkSerializable, System.IEquatable<RootsState>
        {
            public int Seq, Crystal;
            public uint Seed;
            public float Size;
            public Vector3 A, T0, Right, Up;
            public bool Out;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref Seq);
                serializer.SerializeValue(ref Crystal);
                serializer.SerializeValue(ref Seed);
                serializer.SerializeValue(ref Size);
                serializer.SerializeValue(ref A);
                serializer.SerializeValue(ref T0);
                serializer.SerializeValue(ref Right);
                serializer.SerializeValue(ref Up);
                serializer.SerializeValue(ref Out);
            }

            public bool Equals(RootsState o) => Seq == o.Seq && Crystal == o.Crystal && Seed == o.Seed && Size == o.Size && A == o.A &&
                                                 T0 == o.T0 && Right == o.Right && Up == o.Up && Out == o.Out;
        }

        readonly NetworkVariable<RootsState> n_roots = new(
            readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);

        readonly NetworkVariable<TunnelState> n_tunnel = new(
            readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Owner);

        MeerkatWormholeExecutor _executor;

        internal void Bind(MeerkatWormholeExecutor executor) => _executor = executor;

        public override void OnNetworkSpawn()
        {
            n_tunnel.OnValueChanged += OnTunnelChanged;
            n_roots.OnValueChanged += OnRootsChanged;
        }

        public override void OnNetworkDespawn()
        {
            n_tunnel.OnValueChanged -= OnTunnelChanged;
            n_roots.OnValueChanged -= OnRootsChanged;
        }

        /// <summary>Owner: roots grew from an omnicrystal.</summary>
        internal void PublishRoots(int seq, uint seed, float size, Vector3 a, Vector3 t0, Vector3 right, Vector3 up, int crystal)
        {
            if (!IsSpawned || !IsOwner) return;
            n_roots.Value = new RootsState { Seq = seq, Seed = seed, Size = size, A = a, T0 = t0, Right = right, Up = up, Crystal = crystal };
        }

        /// <summary>Owner: its rider came out of roots <paramref name="seq"/>.</summary>
        internal void PublishRootsOut(int seq)
        {
            if (!IsSpawned || !IsOwner) return;
            var s = n_roots.Value;
            if (s.Seq != seq || s.Out) return;
            s.Out = true;
            n_roots.Value = s;
        }

        void OnRootsChanged(RootsState before, RootsState now)
        {
            if (IsOwner || !_executor) return;
            if (now.Seq != before.Seq)
                _executor.GrowRootsRemote(now.Seq, now.Seed, now.Size, now.A, now.T0, now.Right, now.Up, now.Crystal);
            if (now.Out && (!before.Out || now.Seq != before.Seq)) _executor.RootsOutRemote(now.Seq);
        }

        /// <summary>Owner: a tunnel was formed.</summary>
        internal void PublishFormed(MeerkatWormholeExecutor.Tunnel t)
        {
            if (!IsSpawned || !IsOwner) return;
            n_tunnel.Value = new TunnelState
            {
                Seq = t.Seq, A = t.A, T0 = t.T0, Side = t.Side, L = t.L, S = t.S, R = t.R, B = t.B, M = t.M, Theta = t.Theta, FormT = t.FormT,
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
                _executor.FormRemote(now.Seq, now.A, now.T0, now.Side, now.L, now.S, now.R, now.B, now.M, now.Theta, now.FormT);
            if (now.Out && (!before.Out || now.Seq != before.Seq)) _executor.RiderOutRemote(now.Seq);
        }
    }
}
