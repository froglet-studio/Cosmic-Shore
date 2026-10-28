using System.Collections.Generic;
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
        /// <summary>Round 23: each channel's calm inner wall, built and destroyed by <see cref="MeerkatTubeWallView"/>.</summary>
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
        /// The page's <c>rootsHere</c> + <c>labCommit</c>: the roots grown from <paramref name="seed"/>, sized by the last
        /// trunk (<paramref name="size"/>, 0..1), spreading every way from <paramref name="a"/> in the hull's frame.
        /// </summary>
        public static MeerkatRootNetwork Grow(ScriptableObjects.MeerkatWormholeConfigSO cfg, MeerkatWormholeExecutor maker, int seq,
            uint seed, float size, Vector3 a, Vector3 t0, Vector3 right, Vector3 up)
        {
            var lay = MeerkatRootMath.BuildLayout(cfg.RootLayout(), seed, size, true);
            var channels = MeerkatRootMath.Build(lay, cfg.RootChannel(), D(a), D(t0), D(right), D(up));
            var net = new MeerkatRootNetwork(channels, D(up), maker, seq, cfg.LabyrinthFormSeconds);
            s_live.Add(net);
            return net;
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
