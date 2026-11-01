using System.Collections.Generic;
using CosmicShore.Data;
using Unity.Netcode;
using Unity.Profiling;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// FULMEN over the network (<c>Fulmen/README.md</c> § Network). The server is the only peer that counts charge,
    /// decides a strike, strikes crystals and ignites prisms; clients replay it from three compact reliable messages
    /// sent on the collector's <see cref="NetworkVesselImpactor"/> (so they share one ordered channel):
    /// <list type="bullet">
    /// <item><b>Strike</b> (<see cref="StrikeMsg"/>, 40 B): id, config, seed, centre, range, excluded crystal, server
    /// time - the client grows the identical root bolt.</item>
    /// <item><b>Child</b> (<see cref="ChildMsg"/>, 28 B): strike id, crystal id, child seed, generation, origin, range -
    /// sent as the server derived it, never re-derived on the client (no float drift).</item>
    /// <item><b>Burns</b> (<see cref="BurnBatch"/>, 11 B + 8 B a prism, ≤ <see cref="MaxPerMessage"/> a message): every
    /// burn the strike lit this frame - its own and its spreads - as a position relative to the strike centre (1/8
    /// unit), a noise seed and a duration. One batch per strike per frame, chunked past the cap.</item>
    /// </list>
    /// A client resolves each burn to its own copy of the prism (nearest live prism at that spot: trail and
    /// environment prisms have no network id - <see cref="Prism.SpatialIndexId"/> is per-peer registration order) and
    /// burns it locally on the server's timeline (<see cref="PrismBurnManager.IgniteRemote"/>): no spread, the quiet
    /// burnout. Every peer simulates destruction locally (as for any trail damage), so there is no second destroy.
    /// Zero GC per message: fixed rings and buffers, cached delegates.
    /// </summary>
    public static class FulmenNetSync
    {
        public const int MaxPerMessage = 150;   // 11 + 150 * 8 = 1211 B: stays under one MTU-sized packet
        const int Ring = 64;                    // live strikes remembered (ids wrap through 1..65535)
        const int MaxQueued = 1024;
        const float Quant = 8f;                 // offsets in 1/8 unit, so ±4096 units around the strike centre
        const float MatchRadius = 0.75f;        // a client finds its prism within this of the sent spot

        static readonly ProfilerMarker s_mSend = new("Fulmen.Net.Send");
        static readonly ProfilerMarker s_mFlush = new("Fulmen.Net.Flush");
        static readonly ProfilerMarker s_mApply = new("Fulmen.Net.Apply");

        public struct StrikeMsg : INetworkSerializeByMemcpy
        {
            public double ServerTime;
            public Vector3 Centre;
            public float Range;
            public int ConfigId, Seed, ExcludeCrystal;
            public ushort Id;
        }

        public struct ChildMsg : INetworkSerializeByMemcpy
        {
            public Vector3 At;
            public float Range;
            public int Crystal, Seed;
            public ushort Id;
            public byte Generation;
        }

        /// <summary>One burn on the wire (8 B).</summary>
        public struct Burn : INetworkSerializeByMemcpy
        {
            public short X, Y, Z;
            public byte Seed;       // PrismBurnManager.PackSeed
            public byte Tenths;     // burn seconds × 10
        }

        /// <summary>A strike's burns lit in one server frame. The entries live in static buffers (one send, one receive)
        /// so a batch allocates nothing; everything runs on the main thread.</summary>
        public struct BurnBatch : INetworkSerializable
        {
            public ushort Id;
            public double ServerTime;
            public byte Count;

            internal static readonly Burn[] Send = new Burn[MaxPerMessage];
            internal static readonly Burn[] Recv = new Burn[MaxPerMessage];

            public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
            {
                s.SerializeValue(ref Id);
                s.SerializeValue(ref ServerTime);
                s.SerializeValue(ref Count);
                if (Count > MaxPerMessage) Count = MaxPerMessage;
                var buf = s.IsReader ? Recv : Send;
                for (int i = 0; i < Count; i++) s.SerializeValue(ref buf[i]);
            }
        }

        struct Live
        {
            public ushort Id;
            public NetworkVesselImpactor Relay;
            public FulmenConfigSO Config;
            public Vector3 Centre;
            public string Igniter;
            public Domains Domain;
        }

        static readonly Live[] s_live = new Live[Ring];
        static ushort s_next;
        static readonly ushort[] s_qId = new ushort[MaxQueued];
        static readonly Burn[] s_qBurn = new Burn[MaxQueued];
        static int s_queued;
        static readonly List<Prism> s_hits = new();
        static bool s_hooked;

        static NetworkManager Nm
        {
            get
            {
                var nm = NetworkManager.Singleton;
                return nm != null && nm.IsListening ? nm : null;
            }
        }

        /// <summary>True when this peer is a client of a running session (its Fulmen comes from the server).</summary>
        public static bool IsRemoteClient
        {
            get { var nm = Nm; return nm != null && !nm.IsServer; }
        }

        /// <summary>
        /// SERVER: reserve a network id for a strike by <paramref name="relay"/>'s vessel. 0 (strike locally only) when
        /// offline, not the server, or no remote client is connected - a solo host sends nothing.
        /// </summary>
        public static ushort ServerAllocate(NetworkVesselImpactor relay, FulmenConfigSO config, Vector3 centre,
            string igniter, Domains domain)
        {
            var nm = Nm;
            if (nm == null || !nm.IsServer || relay == null || !relay.IsSpawned || config == null) return 0;
            if (nm.ConnectedClientsIds.Count <= (nm.IsHost ? 1 : 0)) return 0;
            Hook();
            if (++s_next == 0) s_next = 1;
            s_live[s_next % Ring] = new Live
            {
                Id = s_next, Relay = relay, Config = config, Centre = centre, Igniter = igniter, Domain = domain,
            };
            return s_next;
        }

        /// <summary>SERVER: the strike <paramref name="id"/> went off; tell every client.</summary>
        public static void ServerSendStrike(ushort id, int seed, float range, int excludeCrystal)
        {
            ref var l = ref s_live[id % Ring];
            if (id == 0 || l.Id != id || l.Relay == null) return;
            using var _ = s_mSend.Auto();
            l.Relay.SendFulmenStrike(new StrikeMsg
            {
                Id = id, ConfigId = l.Config.NetId, Seed = seed, Centre = l.Centre, Range = range,
                ExcludeCrystal = excludeCrystal, ServerTime = Nm != null ? Nm.ServerTime.Time : 0d,
            });
        }

        static void Hook()
        {
            if (s_hooked) return;
            s_hooked = true;
            FulmenSystem.ChildQueued = OnChildQueued;
            PrismBurnManager.NetIgnited = OnIgnited;
        }

        static void OnChildQueued(FulmenSystem.Chain chain, int crystal, int seed, int generation, Vector3 at, float range)
        {
            ref var l = ref s_live[chain.NetId % Ring];
            if (l.Id != chain.NetId || l.Relay == null || !l.Relay.IsSpawned) return;
            using var _ = s_mSend.Auto();
            l.Relay.SendFulmenChild(new ChildMsg
            {
                Id = chain.NetId, Crystal = crystal, Seed = seed, Generation = (byte)generation, At = at, Range = range,
            });
        }

        static void OnIgnited(ushort id, Prism prism, float seconds, float seed)
        {
            if (s_queued >= MaxQueued || prism == null) return;
            ref var l = ref s_live[id % Ring];
            if (l.Id != id) return;
            var d = (prism.transform.position - l.Centre) * Quant;
            if (Mathf.Abs(d.x) > 32767f || Mathf.Abs(d.y) > 32767f || Mathf.Abs(d.z) > 32767f) return;
            s_qId[s_queued] = id;
            s_qBurn[s_queued++] = new Burn
            {
                X = (short)Mathf.RoundToInt(d.x), Y = (short)Mathf.RoundToInt(d.y), Z = (short)Mathf.RoundToInt(d.z),
                Seed = PrismBurnManager.PackSeed(seed),
                Tenths = (byte)Mathf.Clamp(Mathf.RoundToInt(seconds * 10f), 1, 255),
            };
        }

        /// <summary>SERVER: send this frame's burns, one batch per strike (chunked at <see cref="MaxPerMessage"/>).
        /// Called once a frame from <see cref="FulmenSystem"/>'s LateUpdate.</summary>
        public static void Flush()
        {
            if (s_queued == 0) return;
            using var _ = s_mFlush.Auto();
            var nm = Nm;
            double now = nm != null ? nm.ServerTime.Time : 0d;
            var send = BurnBatch.Send;
            while (s_queued > 0)
            {
                ushort id = s_qId[0];
                int n = 0, keep = 0;
                for (int i = 0; i < s_queued; i++)
                {
                    if (s_qId[i] == id && n < MaxPerMessage) send[n++] = s_qBurn[i];
                    else { s_qId[keep] = s_qId[i]; s_qBurn[keep++] = s_qBurn[i]; }
                }
                s_queued = keep;
                ref var l = ref s_live[id % Ring];
                if (nm == null || l.Id != id || l.Relay == null || !l.Relay.IsSpawned) continue;   // collector gone: drop
                l.Relay.SendFulmenBurns(new BurnBatch { Id = id, ServerTime = now, Count = (byte)n });
            }
        }

        // ---------------- client ----------------

        /// <summary>CLIENT: the server struck; grow the same root bolt (remote: children and burns arrive by RPC).</summary>
        internal static void ApplyStrike(NetworkVesselImpactor relay, in StrikeMsg m)
        {
            using var _ = s_mApply.Auto();
            var config = FulmenConfigSO.ByNetId(m.ConfigId);
            if (config == null) return;
            var status = relay.VesselImpactor ? relay.VesselImpactor.Vessel?.VesselStatus : null;
            string igniter = status?.PlayerName;
            var domain = status != null ? status.Domain : default;
            s_live[m.Id % Ring] = new Live
            {
                Id = m.Id, Relay = relay, Config = config, Centre = m.Centre, Igniter = igniter, Domain = domain,
            };
            FulmenSystem.EnsureInstance().Strike(config, m.Centre, m.Range, m.Seed, false, igniter, domain,
                m.ExcludeCrystal, m.Id, remote: true);
        }

        /// <summary>CLIENT: the server's chain spawned a child.</summary>
        internal static void ApplyChild(in ChildMsg m)
        {
            var sys = FulmenSystem.Instance;
            if (sys != null) sys.QueueRemoteChild(m.Id, m.Crystal, m.Seed, m.Generation, m.At, m.Range);
        }

        /// <summary>CLIENT: burn this peer's copies of the server's burns, on the server's timeline.</summary>
        internal static void ApplyBurns(in BurnBatch b)
        {
            using var _ = s_mApply.Auto();
            ref var l = ref s_live[b.Id % Ring];
            var index = PrismSpatialIndex.Instance;
            if (l.Id != b.Id || index == null) return;   // missed the strike (joined late): nothing to anchor on
            var nm = Nm;
            float ago = nm != null ? Mathf.Max(0f, (float)(nm.ServerTime.Time - b.ServerTime)) : 0f;
            var burn = PrismBurnManager.EnsureInstance();
            bool convert = l.Config != null && l.Config.ConvertOnBurnout;
            var recv = BurnBatch.Recv;
            for (int i = 0; i < b.Count; i++)
            {
                var e = recv[i];
                var at = l.Centre + new Vector3(e.X, e.Y, e.Z) * (1f / Quant);
                var prism = Nearest(index, at, l.Domain);
                if (prism == null) continue;
                if (burn.IgniteRemote(prism, l.Igniter, l.Domain, convert, e.Tenths * 0.1f, ago,
                        PrismBurnManager.UnpackSeed(e.Seed)) && l.Config != null)
                    l.Config.LastPrismsBurnt++;
            }
        }

        static Prism Nearest(PrismSpatialIndex index, Vector3 at, Domains igniter)
        {
            s_hits.Clear();
            index.QuerySphere(at, MatchRadius, s_hits);
            Prism best = null;
            float bestSq = float.MaxValue;
            for (int i = 0; i < s_hits.Count; i++)
            {
                var p = s_hits[i];
                if (p == null || p.destroyed || p.Domain == igniter) continue;
                float d = (p.transform.position - at).sqrMagnitude;
                if (d < bestSq) { bestSq = d; best = p; }
            }
            s_hits.Clear();
            return best;
        }
    }
}
