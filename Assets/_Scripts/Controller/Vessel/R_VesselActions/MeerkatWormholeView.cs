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
    /// <para><b>Round 26</b> (Yash, 2026-10-28: "We can't make it a root, we need to make it smoother, like our tube, same
    /// visuals ... just anomaly: tubes of wormholes, and the network of wormholes"): every channel of an omnicrystal's
    /// network is a wormhole tube too, drawn by the same code as a formed tunnel (<see cref="Tube"/>): its bridge in the
    /// Bridge look, its warp tube, its contour field (<see cref="MeerkatWormholeLensPass"/>) and its calm inner wall
    /// (<see cref="MeerkatTubeWallView"/>). A tube still wrapped in the old look (a tunnel in its tree,
    /// <c>trunkVisibility</c>; a channel in its bark, <c>rootBarkVisibility</c>) draws no bridge, as the page's rounds 20-22.</para>
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

        /// <summary>
        /// One wormhole tube's centreline, radius and live window [S0, S1]: a tunnel's (or a reticle's) conic arc
        /// (<see cref="MeerkatWormholeMath"/>), or, round 26, a channel of an omnicrystal's network
        /// (<see cref="MeerkatRootMath"/>, the page's <c>wh.poly</c>). The page's <c>whSurf</c> / <c>whRadius</c> dispatch the
        /// same way, so the bridge, its glass and the warp tube are one code for both.
        /// </summary>
        public readonly struct Tube
        {
            readonly Vector3 _a0, _t0, _side;
            readonly MeerkatWormholeMath.Conic _path;
            readonly float _b0, _m0;
            readonly MeerkatRootMath.Channel _chan;
            readonly double _tip;
            public readonly float L, S0, S1;

            Tube(Vector3 a0, Vector3 t0, Vector3 side, MeerkatWormholeMath.Conic path, float b0, float m0,
                MeerkatRootMath.Channel chan, double tip, float L, float s0, float s1)
            {
                _a0 = a0; _t0 = t0; _side = side; _path = path; _b0 = b0; _m0 = m0; _chan = chan; _tip = tip;
                this.L = L; S0 = s0; S1 = s1;
            }

            /// <summary>A tunnel's (or a reticle's) tube over the window [s0, s1].</summary>
            public static Tube Conic(Vector3 a0, Vector3 t0, Vector3 side, MeerkatWormholeMath.Conic path, float b0, float m0,
                float s0, float s1) => new(a0, t0, side, path, b0, m0, null, 0, path != null ? path.L : 0f, s0, s1);

            /// <summary>A channel of a network over its dug window [s0, s1] (round 26).</summary>
            public static Tube Channel(MeerkatRootMath.Channel c, float s0, float s1, double rootTip) =>
                new(default, default, default, null, 0f, 0f, c, rootTip, c != null ? (float)c.L : 0f, s0, s1);

            public bool Valid => _chan != null || _path != null;

            /// <summary>The page's <c>whRadius</c>: a tunnel's hyperboloid over its live window, a channel's <c>chanRadius</c>.</summary>
            public float Radius(float s) => _chan != null
                ? (float)MeerkatRootMath.Radius(_chan, s, _tip)
                : MeerkatWormholeMath.Radius(_b0, _m0, L, S0, S1, s);

            /// <summary>The page's <c>whSurf</c>: a point on the wall at angle phi, radius r; <paramref name="normal"/> is the wall's outward normal there.</summary>
            public Vector3 Surf(float s, float phi, float r, out Vector3 normal)
            {
                float c = Mathf.Cos(phi), sn = Mathf.Sin(phi);
                if (_chan != null)
                {
                    var pd = MeerkatRootMath.At(_chan.Path, s, out _);
                    MeerkatRootMath.Frame(_chan.Path, s, out var nd, out var bd);
                    Vector3 n = MeerkatRootNetwork.V(nd), bn = MeerkatRootNetwork.V(bd);
                    normal = n * c + bn * sn;
                    return MeerkatRootNetwork.V(pd) + n * (r * c) + bn * (r * sn);
                }
                var p = MeerkatWormholeMath.At(_a0, _t0, _side, _path, s, out _);
                MeerkatWormholeMath.Frame(_t0, _side, _path, s, out var nc, out var bc);
                normal = nc * c + bc * sn;
                return p + nc * (r * c) + bc * (r * sn);
            }
        }

        /// <summary>Round 26: a tunnel in its tree is wrapped in the old look (that look is its wall; no bridge).</summary>
        public static bool TunnelWrapped(ScriptableObjects.MeerkatWormholeConfigSO cfg) => cfg.TrunkVisibility > 0.004f;

        /// <summary>Round 26: a channel in its bark is wrapped in the old look (rounds 17-21); without it, it is a wormhole tube.</summary>
        public static bool ChannelWrapped(ScriptableObjects.MeerkatWormholeConfigSO cfg) => cfg.RootBarkVisibility > 0.004f;

        /// <summary>A channel's fade as it goes (the page's pair <c>dieT</c> fade).</summary>
        public static float ChannelFade(in MeerkatRootMath.Life w) =>
            w.Dying ? Mathf.Max(0f, 1f - (float)(w.DieT / MeerkatRootMath.FadeSeconds)) : 1f;

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

            bool lens = MeerkatTubeLook.LensOn;   // the default watery-glass look needs no warp mesh: skip building it
            var live = MeerkatWormholeExecutor.Live;
            for (int i = 0; i < live.Count; i++)
            {
                var t = live[i];
                var cfg = t.Config;
                if (!cfg) continue;
                var tube = Tube.Conic(t.A, t.T0, t.Side, t.Path, t.B, t.M, t.W.S0, t.W.S1);
                if (cfg.FormedLook == ScriptableObjects.MeerkatFormedLook.Bridge && !TunnelWrapped(cfg))
                    DrawBridge(tube, cfg, t.Fade, t.Colour, eye);
                if (lens) AddWarpTube(tube, t.Fade, cfg);   // only the full-screen lens reads the warp mesh
            }

            // round 26: the network's channels are wormhole tubes, drawn exactly as a formed tunnel
            var nets = MeerkatRootNetwork.Live;
            var inside = MeerkatWormholeExecutor.LocalRootNetwork;
            for (int n = 0; n < nets.Count; n++)
            {
                var net = nets[n];
                var cfg = net.Maker ? net.Maker.Config : null;
                if (!cfg) continue;
                bool wrapped = ChannelWrapped(cfg);
                bool bridge = cfg.FormedLook == ScriptableObjects.MeerkatFormedLook.Bridge && !wrapped;
                bool warp = lens && cfg.WarpStrength > 0f && !(wrapped && inside == net);   // the page: roots in bark are earth from inside
                if (!bridge && !warp) continue;
                var colour = MeerkatTubeLook.NetworkColour;   // universal: no maker's domain
                for (int i = 0; i < net.Channels.Count; i++)
                {
                    ref var w = ref net.Life[i];
                    if (w.Phase == MeerkatRootMath.Phase.Gone) continue;
                    float fade = ChannelFade(w);
                    if (fade <= 0.004f) continue;
                    var tube = Tube.Channel(net.Channels[i], (float)w.S0, (float)w.S1, cfg.RootTip);
                    if (bridge) DrawBridge(tube, cfg, fade, colour, eye);
                    if (warp) AddWarpTube(tube, fade, cfg);
                }
            }

            Upload(ref s_tris, "MeerkatWormholeSpindles", s_tv, s_tc, null, null, s_ti, MeshTopology.Triangles);
            Upload(ref s_glass, "MeerkatWormholeGlass", s_gv, s_gc, s_gn, null, s_gi, MeshTopology.Triangles);
            Upload(ref s_warp, "MeerkatWormholeWarp", s_wv, null, s_wn, s_wuv, s_wi, MeshTopology.Triangles);
            WarpMesh = s_wi.Count > 0 ? s_warp : null;

            MeerkatTreeView.Draw();       // the tree round a tunnel and the roots' bark, while switched on (MEERKAT.md §7)
            MeerkatTubeWallView.Draw();   // round 26: every tube's calm inner wall
            MeerkatWaterTubeView.Draw();  // the default watery-glass tube (MeerkatTubeLook; the lab can A/B it against the lens)

            var additive = new RenderParams(s_additive) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false };
            var glassParams = new RenderParams(s_glassMat) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false };
            if (s_ti.Count > 0) Graphics.RenderMesh(additive, s_tris, 0, Matrix4x4.identity);
            if (s_gi.Count > 0) Graphics.RenderMesh(glassParams, s_glass, 0, Matrix4x4.identity);
        }

        // ------------------------------------------------------------------ the bridge (drawBridge)

        static void DrawBridge(in Tube tube, ScriptableObjects.MeerkatWormholeConfigSO cfg, float fade, Color colour, Vector3 eye)
        {
            float vis = MeerkatWormholeMath.Clamp01(cfg.BridgeVisibility);
            if (vis <= 0.004f) return;
            if (tube.S1 - tube.S0 < 1f) return;
            BridgeFrame(tube, vis * fade, colour, cfg.BridgeWhite, cfg, eye, SpindleNear, SpindleFull);
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
            BridgeFrame(Tube.Conic(r.A, r.T0, r.Side, r.Path, sh.B, sh.M, r.S0, r.S1), r.Alpha, colour, cfg.ReticleWhite,
                        cfg, eye, ReticleNear, ReticleFull, glass: false, nearLight: cfg.ReticleNearLight);
        }

        /// <summary>
        /// The wormhole's one geometry, the page's <c>drawFrame</c> (plus the bridge's glass), over a tube's window at
        /// brightness A: its spindles are <paramref name="baseCol"/> toward white by <paramref name="whiten"/>.
        /// </summary>
        static void BridgeFrame(in Tube tube, float A, Color baseCol, float whiten, ScriptableObjects.MeerkatWormholeConfigSO cfg,
            Vector3 eye, float nearFrom, float nearFull, bool glass = true, float nearLight = 1f)
        {
            if (A <= 0.004f || !tube.Valid) return;
            float L = tube.L, s0 = tube.S0, s1 = tube.S1;
            // round 11: brightness from nearLight x at the way in to full at the far end (1 = even; the reticle's reticleNearLight)
            float near = MeerkatWormholeMath.Clamp01(nearLight);
            var white = Color.white;
            var col = Color.Lerp(baseCol, white, MeerkatWormholeMath.Clamp01(whiten));
            if (glass)
                Fill(tube, A * Mathf.Max(0f, cfg.BridgeGlass), baseCol, Color.Lerp(white, baseCol, 0.4f));
            int nR = Mathf.Max(2, cfg.BridgeRings), nS = Mathf.Max(3, cfg.BridgeSpindlesPerRing), nRib = Mathf.Max(0, cfg.BridgeRibs);
            float th = Mathf.Max(0.05f, cfg.BridgeSpindleThickness), sh = cfg.BridgeShimmer, now = Time.time;
            for (int i = 0; i < nR; i++)
            {
                float s = MeerkatWormholeMath.BridgeRingS(L, i, nR), g = MeerkatWormholeMath.BridgeGrow(L, s0, s1, s);
                if (g <= 0f) continue;
                float rad = tube.Radius(s), br = A * (near + (1f - near) * s / L) * MeerkatWormholeMath.BridgeGlow(s / L, sh, now);
                for (int j = 0; j < nS; j++)
                {
                    var a = tube.Surf(s, j / (float)nS * Mathf.PI * 2f, rad, out _);
                    var b = tube.Surf(s, (j + 1) / (float)nS * Mathf.PI * 2f, rad, out _);
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
                    var a = tube.Surf(s1r, phi, tube.Radius(s1r), out _);
                    var b = tube.Surf(s2r, phi, tube.Radius(s2r), out _);
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

        static void Fill(in Tube tube, float alpha, Color cIn, Color cOut)
        {
            float span = tube.S1 - tube.S0;
            if (span < 0.5f || alpha <= 0.01f) return;
            int start = s_gv.Count;
            for (int r = 0; r < Rings; r++)
            {
                float u = r / (float)(Rings - 1), s = tube.S0 + span * u, rad = tube.Radius(s);
                var c = Color.Lerp(cIn, cOut, u);
                c.a = alpha;
                for (int j = 0; j <= Seg; j++)
                {
                    s_gv.Add(tube.Surf(s, j / (float)Seg * Mathf.PI * 2f, rad, out var n));
                    s_gn.Add(n);
                    s_gc.Add(c);
                }
            }
            Grid(s_gi, start);
        }

        static void AddWarpTube(in Tube tube, float fade, ScriptableObjects.MeerkatWormholeConfigSO cfg)
        {
            float span = tube.S1 - tube.S0;
            if (span < 1f || !cfg || cfg.WarpStrength <= 0f || !tube.Valid) return;
            int start = s_wv.Count;
            for (int r = 0; r < Rings; r++)
            {
                float u = r / (float)(Rings - 1), s = tube.S0 + span * u, rad = tube.Radius(s), sign = Mathf.Cos(Mathf.PI * u) * fade;
                for (int j = 0; j <= Seg; j++)
                {
                    s_wv.Add(tube.Surf(s, j / (float)Seg * Mathf.PI * 2f, rad, out var n));
                    s_wn.Add(n);
                    s_wuv.Add(new Vector2(sign, 0.05f * Mathf.Max(0f, cfg.WarpStrength)));
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
