using System.Collections.Generic;
using CosmicShore.Utility;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The calm inner wall of every Meerkat wormhole tube (<c>R_VesselActions/MEERKAT.md</c> §7a, round 23), the Vessel Studio's
    /// <c>drawBore</c> (<c>Docs/Studios/VesselStudio/meerkat.html</c>): a formed tunnel's and each channel of an omnicrystal's
    /// wormhole network, a smooth sheen with no lines (<c>tubeInner</c>, the Fresnel at <c>trunkFresnel</c>), its maker's
    /// domain colour toward white, showing exactly the tube's live window. It is round 21's bore on its own, because the tree
    /// that carried it ships off (Yash, 2026-10-28: "just anomaly: tubes of wormholes, and the network of wormholes").
    /// A tube still wrapped in the old look (a tunnel in its tree, a channel in its bark) has no wall of its own: that look
    /// draws it (<see cref="MeerkatTreeView"/>). The contour field's water pipe (<see cref="MeerkatWormholeLensPass"/>) bends
    /// space round the same tubes.
    /// A mesh is built once per tube (a tunnel when it forms, a channel when its network grows) and drawn each frame with a
    /// <see cref="MaterialPropertyBlock"/> through <c>Resources/MeerkatTubeWall.mat</c>; with <c>tubeInner</c> 0 nothing is built.
    /// </summary>
    public static class MeerkatTubeWallView
    {
        public const string MaterialPath = "MeerkatTubeWall";

        static readonly ProfilerMarker s_marker = new("MeerkatTubeWallView.Draw");

        sealed class Wall
        {
            public Mesh Mesh;
            public float L, B, M;
        }

        static readonly Dictionary<MeerkatWormholeExecutor.Tunnel, Wall> s_walls = new();
        static readonly List<MeerkatWormholeExecutor.Tunnel> s_gone = new();
        static readonly List<MaterialPropertyBlock> s_blocks = new();
        static int s_used;
        static Material s_mat;
        static bool s_resolved;

        static readonly int IdCIn = Shader.PropertyToID("_CIn"), IdCOut = Shader.PropertyToID("_COut"), IdAlpha = Shader.PropertyToID("_Alpha"),
            IdInner = Shader.PropertyToID("_Inner"), IdPower = Shader.PropertyToID("_Power"), IdF0 = Shader.PropertyToID("_F0"),
            IdF1 = Shader.PropertyToID("_F1"), IdEdge = Shader.PropertyToID("_Edge"), IdT = Shader.PropertyToID("_T");

        /// <summary>Draws every tube's wall; called once a frame by <see cref="MeerkatWormholeView"/>.</summary>
        public static void Draw()
        {
            using var _ = s_marker.Auto();
            if (!Resolve()) return;
            s_used = 0;
            float now = Time.time;
            DrawTunnels(now);
            DrawNetworks(now);
        }

        static void DrawTunnels(float now)
        {
            var live = MeerkatWormholeExecutor.Live;
            for (int i = 0; i < live.Count; i++)
            {
                var t = live[i];
                var cfg = t.Config;
                if (!cfg || t.L < 1f || cfg.TubeInner <= 0f || MeerkatWormholeView.TunnelWrapped(cfg)) continue;
                float A = t.Fade;
                if (A <= 0.004f || t.W.S1 - t.W.S0 < 0.5f) continue;
                if (!s_walls.TryGetValue(t, out var wall)) s_walls[t] = wall = new Wall();
                if (!wall.Mesh || wall.L != t.L || wall.B != t.B || wall.M != t.M)   // built the frame RT formed it
                {
                    if (wall.Mesh) Object.Destroy(wall.Mesh);
                    wall.Mesh = MeerkatTreeMesh.BuildTubeWall(t);
                    wall.L = t.L; wall.B = t.B; wall.M = t.M;
                }
                Render(wall.Mesh, cfg, t.Colour, A, t.W.S0 / t.L, t.W.S1 / t.L, t.L, now);
            }
            // a tunnel gone: its wall goes with it
            if (s_walls.Count == 0) return;
            s_gone.Clear();
            foreach (var kv in s_walls)
            {
                bool alive = false;
                for (int i = 0; i < live.Count && !alive; i++) alive = live[i] == kv.Key;
                if (!alive) s_gone.Add(kv.Key);
            }
            foreach (var t in s_gone)
            {
                if (s_walls[t].Mesh) Object.Destroy(s_walls[t].Mesh);
                s_walls.Remove(t);
            }
        }

        static void DrawNetworks(float now)
        {
            var nets = MeerkatRootNetwork.Live;
            for (int n = 0; n < nets.Count; n++)
            {
                var net = nets[n];
                var cfg = net.Maker ? net.Maker.Config : null;
                if (!cfg || cfg.TubeInner <= 0f || MeerkatWormholeView.ChannelWrapped(cfg)) continue;
                if (net.Walls == null || net.Walls.Length != net.Channels.Count)
                {
                    Release(net);
                    net.Walls = new Mesh[net.Channels.Count];
                    for (int i = 0; i < net.Walls.Length; i++) net.Walls[i] = MeerkatTreeMesh.BuildTubeWall(net.Channels[i], cfg.RootTip);
                }
                var colour = net.Maker.Colour;
                for (int i = 0; i < net.Channels.Count; i++)
                {
                    ref var w = ref net.Life[i];
                    if (w.Phase == MeerkatRootMath.Phase.Gone) continue;
                    var c = net.Channels[i];
                    float L = (float)c.L, fade = MeerkatWormholeView.ChannelFade(w);
                    if (fade <= 0.004f || w.S1 - w.S0 < 0.5) continue;
                    Render(net.Walls[i], cfg, colour, fade, (float)(w.S0 / c.L), (float)(w.S1 / c.L), L, now);
                }
            }
        }

        static void Render(Mesh mesh, ScriptableObjects.MeerkatWormholeConfigSO cfg, Color colour, float alpha, float f0, float f1,
            float L, float now)
        {
            if (f0 <= 1e-3f) f0 = -1f;   // a finished end is open, not a front
            if (f1 >= 1f - 1e-3f) f1 = 2f;
            var mpb = Next();
            mpb.SetColor(IdCIn, colour);
            mpb.SetColor(IdCOut, Color.Lerp(Color.white, colour, 0.4f));
            mpb.SetFloat(IdAlpha, alpha);
            mpb.SetFloat(IdInner, Mathf.Max(0f, cfg.TubeInner));
            mpb.SetFloat(IdPower, Mathf.Max(0.5f, cfg.TrunkFresnel));
            mpb.SetFloat(IdF0, f0);
            mpb.SetFloat(IdF1, f1);
            mpb.SetFloat(IdEdge, Mathf.Max(4f, 0.06f * L) / L);
            mpb.SetFloat(IdT, now);
            Graphics.RenderMesh(new RenderParams(s_mat) { matProps = mpb, shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false },
                                mesh, 0, Matrix4x4.identity);
        }

        static MaterialPropertyBlock Next()
        {
            if (s_used == s_blocks.Count) s_blocks.Add(new MaterialPropertyBlock());
            var b = s_blocks[s_used++];
            b.Clear();
            return b;
        }

        /// <summary>A network gone: its walls go with it.</summary>
        public static void Release(MeerkatRootNetwork net)
        {
            if (net.Walls == null) return;
            foreach (var m in net.Walls) if (m) Object.Destroy(m);
            net.Walls = null;
        }

        static bool Resolve()
        {
            if (s_resolved) return s_mat;
            s_resolved = true;
            s_mat = Resources.Load<Material>(MaterialPath);
            if (!s_mat)
                CSDebug.LogWarning($"[Meerkat] Resources/{MaterialPath}.mat is missing: the wormhole tubes' calm inner wall is not drawn.");
            return s_mat;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ResetOnLoad()
        {
            s_walls.Clear();
            s_resolved = false;
            s_mat = null;
            s_blocks.Clear();
        }
    }
}
