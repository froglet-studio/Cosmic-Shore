using System.Collections.Generic;
using CosmicShore.Utility;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// THE Meerkat wormhole tube (Yash, 2026-10-29: "a watery-glassy tunnel, like looking from an underwater aquarium at the
    /// outside world ... tunnels won't have domains ... the networked tunnel a little upgraded look"): every formed tunnel and
    /// every channel of an omnicrystal's network drawn as one premultiplied-alpha skin through <c>MeerkatWaterTube.shader</c>.
    /// <list type="bullet">
    /// <item>a single tunnel: <c>Resources/MeerkatWaterTube.mat</c>, the watery-glass pane (the opaque world behind it, bent
    /// along the wall's normal and rippling, under a faint aqua tint, a Fresnel rim, flow bands and a caustic shimmer);</item>
    /// <item>a network channel: <c>Resources/MeerkatWaterTubeNetwork.mat</c>, the same shader with MEERKAT_TUBE_NETWORK on
    /// (an iridescent rim, energy streaks racing along it, an HDR glow).</item>
    /// </list>
    /// No domain colour: tunnels are universal. The pane's bend reads URP's opaque copy (<c>_CameraOpaqueTexture</c>), which
    /// the project's URP asset leaves off; <see cref="SceneColour"/> switches it on for the game cameras only while a tube
    /// draws (one half-res copy a frame) and tells the shader through <c>_MeerkatTubeSceneOn</c>; without it the tube is a
    /// clear glass skin. It draws while <see cref="MeerkatTubeLook.WaterOn"/>, the default. It reuses the calm wall's mesh
    /// (<see cref="MeerkatTreeMesh.BuildTubeWall(MeerkatWormholeExecutor.Tunnel)"/>) but keeps its own copies, so it never
    /// disturbs <see cref="MeerkatTubeWallView"/>'s cache.
    /// </summary>
    public static class MeerkatWaterTubeView
    {
        public const string MaterialPath = "MeerkatWaterTube";
        public const string NetworkMaterialPath = "MeerkatWaterTubeNetwork";
        public const string NetworkKeyword = "MEERKAT_TUBE_NETWORK";

        /// <summary>Live overrides of the material's look (the lab's sliders); <see cref="Active"/> false keeps the material's.</summary>
        public static class Tuning
        {
            public static bool Active;
            public static float Clarity = 0.03f, Rim = 0.4f, Flow = 0.1f, Caustic = 0.22f, Twist = 0.15f, Refract = 0.04f, Cull;
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
        static Material s_mat, s_netMat;
        static bool s_resolved, s_netMatOwned;

        static readonly int IdAlpha = Shader.PropertyToID("_Alpha"), IdSceneOn = Shader.PropertyToID("_MeerkatTubeSceneOn"),
            IdRefract = Shader.PropertyToID("_Refract"),
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
                SceneColour.Hold(false);
                return;
            }
            using var _ = s_marker.Auto();
            if (!Resolve()) { SceneColour.Hold(false); return; }
            s_used = 0;
            float now = Time.time;
            DrawTunnels(now);
            DrawNetworks(now);
            SceneColour.Hold(s_used > 0);   // the opaque copy only while a tube draws (none: no copy)
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
                Render(skin.Mesh, s_mat, A, t.W.S0 / t.L, t.W.S1 / t.L, t.L, now);
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
                for (int i = 0; i < net.Channels.Count; i++)
                {
                    var w = net.Life[i];
                    if (w.Phase == MeerkatRootMath.Phase.Gone) continue;
                    var c = net.Channels[i];
                    float L = (float)c.L, fade = MeerkatWormholeView.ChannelFade(w);
                    if (fade <= 0.004f || w.S1 - w.S0 < 0.5) continue;
                    Render(meshes[i], s_netMat, fade, (float)(w.S0 / c.L), (float)(w.S1 / c.L), L, now);
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

        static void Render(Mesh mesh, Material mat, float alpha, float f0, float f1, float L, float now)
        {
            if (!mesh || !mat) return;
            if (f0 <= 1e-3f) f0 = -1f;   // a finished end is open, not a front
            if (f1 >= 1f - 1e-3f) f1 = 2f;
            var mpb = Next();
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
                mpb.SetFloat(IdRefract, Tuning.Refract);
                mpb.SetFloat(IdCull, Tuning.Cull);
            }
            Graphics.RenderMesh(new RenderParams(mat) { matProps = mpb, shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false },
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

        /// <summary>
        /// Holds URP's opaque copy (<c>_CameraOpaqueTexture</c>, half resolution per the URP asset's Opaque Downsampling) on
        /// for every base game camera while a tube draws, and puts each camera back as it was when none does. The copy is
        /// one blit after opaques; the shader reads it once per tube pixel. <c>_MeerkatTubeSceneOn</c> turns 1 only from
        /// the frame AFTER the cameras were asked (so the first frame never reads a copy that was not made).
        /// </summary>
        static class SceneColour
        {
            static readonly Dictionary<Camera, CameraOverrideOption> s_patched = new();
            static readonly List<Camera> s_stale = new();
            static Camera[] s_buffer = new Camera[8];
            static bool s_held;
            static int s_since;

            public static void Hold(bool on)
            {
                if (!on)
                {
                    if (!s_held) return;
                    s_held = false;
                    Restore();
                    Shader.SetGlobalFloat(IdSceneOn, 0f);
                    return;
                }
                if (!s_held) { s_held = true; s_since = Time.frameCount; }
                Patch();
                Shader.SetGlobalFloat(IdSceneOn, Time.frameCount > s_since ? 1f : 0f);
            }

            static void Patch()
            {
                if (s_buffer.Length < Camera.allCamerasCount) s_buffer = new Camera[Mathf.NextPowerOfTwo(Camera.allCamerasCount)];
                int n = Camera.GetAllCameras(s_buffer);
                for (int i = 0; i < n; i++)
                {
                    var cam = s_buffer[i];
                    s_buffer[i] = null;
                    if (cam == null || cam.cameraType != CameraType.Game || s_patched.ContainsKey(cam)) continue;
                    var data = cam.GetUniversalAdditionalCameraData();
                    if (data == null || data.renderType != CameraRenderType.Base) continue;
                    s_patched[cam] = data.requiresColorOption;
                    data.requiresColorOption = CameraOverrideOption.On;
                }
                if (s_patched.Count == 0) return;
                foreach (var cam in s_patched.Keys)   // forget destroyed cameras (scene changes)
                    if (cam == null) s_stale.Add(cam);
                foreach (var cam in s_stale) s_patched.Remove(cam);
                s_stale.Clear();
            }

            static void Restore()
            {
                foreach (var kv in s_patched)
                {
                    if (kv.Key == null) continue;
                    var data = kv.Key.GetUniversalAdditionalCameraData();
                    if (data != null) data.requiresColorOption = kv.Value;
                }
                s_patched.Clear();
            }

            public static void ResetOnLoad()
            {
                s_patched.Clear();
                s_held = false;
                Shader.SetGlobalFloat(IdSceneOn, 0f);
            }
        }

        static bool Resolve()
        {
            if (s_resolved) return s_mat;
            s_resolved = true;
            s_mat = Resources.Load<Material>(MaterialPath);
            if (!s_mat)
            {
                CSDebug.LogWarning($"[Meerkat] Resources/{MaterialPath}.mat is missing: the wormhole tubes are not drawn.");
                return false;
            }
            s_netMat = Resources.Load<Material>(NetworkMaterialPath);
            if (!s_netMat)
            {
                // the shipped asset keeps the network variant in builds; this clone is an editor safety net only
                CSDebug.LogWarning($"[Meerkat] Resources/{NetworkMaterialPath}.mat is missing: the network look is cloned at run time.");
                s_netMat = new Material(s_mat) { name = NetworkMaterialPath };
                s_netMat.EnableKeyword(NetworkKeyword);
                s_netMatOwned = true;
            }
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ResetOnLoad()
        {
            s_tunnels.Clear();
            s_nets.Clear();
            s_resolved = false;
            if (s_netMatOwned && s_netMat) Object.Destroy(s_netMat);
            s_mat = s_netMat = null;
            s_netMatOwned = false;
            s_blocks.Clear();
            SceneColour.ResetOnLoad();
        }
    }
}
