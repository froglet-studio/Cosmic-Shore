using System.Collections.Generic;
using CosmicShore.Utility;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// What a Meerkat's wormhole LOOKS like (<c>R_VesselActions/MEERKAT.md</c>), the Vessel Studio's <c>whDraw</c> /
    /// <c>whFill</c> / <c>drawBridge</c> / <c>updateTubeWarp</c> (<c>Docs/Studios/VesselStudio/meerkat.html</c>) ported:
    /// <list type="bullet">
    /// <item><b>Your reticle</b> (its pilot only; round 9, Yash: "the reticle is the silhouette of the geometry"): the
    /// wormhole's own wireframe - the bridge's rings, spindles, ribs and thickness, in the same places, condensing the same
    /// way - with no glass, at its own brightness, in its pilot's domain colour toward white by <c>reticleWhite</c>. A formed
    /// tunnel has no reticle.</item>
    /// <item><b>The bridge</b> (everyone, every tunnel): rings of spindles round the throat and ribs of spindles along it
    /// over a glass surface (<c>bridgeGlass</c>), the spindles its maker's domain colour toward white by <c>bridgeWhite</c>, condensing in from the centre as it forms
    /// and evaporating as it collapses, a shimmer running way in to way out; spindles near the camera fade.
    /// <c>bridgeVisibility</c> 0 hides it.</item>
    /// <item><b>The bent space</b> round each tube is <see cref="MeerkatWormholeLensPass"/>'s, from the warp tubes built
    /// here (<see cref="WarpMesh"/>) and the contour field.</item>
    /// </list>
    /// Garrett's after (2026-10-27, the Tube Lens Lab): with <c>formedLook</c> ContourField a formed tunnel draws nothing
    /// here and is only the lens pass's bent space.
    /// Everything is three dynamic meshes rebuilt once a frame and drawn as one identity instance each (the
    /// <c>StoatPathfinderWorldDots</c> pattern), with the materials shipped in <c>Resources/</c>. A spindle is a six-sided
    /// bipyramid (the page's tapered lathe), drawn additively.
    /// </summary>
    public static class MeerkatWormholeView
    {
        public const string AdditiveMaterialPath = "MeerkatWormholeAdditive";
        public const string GlassMaterialPath = "MeerkatWormholeGlass";

        // the page's WH_RINGS / WH_SEG (the glass and the warp tube)
        const int Rings = 22, Seg = 24;
        const float SpindleNear = 4f, SpindleFull = 30f;   // BR_CAM_NEAR / BR_CAM_FULL
        const float ReticleNear = 1f, ReticleFull = 8f;    // RET_NEAR / RET_FULL: a reticle spindle fades only right at the camera
        const int SpindleSides = 6;

        static readonly ProfilerMarker s_marker = new("MeerkatWormholeView.Draw");

        static int s_frame = -1;
        static Mesh s_tris, s_glass, s_warp;
        static Material s_additive, s_glassMat;
        static bool s_resolved;

        static readonly List<Vector3> s_tv = new(), s_gv = new(), s_gn = new(), s_wv = new(), s_wn = new();
        static readonly List<Color> s_tc = new(), s_gc = new();
        static readonly List<Vector2> s_wuv = new();
        static readonly List<int> s_ti = new(), s_gi = new(), s_wi = new();

        /// <summary>The warp tubes this frame (every live tunnel's wall, its outward normal, uv.x the in/out sign × fade),
        /// for <see cref="MeerkatWormholeLensPass"/>; null when there is none.</summary>
        public static Mesh WarpMesh { get; private set; }

        /// <summary>Called by every Meerkat's LateUpdate; builds and draws once a frame.</summary>
        public static void DrawOncePerFrame()
        {
            if (s_frame == Time.frameCount) return;
            s_frame = Time.frameCount;
            using var _ = s_marker.Auto();
            if (!Resolve()) return;
            var cam = BlackHoleLens.ViewCamera();
            Vector3 eye = cam ? cam.transform.position : Vector3.zero;

            s_tv.Clear(); s_tc.Clear(); s_ti.Clear();
            s_gv.Clear(); s_gn.Clear(); s_gc.Clear(); s_gi.Clear();
            s_wv.Clear(); s_wn.Clear(); s_wuv.Clear(); s_wi.Clear();

            foreach (var e in MeerkatWormholeExecutor.Executors)
            {
                if (!e || !e.TryGetReticle(out var ret, out var ghost, out bool hasGhost)) continue;
                if (!e.Config) continue;
                var colour = e.Colour;
                if (ret.Alpha > 0f) DrawReticle(ret, e.Config, eye, colour);
                if (hasGhost) DrawReticle(ghost, e.Config, eye, colour);
            }

            var live = MeerkatWormholeExecutor.Live;
            for (int i = 0; i < live.Count; i++)
            {
                var t = live[i];
                var cfg = t.Config;
                if (!cfg) continue;
                if (cfg.FormedLook == ScriptableObjects.MeerkatFormedLook.Bridge) DrawBridge(t, cfg, eye);
                AddWarpTube(t);
            }

            Upload(ref s_tris, "MeerkatWormholeSpindles", s_tv, s_tc, null, null, s_ti, MeshTopology.Triangles);
            Upload(ref s_glass, "MeerkatWormholeGlass", s_gv, s_gc, s_gn, null, s_gi, MeshTopology.Triangles);
            Upload(ref s_warp, "MeerkatWormholeWarp", s_wv, null, s_wn, s_wuv, s_wi, MeshTopology.Triangles);
            WarpMesh = s_wi.Count > 0 ? s_warp : null;

            MeerkatTreeView.Draw();   // the tree round every tunnel, and the roots (MEERKAT.md §7)

            var additive = new RenderParams(s_additive) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false };
            var glassParams = new RenderParams(s_glassMat) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false };
            if (s_ti.Count > 0) Graphics.RenderMesh(additive, s_tris, 0, Matrix4x4.identity);
            if (s_gi.Count > 0) Graphics.RenderMesh(glassParams, s_glass, 0, Matrix4x4.identity);
        }

        // ------------------------------------------------------------------ the bridge (drawBridge)

        static void DrawBridge(MeerkatWormholeExecutor.Tunnel t, ScriptableObjects.MeerkatWormholeConfigSO cfg, Vector3 eye)
        {
            float vis = MeerkatWormholeMath.Clamp01(cfg.BridgeVisibility);
            if (vis <= 0.004f) return;
            if (t.W.S1 - t.W.S0 < 1f) return;
            BridgeFrame(t.A, t.T0, t.Side, t.Path, t.B, t.M, t.W.S0, t.W.S1, vis * t.Fade, t.Colour, cfg.BridgeWhite,
                        cfg, eye, SpindleNear, SpindleFull);
        }

        /// <summary>
        /// Your reticle, the page's round-9 <c>drawReticle</c>: the wormhole's wireframe (<see cref="BridgeFrame"/>, the bridge's
        /// own rings, spindles, ribs and thickness in the same places), bare - no glass - at the reticle's brightness, in its
        /// pilot's domain colour toward white by <c>reticleWhite</c>. Its spindles fade only right at the camera (it rides just
        /// ahead of the nose), dim at its way in and bright at its far end (<c>reticleNearLight</c>, round 11). Garrett's "before field" (2026-10-27) made it the bridge; Yash's round 9 made it one geometry.
        /// </summary>
        static void DrawReticle(in MeerkatWormholeExecutor.Reticle r, ScriptableObjects.MeerkatWormholeConfigSO cfg,
            Vector3 eye, Color colour)
        {
            if (r.S1 - r.S0 < 0.5f || r.Alpha <= 0.01f) return;
            var sh = r.Shape;
            BridgeFrame(r.A, r.T0, r.Side, r.Path, sh.B, sh.M, r.S0, r.S1, r.Alpha, colour, cfg.ReticleWhite,
                        cfg, eye, ReticleNear, ReticleFull, glass: false, nearLight: cfg.ReticleNearLight);
        }

        /// <summary>
        /// The wormhole's one geometry, the page's <c>drawFrame</c> (plus the bridge's glass), over any window [s0, s1] of a
        /// tunnel at brightness A: its spindles are <paramref name="baseCol"/> toward white by <paramref name="whiten"/>.
        /// </summary>
        static void BridgeFrame(Vector3 a0, Vector3 t0, Vector3 side, MeerkatWormholeMath.Conic path, float b0, float m0, float s0, float s1,
            float A, Color baseCol, float whiten, ScriptableObjects.MeerkatWormholeConfigSO cfg, Vector3 eye, float nearFrom,
            float nearFull, bool glass = true, float nearLight = 1f)
        {
            if (A <= 0.004f || path == null) return;
            float L = path.L;
            // round 11: brightness from nearLight x at the way in to full at the far end (1 = even; the reticle's reticleNearLight)
            float near = MeerkatWormholeMath.Clamp01(nearLight);
            var white = Color.white;
            var col = Color.Lerp(baseCol, white, MeerkatWormholeMath.Clamp01(whiten));
            if (glass)
                Fill(a0, t0, side, path, b0, m0, s0, s1, A * Mathf.Max(0f, cfg.BridgeGlass), baseCol, Color.Lerp(white, baseCol, 0.4f));
            int nR = Mathf.Max(2, cfg.BridgeRings), nS = Mathf.Max(3, cfg.BridgeSpindlesPerRing), nRib = Mathf.Max(0, cfg.BridgeRibs);
            float th = Mathf.Max(0.05f, cfg.BridgeSpindleThickness), sh = cfg.BridgeShimmer, now = Time.time;
            for (int i = 0; i < nR; i++)
            {
                float s = MeerkatWormholeMath.BridgeRingS(L, i, nR), g = MeerkatWormholeMath.BridgeGrow(L, s0, s1, s);
                if (g <= 0f) continue;
                float rad = MeerkatWormholeMath.Radius(b0, m0, L, s0, s1, s), br = A * (near + (1f - near) * s / L) * MeerkatWormholeMath.BridgeGlow(s / L, sh, now);
                for (int j = 0; j < nS; j++)
                {
                    var a = Surf(a0, t0, side, path, s, j / (float)nS * Mathf.PI * 2f, rad, out _);
                    var b = Surf(a0, t0, side, path, s, (j + 1) / (float)nS * Mathf.PI * 2f, rad, out _);
                    Grow(ref a, ref b, g);
                    Spindle(a, b, th * g, br, col, eye, nearFrom, nearFull);
                }
            }
            for (int k = 0; k < nRib; k++)
            {
                float phi = (k + 0.5f) / nRib * Mathf.PI * 2f;
                for (int i = 0; i < nR - 1; i++)
                {
                    float s1r = MeerkatWormholeMath.BridgeRingS(L, i, nR), s2r = MeerkatWormholeMath.BridgeRingS(L, i + 1, nR);
                    float g = Mathf.Min(MeerkatWormholeMath.BridgeGrow(L, s0, s1, s1r), MeerkatWormholeMath.BridgeGrow(L, s0, s1, s2r));
                    if (g <= 0f) continue;
                    var a = Surf(a0, t0, side, path, s1r, phi, MeerkatWormholeMath.Radius(b0, m0, L, s0, s1, s1r), out _);
                    var b = Surf(a0, t0, side, path, s2r, phi, MeerkatWormholeMath.Radius(b0, m0, L, s0, s1, s2r), out _);
                    Grow(ref a, ref b, g);
                    float ur = (s1r + s2r) / (2f * L);
                    Spindle(a, b, th * 0.8f * g, A * 0.8f * (near + (1f - near) * ur) * MeerkatWormholeMath.BridgeGlow(ur, sh, now), col, eye,
                            nearFrom, nearFull);
                }
            }
        }

        /// <summary>A spindle grows from its middle (the page's <c>brA.lerp(brM, 1 - g)</c>).</summary>
        static void Grow(ref Vector3 a, ref Vector3 b, float g)
        {
            var m = (a + b) * 0.5f;
            a = Vector3.Lerp(a, m, 1f - g);
            b = Vector3.Lerp(b, m, 1f - g);
        }

        static void Spindle(Vector3 a, Vector3 b, float thickness, float bright, Color col, Vector3 eye,
            float nearFrom = SpindleNear, float nearFull = SpindleFull)
        {
            if (bright <= 0.004f) return;
            var d = b - a;
            float len = d.magnitude;
            if (len < 0.05f) return;
            var mid = (a + b) * 0.5f;
            float near = MeerkatWormholeMath.Smooth(nearFrom, nearFull, Vector3.Distance(mid, eye));
            if (near <= 0.004f) return;
            var c = col * (bright * near);
            c.a = 1f;
            var axis = d / len;
            var side = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            var side2 = Vector3.Cross(axis, side);
            float r = 0.5f * thickness;
            int start = s_tv.Count;
            s_tv.Add(a); s_tc.Add(c);
            s_tv.Add(b); s_tc.Add(c);
            for (int k = 0; k < SpindleSides; k++)
            {
                float ang = k / (float)SpindleSides * Mathf.PI * 2f;
                s_tv.Add(mid + (side * Mathf.Cos(ang) + side2 * Mathf.Sin(ang)) * r);
                s_tc.Add(c);
            }
            for (int k = 0; k < SpindleSides; k++)
            {
                int v0 = start + 2 + k, v1 = start + 2 + (k + 1) % SpindleSides;
                s_ti.Add(start); s_ti.Add(v0); s_ti.Add(v1);
                s_ti.Add(start + 1); s_ti.Add(v1); s_ti.Add(v0);
            }
        }

        // ------------------------------------------------------------------ the glass (whFill) and the warp (updateTubeWarp)

        static void Fill(Vector3 a0, Vector3 t0, Vector3 side, MeerkatWormholeMath.Conic path, float b, float m, float s0, float s1,
            float alpha, Color cIn, Color cOut)
        {
            float span = s1 - s0;
            if (span < 0.5f || alpha <= 0.01f) return;
            int start = s_gv.Count;
            for (int r = 0; r < Rings; r++)
            {
                float u = r / (float)(Rings - 1), s = s0 + span * u, rad = MeerkatWormholeMath.Radius(b, m, path.L, s0, s1, s);
                var c = Color.Lerp(cIn, cOut, u);
                c.a = alpha;
                for (int j = 0; j <= Seg; j++)
                {
                    s_gv.Add(Surf(a0, t0, side, path, s, j / (float)Seg * Mathf.PI * 2f, rad, out var n));
                    s_gn.Add(n);
                    s_gc.Add(c);
                }
            }
            Grid(s_gi, start);
        }

        static void AddWarpTube(MeerkatWormholeExecutor.Tunnel t)
        {
            float span = t.W.S1 - t.W.S0;
            if (span < 1f || !t.Config || t.Config.WarpStrength <= 0f) return;
            int start = s_wv.Count;
            float fade = t.Fade;
            for (int r = 0; r < Rings; r++)
            {
                float u = r / (float)(Rings - 1), s = t.W.S0 + span * u, rad = t.Radius(s), sign = Mathf.Cos(Mathf.PI * u) * fade;
                for (int j = 0; j <= Seg; j++)
                {
                    s_wv.Add(Surf(t.A, t.T0, t.Side, t.Path, s, j / (float)Seg * Mathf.PI * 2f, rad, out var n));
                    s_wn.Add(n);
                    s_wuv.Add(new Vector2(sign, 0.05f * Mathf.Max(0f, t.Config.WarpStrength)));
                }
            }
            Grid(s_wi, start);
        }

        static void Grid(List<int> idx, int start)
        {
            for (int r = 0; r < Rings - 1; r++)
            for (int j = 0; j < Seg; j++)
            {
                int a = start + r * (Seg + 1) + j, b = a + Seg + 1;
                idx.Add(a); idx.Add(b); idx.Add(a + 1);
                idx.Add(a + 1); idx.Add(b); idx.Add(b + 1);
            }
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>The page's <c>whSurf</c>: a point on the wall at angle phi, radius r; <paramref name="normal"/> is the wall's outward normal there.</summary>
        static Vector3 Surf(Vector3 a0, Vector3 t0, Vector3 side, MeerkatWormholeMath.Conic path, float s, float phi, float r, out Vector3 normal)
        {
            var p = MeerkatWormholeMath.At(a0, t0, side, path, s, out _);
            MeerkatWormholeMath.Frame(t0, side, path, s, out var n, out var bn);
            float c = Mathf.Cos(phi), sn = Mathf.Sin(phi);
            normal = n * c + bn * sn;
            return p + n * (r * c) + bn * (r * sn);
        }

        static void Upload(ref Mesh mesh, string name, List<Vector3> v, List<Color> c, List<Vector3> n, List<Vector2> uv,
            List<int> idx, MeshTopology topology)
        {
            if (!mesh)
            {
                mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave, indexFormat = IndexFormat.UInt32 };
                mesh.MarkDynamic();
            }
            mesh.Clear();
            if (idx.Count == 0) return;
            mesh.SetVertices(v);
            if (c != null) mesh.SetColors(c);
            if (n != null) mesh.SetNormals(n);
            if (uv != null) mesh.SetUVs(0, uv);
            mesh.SetIndices(idx, topology, 0, false);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1e6f);
        }

        static bool Resolve()
        {
            if (s_resolved) return s_additive && s_glassMat;
            s_resolved = true;
            s_additive = Resources.Load<Material>(AdditiveMaterialPath);
            s_glassMat = Resources.Load<Material>(GlassMaterialPath);
            if (!s_additive || !s_glassMat)
                CSDebug.LogWarning($"[Meerkat] Resources/{AdditiveMaterialPath}.mat or {GlassMaterialPath}.mat is missing: " +
                                   "the wormhole's reticle and bridge are not drawn.");
            return s_additive && s_glassMat;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ResetOnLoad()
        {
            s_frame = -1;
            s_resolved = false;
            s_additive = s_glassMat = null;
            WarpMesh = null;
        }
    }
}
