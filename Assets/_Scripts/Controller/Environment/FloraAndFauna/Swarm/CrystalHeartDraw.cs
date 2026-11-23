using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using SVector3 = System.Numerics.Vector3;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The HEARTS of a population drawn as data - a swarm, a builder colony, a substrate (Docs/ECOSYSTEM.md §65.7) -
    /// drawn with the elemental crystal's OWN models and materials, exactly as a living lifeform's heart is. Until
    /// 2026-10-28 these hearts were drawn by the member shader (SwarmMemberInstanced) with a look of its own: every
    /// renderer mesh of the crystal (the Charge crystal's arc overlay included) under one rim-lit pair, so a swarm's
    /// hearts kept a crystal look the elemental crystals had moved on from while every other lifeform's heart wore the
    /// current one (Garrett, 2026-10-28: "a bunch of the swarm fauna are still using outdated versions of the inactive
    /// crystals").
    ///
    /// <para>What it draws, per element: each of the crystal prefab's MODELS (<see cref="Crystal.CrystalModels"/>, the
    /// renderers the Crystal tints - an overlay such as the Charge arcs is the crystal's light, rebuilt every frame by its
    /// own driver, and is not a model), with the materials the prefab authors on it (the inactive look a fresh heart
    /// wears), painted with the living-heart pair wherever the Crystal itself would paint it
    /// (<see cref="Crystal.FindColorPropertyNames"/>), and with the Charge shell's edge crawl held silent the way its
    /// seam walk holds it (<see cref="CrystalChargeArcs.SilenceCrawl(MaterialPropertyBlock)"/>). What it does not draw is
    /// a heart's per-crystal motion (the Mass depth ratchet, the Space spin, the Time flip, the Charge seam walk): those
    /// are drivers on one crystal each, and their materials show the crystal at rest without them, which is how every
    /// crystal shader is written to read undriven.</para>
    ///
    /// <para>Per TICK the owner hands it the published frame and its per-element heart lists (one copy into native
    /// memory). Per FRAME it poses every listed heart in Burst (<see cref="SwarmHeartPoseJob"/>, the proxy chain term for
    /// term) and issues one <c>RenderMeshInstanced</c> per crystal model submesh. The crystal materials must have GPU
    /// instancing on (the asset flag is also what keeps the instancing variant in a build); a part whose material does
    /// not is refused, loudly, at bind.</para>
    /// </summary>
    public sealed class CrystalHeartDraw : System.IDisposable
    {
        sealed class Part
        {
            public Mesh Mesh;
            public int Submesh;
            public Material Material;
            public MaterialPropertyBlock Block;
            public int Local;   // which of its element's model transforms (Matrices slice) it draws over
        }

        sealed class ElementDraw
        {
            public readonly List<Part> Parts = new();
            public NativeArray<float4x4> Locals;
            public NativeArray<float4x4> Matrices;   // Locals.Length x cap, part-major
            public NativeArray<uint> Slots;          // this tick's heart list
            public int Count;
        }

        static readonly int OpacityId = Shader.PropertyToID("_opacity");
        static bool s_warned;

        readonly int _cap, _layer;
        readonly ElementDraw[] _elements = new ElementDraw[4];
        readonly float[] _heartScale = new float[4];
        NativeArray<SwarmInstance> _inst;

        /// <summary>True once at least one element has a crystal model to draw.</summary>
        public bool Valid { get; }

        /// <param name="cap">Member slots: the most hearts of any one element a tick can list.</param>
        /// <param name="elementMask">Bit e set for each research element index (0 Charge, 1 Mass, 2 Space, 3 Time) this
        /// population's hearts can be.</param>
        public CrystalHeartDraw(int cap, int layer, int elementMask)
        {
            _cap = Mathf.Max(1, cap);
            _layer = layer;
            for (int e = 0; e < 4; e++) _heartScale[e] = ElementalCrystalSetSO.CrystalWorldScale;
            var set = ElementalCrystalSetSO.Load();
            if (!set) { Warn($"Resources/{ElementalCrystalSetSO.ResourcePath} is missing"); return; }

            for (int e = 0; e < 4; e++)
            {
                if ((elementMask & (1 << e)) == 0) continue;
                var crystal = set.GetPrefab(SwarmFaunaConfigSO.ToElement(e));
                if (!crystal) { Warn($"the crystal set has no {SwarmFaunaConfigSO.ToElement(e)} crystal"); continue; }
                var draw = Bind(crystal);
                if (draw == null) continue;
                draw.Matrices = new NativeArray<float4x4>(draw.Locals.Length * _cap, Allocator.Persistent);
                draw.Slots = new NativeArray<uint>(_cap, Allocator.Persistent);
                _elements[e] = draw;
                Valid = true;
            }
            if (Valid) _inst = new NativeArray<SwarmInstance>(_cap, Allocator.Persistent);
        }

        ElementDraw Bind(Crystal crystal)
        {
            var root = crystal.transform;
            // the heart's ROOT is posed and sized per member, so each model is taken under a unit-scale root that keeps
            // the prefab's root rotation
            var rootBasis = Matrix4x4.Rotate(root.localRotation);
            var toRoot = root.worldToLocalMatrix;
            var silenced = new HashSet<Renderer>();
            foreach (var arcs in crystal.GetComponentsInChildren<CrystalChargeArcs>(true))
                if (arcs.SilencedRenderer) silenced.Add(arcs.SilencedRenderer);

            var draw = new ElementDraw();
            var locals = new List<float4x4>();
            foreach (var model in crystal.CrystalModels)
            {
                if (model?.model == null || !model.model.TryGetComponent<Renderer>(out var r)) continue;
                Mesh mesh = r is SkinnedMeshRenderer smr ? smr.sharedMesh
                          : r.TryGetComponent<MeshFilter>(out var mf) ? mf.sharedMesh : null;
                var materials = r.sharedMaterials;
                if (!mesh || materials.Length == 0) continue;
                int local = locals.Count;
                locals.Add(rootBasis * (toRoot * r.transform.localToWorldMatrix));
                for (int sm = 0; sm < mesh.subMeshCount; sm++)
                {
                    var mat = materials[Mathf.Min(sm, materials.Length - 1)];
                    if (!mat) continue;
                    if (!mat.enableInstancing)
                    {
                        Warn($"'{mat.name}' (the {crystal.name} heart's {r.name}) has GPU instancing off");
                        continue;
                    }
                    var block = new MaterialPropertyBlock();
                    if (mat.HasProperty(OpacityId)) block.SetFloat(OpacityId, 1f);
                    if (silenced.Contains(r)) CrystalChargeArcs.SilenceCrawl(block);
                    draw.Parts.Add(new Part { Mesh = mesh, Submesh = sm, Material = mat, Block = block, Local = local });
                }
            }
            if (draw.Parts.Count == 0) { Warn($"{crystal.name} has no crystal model to draw"); return null; }
            draw.Locals = new NativeArray<float4x4>(locals.ToArray(), Allocator.Persistent);
            return draw;
        }

        static void Warn(string why)
        {
            if (s_warned) return;
            s_warned = true;
            CSDebug.LogWarning($"[CrystalHeartDraw] {why}: hearts of populations drawn as data (swarms, builder colonies, " +
                               "substrates) will be missing that crystal - Docs/ECOSYSTEM.md §65.7.");
        }

        /// <summary>
        /// Palette, once at bind: the living-heart pair, written wherever the Crystal itself writes its tint (a model whose
        /// material has no pair keeps its own colours, as a living heart does), and each element's root world scale.
        /// </summary>
        public void SetLivingTint(Color dull, Color bright, Vector4 heartScale)
        {
            _heartScale[0] = heartScale.x; _heartScale[1] = heartScale.y; _heartScale[2] = heartScale.z; _heartScale[3] = heartScale.w;
            foreach (var draw in _elements)
            {
                if (draw == null) continue;
                foreach (var part in draw.Parts)
                {
                    var (b, d) = Crystal.FindColorPropertyNames(part.Material);
                    if (b == null) continue;
                    part.Block.SetColor(b, bright);
                    part.Block.SetColor(d, dull);
                }
            }
        }

        /// <summary>
        /// Once per TICK: the published frame's slots [<paramref name="start"/>, start + cap) and each element's heart list
        /// (<paramref name="heartIdx"/>[listStart[e] .. + listCount[e]], slots relative to <paramref name="start"/>). A
        /// molting heart is in two lists; the pose shows it in the one the molt has reached.
        /// </summary>
        public void Upload(SwarmInstance[] instances, int start, uint[] heartIdx, int[] listStart, int[] listCount)
        {
            if (!Valid) return;
            NativeArray<SwarmInstance>.Copy(instances, start, _inst, 0, Mathf.Min(_cap, instances.Length - start));
            for (int e = 0; e < 4; e++)
            {
                var draw = _elements[e];
                if (draw == null) continue;
                draw.Count = Mathf.Clamp(listCount[e], 0, _cap);
                if (draw.Count > 0) NativeArray<uint>.Copy(heartIdx, listStart[e], draw.Slots, 0, draw.Count);
            }
        }

        /// <summary>Once per TICK, for a population whose hearts are all one element (a substrate).</summary>
        public void Upload(SwarmInstance[] instances, int start, int element, uint[] heartIdx, int listStart, int count)
        {
            if (!Valid || element < 0 || element > 3) return;
            NativeArray<SwarmInstance>.Copy(instances, start, _inst, 0, Mathf.Min(_cap, instances.Length - start));
            for (int e = 0; e < 4; e++)
                if (_elements[e] != null) _elements[e].Count = 0;
            var draw = _elements[element];
            if (draw == null) return;
            draw.Count = Mathf.Clamp(count, 0, _cap);
            if (draw.Count > 0) NativeArray<uint>.Copy(heartIdx, listStart, draw.Slots, 0, draw.Count);
        }

        /// <summary>A member died between uploads: its heart goes NOW (the owner drops its crystal through the proxy).</summary>
        public void Hide(int slot)
        {
            if (!Valid || slot < 0 || slot >= _cap) return;
            var s = _inst[slot];
            s.Flags = 0u;
            _inst[slot] = s;
        }

        /// <summary>Once per FRAME: pose every listed heart and draw each crystal model once.</summary>
        public void Draw(Bounds bounds, float alpha, float clock, float bloomTicks, Vector3 up, Vector3 upAlt)
        {
            if (!Valid) return;
            for (int e = 0; e < 4; e++)
            {
                var draw = _elements[e];
                if (draw == null || draw.Count <= 0) continue;
                new SwarmHeartPoseJob
                {
                    Instances = _inst, Slots = draw.Slots, Locals = draw.Locals, Matrices = draw.Matrices,
                    Count = draw.Count, Element = e, Alpha = alpha, Clock = clock, BloomTicks = bloomTicks,
                    HeartScale = _heartScale[e], Up = new SVector3(up.x, up.y, up.z), UpAlt = new SVector3(upAlt.x, upAlt.y, upAlt.z),
                }.Run(draw.Count);
                var matrices = draw.Matrices.Reinterpret<Matrix4x4>();
                foreach (var part in draw.Parts)
                {
                    // the material constructor, so the rendering layer mask and probe usage take the pipeline's defaults
                    var rp = new RenderParams(part.Material)
                    {
                        worldBounds = bounds,
                        matProps = part.Block,
                        shadowCastingMode = ShadowCastingMode.Off,
                        receiveShadows = false,
                        layer = _layer,
                    };
                    Graphics.RenderMeshInstanced(rp, part.Mesh, part.Submesh, matrices, draw.Count, part.Local * draw.Count);
                }
            }
        }

        public void Dispose()
        {
            if (_inst.IsCreated) _inst.Dispose();
            foreach (var draw in _elements)
            {
                if (draw == null) continue;
                if (draw.Locals.IsCreated) draw.Locals.Dispose();
                if (draw.Matrices.IsCreated) draw.Matrices.Dispose();
                if (draw.Slots.IsCreated) draw.Slots.Dispose();
            }
        }
    }
}
