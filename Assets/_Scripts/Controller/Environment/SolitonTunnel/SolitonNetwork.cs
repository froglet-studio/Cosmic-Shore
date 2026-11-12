using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A SOLITON NETWORK: a tree of Soliton Tunnel channels (an environmental fundamental, <c>Docs/WORMHOLES.md</c>). The
    /// Meerkat grows one from an omnicrystal (<c>R_VesselActions/MEERKAT.md</c> §7); a <see cref="SolitonTunnelPlacer"/> can
    /// lay one in a scene with no maker. Its channels (<see cref="SolitonTunnelMath.Build"/>), each one's dug window and life
    /// (<see cref="SolitonTunnelMath.Life"/>), the network's up, its config and tint, and who grew it (null: placed). Every
    /// peer builds the same one from the same seed and frame (<see cref="MeerkatSolitonNetworkRelay"/> publishes a maker's);
    /// <see cref="SolitonTunnelLensPass"/> bends space round its channels and <see cref="SolitonTunnelView"/> draws its bridge.
    /// </summary>
    public sealed class SolitonNetwork
    {
        static readonly List<SolitonNetwork> s_live = new();
        static readonly ProfilerMarker s_mGrow = new("Soliton.Network.Grow");

        /// <summary>Every network alive now, everyone's.</summary>
        public static IReadOnlyList<SolitonNetwork> Live => s_live;

        public readonly List<SolitonTunnelMath.Channel> Channels;
        public readonly SolitonTunnelMath.Life[] Life;
        public readonly SolitonTunnelMath.D3 Up;
        /// <summary>Who grew it (null: placed in the scene, no maker).</summary>
        public readonly ISolitonTunnelMaker Maker;
        /// <summary>Its look, shape, life and slide.</summary>
        public readonly SolitonTunnelConfigSO Config;
        /// <summary>Its domain tint (rgb) and strength (a), from <see cref="SolitonTunnelConfigSO.Tint"/>.</summary>
        public readonly Color Tint;
        /// <summary>Never collapses on its own (placed and permanent).</summary>
        public bool Held;
        public readonly int Seq;
        /// <summary><see cref="Alive"/> as a delegate, made once (the flight asks it every frame).</summary>
        public readonly System.Func<int, bool> AliveFn;
        /// <summary>Round 28: a RIFT (<see cref="LayRift"/>), the path a Meerkat flew into the void, laid as one channel that
        /// anyone rides from its way in.</summary>
        public bool IsRift { get; private set; }
        /// <summary>How long its channels stay once laid (s); below 0, its config's <see cref="SolitonTunnelConfigSO.StaySeconds"/>.
        /// A rift's is <c>riftLife</c>.</summary>
        public double StayOverride = -1;

        SolitonNetwork(List<SolitonTunnelMath.Channel> channels, SolitonTunnelMath.D3 up, ISolitonTunnelMaker maker,
            SolitonTunnelConfigSO config, Color tint, int seq, double formSeconds)
        {
            Channels = channels;
            Up = up;
            Maker = maker;
            Config = config;
            Tint = tint;
            Seq = seq;
            Life = new SolitonTunnelMath.Life[channels.Count];
            AliveFn = Alive;
            for (int i = 0; i < channels.Count; i++) Life[i] = SolitonTunnelMath.NewLife(channels[i], formSeconds);
        }

        /// <summary>
        /// The page's <c>rootsHere</c> + <c>labCommit</c>, round 26 (Yash, 2026-10-28: "it will always be large, despite the
        /// last wormhole's length ... if it has full mass, then it will be the largest, covering the entire cell"): the network
        /// grown from <paramref name="seed"/>, its reach <paramref name="share"/> × its cell's radius (the maker's Mass,
        /// <c>MeerkatSolitonConfigSO.NetworkShare</c>, read by the owner and published, so every peer grows the same one),
        /// centred that share of the way from <paramref name="a"/> to the cell's centre (<see cref="ResolveCell"/>), laid in
        /// the hull's frame.
        /// </summary>
        public static SolitonNetwork Grow(SolitonTunnelConfigSO cfg, ISolitonTunnelMaker maker, int seq,
            uint seed, float share, Vector3 a, Vector3 t0, Vector3 right, Vector3 up)
        {
            ResolveCell(a, out var o, out float cellR);
            return Grow(cfg, maker, seq, seed, share, cellR, o, a, t0, right, up);
        }

        /// <summary>The network in a cell of <paramref name="cellR"/> centred at <paramref name="cellCentre"/> (a placed network
        /// passes its own).</summary>
        public static SolitonNetwork Grow(SolitonTunnelConfigSO cfg, ISolitonTunnelMaker maker, int seq, uint seed, float share,
            float cellR, Vector3 cellCentre, Vector3 a, Vector3 t0, Vector3 right, Vector3 up)
        {
            using var _ = s_mGrow.Auto();
            var o = cellCentre;
            var p = cfg.NetworkLayout();
            SolitonTunnelMath.NetworkSize(p, share, cellR, D(a), D(o), out double rad, out var centre, out double counts);
            var c = centre - D(a);
            var local = new SolitonTunnelMath.D3(c.Dot(D(right)), c.Dot(D(up)), c.Dot(D(t0)));
            var lay = SolitonTunnelMath.BuildNetwork(p, seed, rad, local, counts);
            var channels = SolitonTunnelMath.Build(lay, cfg.NetworkChannel(), D(a), D(t0), D(right), D(up));
            var tint = cfg.Tint(maker != null ? maker.DomainColour : Color.white, maker != null);
            var net = new SolitonNetwork(channels, D(up), maker, cfg, tint, seq, cfg.NetworkFormSeconds);
            s_live.Add(net);
            return net;
        }

        /// <summary>
        /// Round 28, the RIFT (the page's <c>riftLay</c> + <c>riftEnd</c>, Yash 2026-11-03: "Yes, prototype the Rift"): the path
        /// <paramref name="points"/> a hull flew into the void, laid at once as ONE channel with a mouth at each end (its way
        /// in where the run began, its way out where it ended), throat radius <paramref name="b"/> and mouth radius
        /// <paramref name="m"/>, standing <paramref name="staySeconds"/> before it collapses (a sentinel never holds it, round
        /// 28b). Null when the path is too short to lay.
        /// </summary>
        public static SolitonNetwork LayRift(SolitonTunnelConfigSO cfg, ISolitonTunnelMaker maker, int seq,
            IReadOnlyList<Vector3> points, float b, float m, float staySeconds)
        {
            if (points == null || points.Count < 2) return null;
            var d = new SolitonTunnelMath.D3[points.Count];
            for (int i = 0; i < d.Length; i++) d[i] = D(points[i]);
            var path = SolitonTunnelMath.PolyFrom(d);
            if (path == null) return null;
            double bb = System.Math.Max(0.25, b), mm = System.Math.Max(bb, m);
            var channel = new SolitonTunnelMath.Channel
            {
                Index = 0, Path = path, L = path.L, B = bb, M = mm, Za = 0, Zb = 0, D = 1, Seed = seq * 7.31 + 1.3,
                MouthIn = true, MouthOut = true,
            };
            var tint = cfg ? cfg.Tint(maker != null ? maker.DomainColour : Color.white, maker != null) : Color.clear;
            var up = path.N[0];
            var net = new SolitonNetwork(new List<SolitonTunnelMath.Channel> { channel }, up, maker, cfg, tint, seq, 0.05)
            {
                IsRift = true, StayOverride = System.Math.Max(0, staySeconds),
            };
            // it grew behind its maker during the run: whole and open, its rider (the maker) already out
            net.Life[0] = new SolitonTunnelMath.Life { Phase = SolitonTunnelMath.Phase.Stay, S0 = 0, S1 = path.L, Grow = 1, Out = true };
            s_live.Add(net);
            return net;
        }

        /// <summary>A rift a rider can go into: whole, not collapsing, not fading.</summary>
        public bool RiftStanding => IsRift && Life.Length > 0 && Life[0].Phase == SolitonTunnelMath.Phase.Stay && !Life[0].Dying;

        /// <summary>
        /// Round 26: the cell a network fills - the nearest active <see cref="Cell"/>'s centre (the world's origin without one)
        /// and the larger of its own radius (<see cref="Cell.SenseRadius"/>) and, in a race, the race shell
        /// (<see cref="RaceCourseSource.DefaultOuterRadius"/>, 1080 u) and the farthest ring + 60 u; with neither, the race
        /// shell. The page reads the same: Warpline's outer shell, or its farthest ring + 60 u.
        /// </summary>
        public static void ResolveCell(Vector3 near, out Vector3 centre, out float radius)
        {
            var cell = Cell.FindNearestActiveCell(near);
            centre = cell ? cell.transform.position : Vector3.zero;
            radius = cell ? cell.SenseRadius : 0f;
            var field = MeerkatOmnicrystalField.Instance;
            if (field && field.HasCourse)
                radius = CosmicShore.Utility.MathfNoAlloc.Max(radius, RaceCourseSource.DefaultOuterRadius, field.FarthestRingFrom(centre) + 60f);
            if (radius < 100f) radius = RaceCourseSource.DefaultOuterRadius;
        }

        /// <summary>The channel still stands (dug or digging, not collapsing away).</summary>
        public bool Alive(int i) => Life[i].Phase != SolitonTunnelMath.Phase.Gone && !Life[i].Dying;

        /// <summary>A channel's life this frame; false once every channel is gone.</summary>
        public bool Step(double staySeconds, double collapseSeconds, double dt)
        {
            bool any = false;
            for (int i = 0; i < Life.Length; i++)
                if (Life[i].Phase != SolitonTunnelMath.Phase.Gone)
                    any |= SolitonTunnelMath.Step(ref Life[i], Channels[i].L, staySeconds, collapseSeconds, dt);
            return any;
        }

        /// <summary>A channel's life this frame by its own config (a held network never starts its stay; a rift stays its
        /// <see cref="StayOverride"/>).</summary>
        public bool Step(double dt) =>
            Step(Held ? double.PositiveInfinity : StayOverride >= 0 ? StayOverride : Config.StaySeconds, Config.CollapseSeconds, dt);

        /// <summary>Its rider is out: every open channel starts its stay.</summary>
        public void RiderOut()
        {
            for (int i = 0; i < Life.Length; i++) SolitonTunnelMath.RiderOut(ref Life[i]);
        }

        /// <summary>A new network (or a new tunnel) replaces it: every channel collapses now.</summary>
        public void CollapseNow()
        {
            for (int i = 0; i < Life.Length; i++) SolitonTunnelMath.CollapseNow(ref Life[i]);
        }

        /// <summary>Off the list.</summary>
        public void Remove() => s_live.Remove(this);

        public static SolitonTunnelMath.D3 D(Vector3 v) => new(v.x, v.y, v.z);
        public static Vector3 V(SolitonTunnelMath.D3 v) => new((float)v.X, (float)v.Y, (float)v.Z);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ResetOnLoad() => s_live.Clear();
    }
}
