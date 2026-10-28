using System.Collections.Generic;
using CosmicShore.Utility;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// What the Meerkat's TREE and ROOTS look like (<c>R_VesselActions/MEERKAT.md</c> §7), the Vessel Studio's
    /// <c>drawTrunk</c> (rounds 13-15, 20) and <c>drawRootSkin</c> / <c>rootInMat</c> (rounds 17, 20) from
    /// <c>Docs/Studios/VesselStudio/meerkat.html</c>:
    /// <list type="bullet">
    /// <item><b>The tree</b> round every formed tunnel, for everyone: the trunk, its bore (the tunnel's own wall), its
    /// cut faces and its branches (<see cref="MeerkatTreeMesh.BuildTree"/>), built once when the tunnel forms and drawn
    /// through <c>MeerkatTree.shader</c> in the wall's watery glass, its maker's domain colour at the way in toward white
    /// at the way out. It shows exactly the tunnel's live window, so it ripples out of its centre with the tunnel and
    /// collapses with it; each branch grows over <c>branchGrowSeconds</c> once the tunnel's front reaches its root. The
    /// contour field's water pipe (<see cref="MeerkatWormholeLensPass"/>) is still drawn inside it, as before.</item>
    /// <item><b>The roots</b>, every network's: each root's bark in the same glass, in a root's brown
    /// (<c>rootGlass</c>, <c>rootRim</c>, <c>rootGrain</c>), dug out from the source; and, while the local pilot is inside
    /// a network, that network's inside as a soft, smooth inner tube - a little dense, never black, fading to clear with
    /// distance (<c>MeerkatRootInside.shader</c>, <c>rootOpacity</c>, <c>rootLamp</c>, <c>rootFog</c>). The trunk's bore is
    /// the same smooth sheen (<c>tubeInner</c>), and its bark's lines fade up close, so the ride is calm.</item>
    /// </list>
    /// Meshes are built once (a tree when its tunnel forms, a root's skin when its network grows, <see cref="PrepareSkins"/>)
    /// and drawn each frame with a <see cref="MaterialPropertyBlock"/> through the two materials in <c>Resources/</c>. The
    /// root skins are never built inside <see cref="Draw"/> (perf, 2026-10-29: building every channel's skin in the draw the
    /// first frame a network showed put a burst of mesh building and garbage under <c>MeerkatTreeView.Draw</c> in the
    /// Lavalamp captures).
    /// </summary>
    public static class MeerkatTreeView
    {
        public const string TreeMaterialPath = "MeerkatTree";
        public const string RootInsideMaterialPath = "MeerkatRootInside";

        static readonly ProfilerMarker s_marker = new("MeerkatTreeView.Draw");
        static readonly ProfilerMarker s_mTrees = new("Meerkat.TreeView.Trees");
        static readonly ProfilerMarker s_mBuildTree = new("Meerkat.TreeView.BuildTree");
        static readonly ProfilerMarker s_mRoots = new("Meerkat.TreeView.Roots");
        static readonly ProfilerMarker s_mBuildSkins = new("Meerkat.TreeView.BuildSkins");
        static readonly Color RootIn = new(0.55f, 0.36f, 0.2f), RootOut = new(0.82f, 0.7f, 0.5f);

        sealed class Tree
        {
            public Mesh Mesh;
            public uint Seed;
            public int Key;
            public float Born;
            public readonly List<MeerkatTreeMesh.BranchRoot> Roots = new();
        }

        static readonly Dictionary<MeerkatWormholeExecutor.Tunnel, Tree> s_trees = new();
        static readonly List<MeerkatWormholeExecutor.Tunnel> s_gone = new();
        static readonly float[] s_bg = new float[MeerkatTreeMesh.MaxBranches];
        // one block per draw this frame (a queued draw may read its block after the call returns)
        static readonly List<MaterialPropertyBlock> s_blocks = new();
        static int s_used;
        static Material s_tree, s_inside;
        static bool s_resolved;

        static readonly int IdCIn = Shader.PropertyToID("_CIn"), IdCOut = Shader.PropertyToID("_COut"), IdAlpha = Shader.PropertyToID("_Alpha"),
            IdGlass = Shader.PropertyToID("_Glass"), IdRim = Shader.PropertyToID("_Rim"), IdPower = Shader.PropertyToID("_Power"),
            IdRings = Shader.PropertyToID("_Rings"), IdGrain = Shader.PropertyToID("_Grain"), IdGrainN = Shader.PropertyToID("_GrainN"),
            IdF0 = Shader.PropertyToID("_F0"), IdF1 = Shader.PropertyToID("_F1"), IdEdge = Shader.PropertyToID("_Edge"), IdT = Shader.PropertyToID("_T"),
            IdBg = Shader.PropertyToID("_Bg"), IdLen = Shader.PropertyToID("_Len"), IdEndW = Shader.PropertyToID("_EndW"),
            IdLo0 = Shader.PropertyToID("_Lo0"), IdLo1 = Shader.PropertyToID("_Lo1"), IdOp = Shader.PropertyToID("_Op"),
            IdLamp = Shader.PropertyToID("_Lamp"), IdFall = Shader.PropertyToID("_Fall"), IdFog = Shader.PropertyToID("_Fog"),
            IdHullP = Shader.PropertyToID("_HullP"), IdSeed = Shader.PropertyToID("_Seed"), IdInner = Shader.PropertyToID("_Inner");

        /// <summary>Draws every tree and every root network; called once a frame by <see cref="MeerkatWormholeView"/>.</summary>
        public static void Draw()
        {
            using var _ = s_marker.Auto();
            if (!Resolve()) return;
            s_used = 0;
            float now = Time.time;
            using (s_mTrees.Auto()) DrawTrees(now);
            using (s_mRoots.Auto()) DrawRoots(now);
        }

        // ------------------------------------------------------------------ the tree (drawTrunk)

        static void DrawTrees(float now)
        {
            var live = MeerkatWormholeExecutor.Live;
            for (int i = 0; i < live.Count; i++)
            {
                var t = live[i];
                var cfg = t.Config;
                if (!cfg) continue;
                float vis = Mathf.Clamp01(cfg.TrunkVisibility);
                if (vis <= 0.004f || t.L < 1f) continue;
                var settings = cfg.Tree();
                int key = Key(t, settings);
                if (!s_trees.TryGetValue(t, out var tree))
                {
                    tree = new Tree { Seed = MeerkatTreeMesh.Seed(t.A, t.L), Born = t.Born };
                    s_trees[t] = tree;
                }
                if (!tree.Mesh || tree.Key != key)   // built the frame RT formed it; a slider moved: rebuilt in place
                {
                    using var build = s_mBuildTree.Auto();
                    if (tree.Mesh) Object.Destroy(tree.Mesh);
                    tree.Mesh = MeerkatTreeMesh.BuildTree(t, settings, tree.Seed, tree.Roots);
                    tree.Key = key;
                }
                float f0 = t.W.S0 / t.L, f1 = t.W.S1 / t.L;
                if (f0 <= 1e-3f) f0 = -1f;   // a finished end is a cut face, not a front
                if (f1 >= 1f - 1e-3f) f1 = 2f;
                float A = vis * t.Fade;
                if (A <= 0.004f) continue;
                float edge = Mathf.Max(4f, 0.06f * t.L) / t.L, age = now - tree.Born, grow = Mathf.Max(0.05f, cfg.BranchGrowSeconds);
                for (int b = 0; b < s_bg.Length; b++)
                {
                    if (b >= tree.Roots.Count) { s_bg[b] = 0f; continue; }
                    var r = tree.Roots[b];
                    s_bg[b] = Mathf.Min(MeerkatWormholeMath.Smooth(0f, 1f, (age - r.TReach) / grow),
                                        MeerkatWormholeMath.Smooth(0f, edge, Mathf.Min(r.U - f0, f1 - r.U)));
                }
                var mpb = Next();
                mpb.SetColor(IdCIn, t.Colour);
                mpb.SetColor(IdCOut, Color.Lerp(Color.white, t.Colour, 0.4f));
                mpb.SetFloat(IdAlpha, A);
                mpb.SetFloat(IdGlass, Mathf.Max(0f, cfg.TrunkGlass));
                mpb.SetFloat(IdInner, Mathf.Max(0f, cfg.TubeInner));
                mpb.SetFloat(IdRim, Mathf.Max(0f, cfg.TrunkRim));
                mpb.SetFloat(IdPower, Mathf.Max(0.5f, cfg.TrunkFresnel));
                mpb.SetFloat(IdRings, Mathf.Max(1, cfg.TrunkRings));
                mpb.SetFloat(IdGrain, Mathf.Max(0f, cfg.TrunkGrain));
                mpb.SetFloat(IdGrainN, Mathf.Max(2, cfg.TrunkGrainLines));
                mpb.SetFloat(IdF0, f0);
                mpb.SetFloat(IdF1, f1);
                mpb.SetFloat(IdEdge, edge);
                mpb.SetFloat(IdT, now);
                mpb.SetFloatArray(IdBg, s_bg);
                Graphics.RenderMesh(new RenderParams(s_tree) { matProps = mpb, shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false },
                                    tree.Mesh, 0, Matrix4x4.identity);
            }
            // a tunnel gone: its tree goes with it
            s_gone.Clear();
            foreach (var kv in s_trees)
            {
                bool alive = false;
                for (int i = 0; i < live.Count && !alive; i++) alive = live[i] == kv.Key;
                if (!alive) s_gone.Add(kv.Key);
            }
            foreach (var t in s_gone)
            {
                if (s_trees[t].Mesh) Object.Destroy(s_trees[t].Mesh);
                s_trees.Remove(t);
            }
        }

        static int Key(MeerkatWormholeExecutor.Tunnel t, in MeerkatTreeMesh.TreeSettings p)
        {
            int h = 17;
            Mix(ref h, t.L); Mix(ref h, t.M); Mix(ref h, t.B); Mix(ref h, p.Top); Mix(ref h, p.Base); Mix(ref h, p.Bark); Mix(ref h, p.Ridges);
            Mix(ref h, p.Gnarl); Mix(ref h, p.BranchCount); Mix(ref h, p.BranchLength); Mix(ref h, p.BranchThickness); Mix(ref h, p.BranchUp);
            Mix(ref h, p.BranchZone); Mix(ref h, p.BranchSymmetry); Mix(ref h, p.BranchForks);
            return h;
        }

        static void Mix(ref int h, float f)
        {
            unchecked { h = h * 31 + f.GetHashCode(); }
        }

        // ------------------------------------------------------------------ the roots (drawRootSkin, rootInMat)

        static void DrawRoots(float now)
        {
            var nets = MeerkatRootNetwork.Live;
            var inside = MeerkatWormholeExecutor.LocalRootNetwork;
            var lamp = MeerkatWormholeExecutor.LocalHullPosition;
            for (int n = 0; n < nets.Count; n++)
            {
                var net = nets[n];
                var cfg = net.Maker ? net.Maker.Config : null;
                if (!cfg) continue;
                // round 26: the bark (rootBarkVisibility) and the old inside wall (rootOpacity) ship off - the channels are
                // wormhole tubes (MeerkatWormholeView, MeerkatTubeWallView) - and nothing is built while both are off
                float bark = Mathf.Clamp01(cfg.RootBarkVisibility);
                bool drawInside = inside == net && cfg.RootOpacity > 0.004f;
                if (bark <= 0.004f && !drawInside) continue;
                // built when the network grew, or by its maker's Update the frame a slider turned the skins on (PrepareSkins)
                if (!HasSkins(net)) continue;
                for (int i = 0; i < net.Channels.Count; i++)
                {
                    ref var w = ref net.Life[i];
                    if (w.Phase == MeerkatRootMath.Phase.Gone) continue;
                    var c = net.Channels[i];
                    float L = (float)c.L, fade = w.Dying ? Mathf.Max(0f, 1f - (float)(w.DieT / MeerkatRootMath.FadeSeconds)) : 1f;
                    if (fade <= 0.004f || w.S1 - w.S0 < 1e-3) continue;
                    float f0 = (float)(w.S0 / c.L), f1 = (float)(w.S1 / c.L);
                    if (f0 <= 1e-3f) f0 = -1f;
                    if (f1 >= 1f - 1e-3f) f1 = 2f;
                    float edge = Mathf.Max(4f, 0.06f * L) / L;
                    MaterialPropertyBlock mpb;
                    if (bark > 0.004f)
                    {
                        // the bark: the trunk's own glass, in a root's brown
                        mpb = Next();
                        mpb.SetColor(IdCIn, RootIn);
                        mpb.SetColor(IdCOut, RootOut);
                        mpb.SetFloat(IdAlpha, fade * bark);
                        mpb.SetFloat(IdGlass, Mathf.Max(0f, cfg.RootGlass));
                        mpb.SetFloat(IdRim, Mathf.Max(0f, cfg.RootRim));
                        mpb.SetFloat(IdPower, Mathf.Max(0.5f, cfg.TrunkFresnel));
                        mpb.SetFloat(IdRings, 1f);
                        mpb.SetFloat(IdGrain, Mathf.Max(0f, cfg.RootGrain));
                        mpb.SetFloat(IdGrainN, Mathf.Max(2, Mathf.RoundToInt(cfg.TrunkGrainLines * 0.6f)));
                        mpb.SetFloat(IdF0, f0);
                        mpb.SetFloat(IdF1, f1);
                        mpb.SetFloat(IdEdge, edge);
                        mpb.SetFloat(IdT, now);
                        mpb.SetFloatArray(IdBg, s_bg);
                        var rp = new RenderParams(s_tree) { matProps = mpb, shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false };
                        Graphics.RenderMesh(rp, net.Skins[i], 0, Matrix4x4.identity);
                    }
                    if (!drawInside) continue;
                    // from inside: a soft inner tube, the world outside still showing through, its ends open
                    float b = (float)c.B;
                    mpb = Next();
                    mpb.SetFloat(IdF0, f0);
                    mpb.SetFloat(IdF1, f1);
                    mpb.SetFloat(IdEdge, edge);
                    mpb.SetFloat(IdLen, L);
                    mpb.SetFloat(IdEndW, 0.6f * b);
                    mpb.SetFloat(IdLo0, c.MouthIn ? 0f : 0.85f);
                    mpb.SetFloat(IdLo1, c.MouthOut ? 0f : 0.85f);
                    mpb.SetFloat(IdOp, Mathf.Clamp01(cfg.RootOpacity) * fade);
                    mpb.SetFloat(IdLamp, Mathf.Max(0f, cfg.RootLamp));
                    mpb.SetFloat(IdFall, Mathf.Max(10f, 4f * b));
                    mpb.SetFloat(IdFog, Mathf.Max(5f, cfg.RootFog));
                    mpb.SetVector(IdHullP, lamp);
                    mpb.SetFloat(IdSeed, (float)c.Seed);
                    Graphics.RenderMesh(new RenderParams(s_inside) { matProps = mpb, shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false },
                                        net.Skins[i], 0, Matrix4x4.identity);
                }
            }
        }

        static MaterialPropertyBlock Next()
        {
            if (s_used == s_blocks.Count) s_blocks.Add(new MaterialPropertyBlock());
            var b = s_blocks[s_used++];
            b.Clear();
            return b;
        }

        /// <summary>The root skins draw at all: the bark (<c>rootBarkVisibility</c>) or the inside tube (<c>rootOpacity</c>).</summary>
        public static bool WantsSkins(ScriptableObjects.MeerkatWormholeConfigSO cfg) =>
            cfg && (Mathf.Clamp01(cfg.RootBarkVisibility) > 0.004f || cfg.RootOpacity > 0.004f);

        static bool HasSkins(MeerkatRootNetwork net) => net.Skins != null && net.Skins.Length == net.Channels.Count;

        /// <summary>
        /// Builds <paramref name="net"/>'s root skins once, outside any draw: when it grows (<see cref="MeerkatRootNetwork.Grow"/>)
        /// and, cheaply checked each frame by its maker (<c>MeerkatWormholeExecutor.StepRootNetworks</c>), the first frame the
        /// skins are turned on for a network grown while they were off. Nothing is built while both looks are off, and the
        /// meshes are reused every frame until <see cref="Release"/> (the network is removed).
        /// </summary>
        public static void PrepareSkins(MeerkatRootNetwork net, ScriptableObjects.MeerkatWormholeConfigSO cfg)
        {
            if (net == null || HasSkins(net) || !WantsSkins(cfg)) return;
            using var _ = s_mBuildSkins.Auto();
            Release(net);
            var skins = new Mesh[net.Channels.Count];
            for (int i = 0; i < skins.Length; i++) skins[i] = MeerkatTreeMesh.BuildRootSkin(net.Channels[i], cfg.RootTip);
            net.Skins = skins;
        }

        /// <summary>A network gone: its root skins go with it.</summary>
        public static void Release(MeerkatRootNetwork net)
        {
            if (net.Skins == null) return;
            foreach (var m in net.Skins) if (m) Object.Destroy(m);
            net.Skins = null;
        }

        static bool Resolve()
        {
            if (s_resolved) return s_tree && s_inside;
            s_resolved = true;
            s_tree = Resources.Load<Material>(TreeMaterialPath);
            s_inside = Resources.Load<Material>(RootInsideMaterialPath);
            if (!s_tree || !s_inside)
                CSDebug.LogWarning($"[Meerkat] Resources/{TreeMaterialPath}.mat or {RootInsideMaterialPath}.mat is missing: " +
                                   "the tree round the wormhole and the omnicrystal's roots are not drawn.");
            return s_tree && s_inside;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ResetOnLoad()
        {
            s_trees.Clear();
            s_resolved = false;
            s_tree = s_inside = null;
            s_blocks.Clear();
        }
    }
}
