using System.Collections.Generic;
using CosmicShore.Utility;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The CHEAP mobile water pipe (Yash, 2026-10-28: "a transparent watery tube, mobile friendly, an illusion of a wormhole"):
    /// every formed tunnel and every channel of an omnicrystal's network drawn as one alpha-blended skin through
    /// <c>Resources/MeerkatWaterTube.mat</c> (<c>MeerkatWaterTube.shader</c>): no screen copy, no grab pass, no ray march.
    /// It draws ONLY while <see cref="MeerkatTubeLook.WaterOn"/> (the Lavalamp lab's buttons); the game's default look is
    /// unchanged. It reuses the calm wall's mesh (<see cref="MeerkatTreeMesh.BuildTubeWall(MeerkatWormholeExecutor.Tunnel)"/>)
    /// but keeps its own copies, so it never disturbs <see cref="MeerkatTubeWallView"/>'s cache.
    /// </summary>
    public static class MeerkatWaterTubeView
    {
        public const string MaterialPath = "MeerkatWaterTube";

        /// <summary>Live overrides of the material's look (the lab's sliders); <see cref="Active"/> false keeps the material's.</summary>
        public static class Tuning
        {
            public static bool Active;
            public static float Clarity = 0.04f, Rim = 0.45f, Flow = 0.12f, Caustic = 0.25f, Twist = 0.15f, Cull;
        }

        static readonly ProfilerMarker s_marker = new("MeerkatWaterTubeView.Draw");

        sealed class Skin
        {
            public Mesh Mesh;
            public float L, B, M;
        }

        static readonly Dictionary<MeerkatWormholeExecutor.Tunnel, Skin> s_tunnels = new();
        static readonly Dictionary<MeerkatRootNetwork, Mesh[]> s_nets = new();
        static readonly List<MeerkatWormholeExecutor.Tunnel> s_goneT = new();
        static readonly List<MeerkatRootNetwork> s_goneN = new();
        static readonly List<MaterialPropertyBlock> s_blocks = new();
        static int s_used;
        static Material s_mat;
        static bool s_resolved;

        static readonly int IdDomain = Shader.PropertyToID("_Domain"), IdAlpha = Shader.PropertyToID("_Alpha"),
            IdF0 = Shader.PropertyToID("_F0"), IdF1 = Shader.PropertyToID("_F1"), IdEdge = Shader.PropertyToID("_Edge"),
            IdL = Shader.PropertyToID("_L"), IdT = Shader.PropertyToID("_T"), IdClarity = Shader.PropertyToID("_Clarity"),
            IdRim = Shader.PropertyToID("_Rim"), IdFlow = Shader.PropertyToID("_Flow"), IdCaustic = Shader.PropertyToID("_Caustic"),
            IdTwist = Shader.PropertyToID("_Twist"), IdCull = Shader.PropertyToID("_Cull");

        /// <summary>Called once a frame by <see cref="MeerkatWormholeView"/>; draws nothing unless the water look is picked.</summary>
        public static void Draw()
        {
            if (!MeerkatTubeLook.WaterOn)
            {
                if (s_tunnels.Count > 0 || s_nets.Count > 0) ReleaseAll();
                return;
            }
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
                if (!cfg || t.L < 1f || MeerkatWormholeView.TunnelWrapped(cfg)) continue;
                float A = t.Fade;
                if (A <= 0.004f || t.W.S1 - t.W.S0 < 0.5f) continue;
                if (!s_tunnels.TryGetValue(t, out var skin)) s_tunnels[t] = skin = new Skin();
                if (!skin.Mesh || skin.L != t.L || skin.B != t.B || skin.M != t.M)
                {
                    if (skin.Mesh) Object.Destroy(skin.Mesh);
                    skin.Mesh = MeerkatTreeMesh.BuildTubeWall(t);
                    skin.Mesh.name = "MeerkatWaterTube";
                    skin.L = t.L; skin.B = t.B; skin.M = t.M;
                }
                Render(skin.Mesh, t.Colour, A, t.W.S0 / t.L, t.W.S1 / t.L, t.L, now);
            }
            if (s_tunnels.Count == 0) return;
            s_goneT.Clear();
            foreach (var kv in s_tunnels)
            {
                bool alive = false;
                for (int i = 0; i < live.Count && !alive; i++) alive = live[i] == kv.Key;
                if (!alive) s_goneT.Add(kv.Key);
            }
            foreach (var t in s_goneT)
            {
                if (s_tunnels[t].Mesh) Object.Destroy(s_tunnels[t].Mesh);
                s_tunnels.Remove(t);
            }
        }

        static void DrawNetworks(float now)
        {
            var nets = MeerkatRootNetwork.Live;
            for (int n = 0; n < nets.Count; n++)
            {
                var net = nets[n];
                var cfg = net.Maker ? net.Maker.Config : null;
                if (!cfg || MeerkatWormholeView.ChannelWrapped(cfg)) continue;
                if (!s_nets.TryGetValue(net, out var meshes) || meshes.Length != net.Channels.Count)
                {
                    if (meshes != null) Destroy(meshes);
                    meshes = new Mesh[net.Channels.Count];
                    for (int i = 0; i < meshes.Length; i++) meshes[i] = MeerkatTreeMesh.BuildTubeWall(net.Channels[i], cfg.RootTip);
                    s_nets[net] = meshes;
                }
                var colour = net.Maker.Colour;
                for (int i = 0; i < net.Channels.Count; i++)
                {
                    var w = net.Life[i];
                    if (w.Phase == MeerkatRootMath.Phase.Gone) continue;
                    var c = net.Channels[i];
                    float L = (float)c.L, fade = MeerkatWormholeView.ChannelFade(w);
                    if (fade <= 0.004f || w.S1 - w.S0 < 0.5) continue;
                    Render(meshes[i], colour, fade, (float)(w.S0 / c.L), (float)(w.S1 / c.L), L, now);
                }
            }
            if (s_nets.Count == 0) return;
            s_goneN.Clear();
            foreach (var kv in s_nets)
            {
                bool alive = false;
                for (int i = 0; i < nets.Count && !alive; i++) alive = nets[i] == kv.Key;
                if (!alive) s_goneN.Add(kv.Key);
            }
            foreach (var net in s_goneN)
            {
                Destroy(s_nets[net]);
                s_nets.Remove(net);
            }
        }

        static void Render(Mesh mesh, Color colour, float alpha, float f0, float f1, float L, float now)
        {
            if (!mesh) return;
            if (f0 <= 1e-3f) f0 = -1f;   // a finished end is open, not a front
            if (f1 >= 1f - 1e-3f) f1 = 2f;
            var mpb = Next();
            mpb.SetColor(IdDomain, colour);
            mpb.SetFloat(IdAlpha, alpha);
            mpb.SetFloat(IdF0, f0);
            mpb.SetFloat(IdF1, f1);
            mpb.SetFloat(IdEdge, Mathf.Max(4f, 0.06f * L) / L);
            mpb.SetFloat(IdL, L);
            mpb.SetFloat(IdT, now);
            if (Tuning.Active)
            {
                mpb.SetFloat(IdClarity, Tuning.Clarity);
                mpb.SetFloat(IdRim, Tuning.Rim);
                mpb.SetFloat(IdFlow, Tuning.Flow);
                mpb.SetFloat(IdCaustic, Tuning.Caustic);
                mpb.SetFloat(IdTwist, Tuning.Twist);
                mpb.SetFloat(IdCull, Tuning.Cull);
            }
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

        static void Destroy(Mesh[] meshes)
        {
            foreach (var m in meshes) if (m) Object.Destroy(m);
        }

        static void ReleaseAll()
        {
            foreach (var kv in s_tunnels) if (kv.Value.Mesh) Object.Destroy(kv.Value.Mesh);
            foreach (var kv in s_nets) Destroy(kv.Value);
            s_tunnels.Clear();
            s_nets.Clear();
        }

        static bool Resolve()
        {
            if (s_resolved) return s_mat;
            s_resolved = true;
            s_mat = Resources.Load<Material>(MaterialPath);
            if (!s_mat)
                CSDebug.LogWarning($"[Meerkat] Resources/{MaterialPath}.mat is missing: the water-pipe look is not drawn.");
            return s_mat;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ResetOnLoad()
        {
            s_tunnels.Clear();
            s_nets.Clear();
            s_resolved = false;
            s_mat = null;
            s_blocks.Clear();
        }
    }
}
