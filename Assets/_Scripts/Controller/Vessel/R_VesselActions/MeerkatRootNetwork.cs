using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One network of ROOTS a Meerkat grew from an omnicrystal (<c>R_VesselActions/MEERKAT.md</c> §7): its channels
    /// (<see cref="MeerkatRootMath.Build"/>), each one's dug window and life (<see cref="MeerkatRootMath.Life"/>), the
    /// network's up, and who grew it. Every peer builds the same one from the published seed and frame
    /// (<see cref="MeerkatWormholeNetworkRelay"/>); <see cref="MeerkatTreeView"/> draws it and owns its meshes.
    /// </summary>
    public sealed class MeerkatRootNetwork
    {
        static readonly List<MeerkatRootNetwork> s_live = new();
        static readonly ProfilerMarker s_mGrow = new("Meerkat.RootNetwork.Grow");

        /// <summary>Every network alive now, everyone's.</summary>
        public static IReadOnlyList<MeerkatRootNetwork> Live => s_live;

        public readonly List<MeerkatRootMath.Channel> Channels;
        public readonly MeerkatRootMath.Life[] Life;
        public readonly MeerkatRootMath.D3 Up;
        public readonly MeerkatWormholeExecutor Maker;
        public readonly int Seq;
        /// <summary><see cref="Alive"/> as a delegate, made once (the flight asks it every frame).</summary>
        public readonly System.Func<int, bool> AliveFn;
        /// <summary>The root skins, built and destroyed by <see cref="MeerkatTreeView"/>.</summary>
        internal Mesh[] Skins;
        /// <summary>Round 26: each channel's calm inner wall, built and destroyed by <see cref="MeerkatTubeWallView"/>.</summary>
        internal Mesh[] Walls;

        MeerkatRootNetwork(List<MeerkatRootMath.Channel> channels, MeerkatRootMath.D3 up, MeerkatWormholeExecutor maker, int seq,
            double formSeconds)
        {
            Channels = channels;
            Up = up;
            Maker = maker;
            Seq = seq;
            Life = new MeerkatRootMath.Life[channels.Count];
            AliveFn = Alive;
            for (int i = 0; i < channels.Count; i++) Life[i] = MeerkatRootMath.NewLife(channels[i], formSeconds);
        }

        /// <summary>
        /// The page's <c>rootsHere</c> + <c>labCommit</c>, round 26 (Yash, 2026-10-28: "it will always be large, despite the
        /// last wormhole's length ... if it has full mass, then it will be the largest, covering the entire cell"): the network
        /// grown from <paramref name="seed"/>, its reach <paramref name="share"/> × its cell's radius (the maker's Mass,
        /// <c>MeerkatWormholeConfigSO.NetworkShare</c>, read by the owner and published, so every peer grows the same one),
        /// centred that share of the way from <paramref name="a"/> to the cell's centre (<see cref="ResolveCell"/>), laid in
        /// the hull's frame.
        /// </summary>
        public static MeerkatRootNetwork Grow(ScriptableObjects.MeerkatWormholeConfigSO cfg, MeerkatWormholeExecutor maker, int seq,
            uint seed, float share, Vector3 a, Vector3 t0, Vector3 right, Vector3 up)
        {
            using var _ = s_mGrow.Auto();
            var p = cfg.RootLayout();
            ResolveCell(a, out var o, out float cellR);
            MeerkatRootMath.NetworkSize(p, share, cellR, D(a), D(o), out double rad, out var centre, out double counts);
            var c = centre - D(a);
            var local = new MeerkatRootMath.D3(c.Dot(D(right)), c.Dot(D(up)), c.Dot(D(t0)));
            var lay = MeerkatRootMath.BuildNetwork(p, seed, rad, local, counts);
            var channels = MeerkatRootMath.Build(lay, cfg.RootChannel(), D(a), D(t0), D(right), D(up));
            var net = new MeerkatRootNetwork(channels, D(up), maker, seq, cfg.LabyrinthFormSeconds);
            s_live.Add(net);
            MeerkatTreeView.PrepareSkins(net, cfg);   // built once here, never inside a draw
            return net;
        }

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
                radius = Mathf.Max(radius, RaceCourseSource.DefaultOuterRadius, field.FarthestRingFrom(centre) + 60f);
            if (radius < 100f) radius = RaceCourseSource.DefaultOuterRadius;
        }

        /// <summary>The channel still stands (dug or digging, not collapsing away).</summary>
        public bool Alive(int i) => Life[i].Phase != MeerkatRootMath.Phase.Gone && !Life[i].Dying;

        /// <summary>A channel's life this frame; false once every channel is gone.</summary>
        public bool Step(double staySeconds, double collapseSeconds, double dt)
        {
            bool any = false;
            for (int i = 0; i < Life.Length; i++)
                if (Life[i].Phase != MeerkatRootMath.Phase.Gone)
                    any |= MeerkatRootMath.Step(ref Life[i], Channels[i].L, staySeconds, collapseSeconds, dt);
            return any;
        }

        /// <summary>Its rider is out: every open channel starts its stay.</summary>
        public void RiderOut()
        {
            for (int i = 0; i < Life.Length; i++) MeerkatRootMath.RiderOut(ref Life[i]);
        }

        /// <summary>A new network (or a new tunnel) replaces it: every channel collapses now.</summary>
        public void CollapseNow()
        {
            for (int i = 0; i < Life.Length; i++) MeerkatRootMath.CollapseNow(ref Life[i]);
        }

        /// <summary>Off the list, its meshes destroyed.</summary>
        public void Remove()
        {
            s_live.Remove(this);
            MeerkatTreeView.Release(this);
            MeerkatTubeWallView.Release(this);
        }

        public static MeerkatRootMath.D3 D(Vector3 v) => new(v.x, v.y, v.z);
        public static Vector3 V(MeerkatRootMath.D3 v) => new((float)v.X, (float)v.Y, (float)v.Z);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ResetOnLoad() => s_live.Clear();
    }
}
