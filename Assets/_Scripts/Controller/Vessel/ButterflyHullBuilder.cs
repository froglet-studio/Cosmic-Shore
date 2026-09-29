using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Emits the Butterfly's procedural hull (design: <c>R_VesselActions/BUTTERFLY.md</c> §2). The
    /// GEOMETRY lives in <see cref="ButterflyHullForm"/> — a pure static function this component
    /// feeds its serialized proportions into, compiled and RUN offline by
    /// <c>Tools/Build/butterfly_hull_harness</c>. This class owns only what needs the scene:
    /// emitting the parts as meshes on named children, mirroring the core's materials onto them,
    /// and hiding whatever legacy model the prefab carries.
    ///
    /// <para>That split is <see cref="ScarabHullBuilder"/>'s, deliberately: the Scarab is the
    /// fleet's proven procedural hull and a second one should not invent a second shape. The two
    /// are NOT sharing an implementation today — the bake/blend machinery is duplicated, which is
    /// logged as a refactor row rather than acted on inside a new-vessel branch (a resync or a
    /// ship pass is where that extraction belongs, per the /refactor skill's log-and-never-act
    /// rule).</para>
    ///
    /// <para><b>Why procedural rather than an FBX.</b> There is no Butterfly model in the project,
    /// and the three unwired <c>*_shapekey_with_animations</c> rigs that might have stood in carry
    /// element blend shapes that move ONE vertex by ZERO on two of three
    /// (<c>Docs/VESSEL_CONSTRUCTION.md</c> §4) — borrowing one would turn the morph audit green
    /// while the hull morphed by nothing. A generated hull gets REAL element morphs (the four
    /// extremes of its own pure function) and can be authored headlessly. When real art lands it
    /// replaces this component, not the scaffolding.</para>
    ///
    /// <para><b>MATERIAL CONTRACT.</b> <c>ShipHelper.ApplyShipMaterial</c> paints the domain colour
    /// onto slot <b>1</b>, so the wings are submesh 1 and the body submesh 0 — the wings are what a
    /// pilot reads this vessel's domain from at the range it is flown at.</para>
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class ButterflyHullBuilder : MonoBehaviour, IProceduralElementMorphSource, IProceduralHullSource
    {
        [Header("Body (world units, +Z forward)")]
        [Tooltip("Nose-to-tail length of the body spindle. The WINGS extend well past this — the " +
                 "silhouette is the span, not the body.")]
        [SerializeField, Min(0.5f)] float bodyLength = 7.5f;
        [Tooltip("Fattest half-width of the body. A butterfly's body is slender; at more than " +
                 "about an eighth of the span it reads as a moth.")]
        [SerializeField, Min(0.05f)] float bodyRadius = 0.85f;
        [SerializeField, Range(4, 24)] int bodySegments = 10;
        [SerializeField, Range(4, 20)] int bodySides = 8;

        [Header("Wings")]
        [Tooltip("Half-span of the FOREwing, hinge to tip. This is most of the vessel: the " +
                 "Butterfly is flown from far away and the span is what makes it legible there.")]
        [SerializeField, Min(1f)] float span = 11f;
        [Tooltip("Forewing chord at the hinge.")]
        [SerializeField, Min(0.2f)] float chordRoot = 6.2f;
        [Tooltip("Forewing chord at the tip, before the tip flare and the tip close.")]
        [SerializeField, Min(0.05f)] float chordTip = 2.4f;
        [Tooltip("How far BACK the tip sits relative to the hinge. Zero is a straight wing; the " +
                 "rake is what stops it reading as a manta.")]
        [SerializeField, Min(0f)] float sweep = 3.4f;
        [Tooltip("Bow of the membrane out of its own plane. Enough that it catches light as a " +
                 "surface; a flat wing reads as a decal.")]
        [SerializeField, Min(0f)] float camber = 0.85f;
        [Tooltip("Resting V of the forewings, degrees above horizontal. The hindwings take a " +
                 "shallower NEGATIVE dihedral so the two pairs never share a plane.")]
        [SerializeField, Range(0f, 45f)] float dihedral = 14f;
        [Tooltip("Hindwing size as a fraction of the forewing.")]
        [SerializeField, Range(0.2f, 1f)] float hindScale = 0.68f;
        [Tooltip("Extra rearward offset of the hindwing tip, on top of the shared sweep.")]
        [SerializeField, Min(0f)] float hindSweep = 1.1f;
        [Tooltip("Lobes along the trailing edge. 0 leaves a smooth edge, which reads as a fin.")]
        [SerializeField, Range(0, 8)] int scallopCount = 3;
        [Tooltip("Lobe depth as a fraction of the local chord.")]
        [SerializeField, Range(0f, 0.6f)] float scallopDepth = 0.22f;
        [Tooltip("How much the outer third widens before the tip closes. A wing that only " +
                 "narrows from root to tip reads as a blade.")]
        [SerializeField, Range(0f, 1.2f)] float tipFlare = 0.35f;
        [SerializeField, Range(3, 30)] int spanSegments = 14;
        [SerializeField, Range(3, 24)] int chordSegments = 10;

        [Header("Antennae")]
        [Tooltip("Length of each club-tipped antenna. 0 removes them — note this is a FEATURE " +
                 "GATE, so an element extreme must never drive it across zero (the form asserts).")]
        [SerializeField, Min(0f)] float antennaLength = 2.2f;
        [SerializeField, Min(0.005f)] float antennaThickness = 0.07f;

        [Header("Legacy model")]
        [Tooltip("Root of the inherited model whose RENDERERS this component switches off. Never " +
                 "its GameObjects: the vessel's colliders and serialized references live on that " +
                 "subtree, and deactivating it drops the ship out of the collision world.")]
        [SerializeField] Transform legacyModelRoot;

        ButterflyHullForm.MorphSet _morphSet;
        MeshRenderer _sourceRenderer;
        Material _lastDomainMaterial;
        readonly List<Mesh> _partMeshes = new();
        readonly List<Transform> _partTransforms = new();
        readonly List<Vector3> _blendVerts = new();
        readonly List<Vector3> _blendNormals = new();
        readonly List<Material> _materialWatchScratch = new();
        readonly float[] _blendWeights = new float[4];
        readonly float[] _appliedWeights = { -1f, -1f, -1f, -1f };

        /// <summary>The four wing transforms, in emit order (fore L, fore R, hind L, hind R) — what
        /// <see cref="ButterflyAnimation"/> beats. Empty until <see cref="Rebuild"/> has run.</summary>
        public IReadOnlyList<Transform> WingTransforms => _wings;
        readonly List<Transform> _wings = new();

        // Every element genuinely moves this hull (the harness asserts each one's travel), so all
        // four are declared. The audit reads this instead of hunting for blend shapes.
        static readonly Element[] MorphedElements =
            { Element.Charge, Element.Mass, Element.Space, Element.Time };

        public IReadOnlyList<Element> ProceduralMorphElements => MorphedElements;
        public Transform HiddenLegacyModelRoot => legacyModelRoot;

        /// <summary>The proportions as the pure form consumes them. The serialized fields above are
        /// the authored home; this is how they travel into <see cref="ButterflyHullForm.Generate"/>.</summary>
        public ButterflyHullForm.Settings CollectSettings() => new()
        {
            BodyLength = bodyLength,
            BodyRadius = bodyRadius,
            BodySegments = bodySegments,
            BodySides = bodySides,
            Span = span,
            ChordRoot = chordRoot,
            ChordTip = chordTip,
            Sweep = sweep,
            Camber = camber,
            Dihedral = dihedral,
            HindScale = hindScale,
            HindSweep = hindSweep,
            ScallopCount = scallopCount,
            ScallopDepth = scallopDepth,
            TipFlare = tipFlare,
            SpanSegments = spanSegments,
            ChordSegments = chordSegments,
            AntennaLength = antennaLength,
            AntennaThickness = antennaThickness,
        };

        // IProceduralHullSource — what a harvester reading the prefab ASSET sees instead of an
        // empty MeshFilter. Same parts and the same pivot rule as EmitParts, so a toybox mini hull
        // or a codex portrait is this ship at rest rather than a second opinion about it.
        public void BuildPreviewPieces(List<ProceduralHullPiece> into)
        {
            var parts = ButterflyHullForm.Generate(CollectSettings());
            for (int i = 0; i < parts.Count; i++)
            {
                var part = parts[i];
                Vector3 pivotShift = PivotShift(i, part);
                var verts = new Vector3[part.Verts.Count];
                for (int v = 0; v < verts.Length; v++) verts[v] = part.Verts[v] - pivotShift;
                into.Add(new ProceduralHullPiece
                {
                    Name = part.Name,
                    LocalPosition = pivotShift,
                    Vertices = verts,
                    Normals = part.Normals.ToArray(),
                    Uvs = part.Uvs.ToArray(),
                    Submeshes = new[] { part.Body.ToArray(), part.Wing.ToArray() },
                });
            }
        }

        /// <summary>Geometry arrives in hull space; a CHILD part is re-seated on its pivot, while
        /// part 0 (the Core) renders on THIS GameObject, whose transform belongs to the prefab
        /// author, so its mesh stays in hull space outright.</summary>
        static Vector3 PivotShift(int index, ButterflyHullForm.Part part)
            => index == 0 ? Vector3.zero : part.Pivot;

        void Awake()
        {
            _sourceRenderer = GetComponent<MeshRenderer>();
            Rebuild();
        }

        /// <summary>Right-click the component to preview the shape without entering play mode.
        /// Runtime always rebuilds in <see cref="Awake"/>, so a stale preview mesh cannot ship.</summary>
        [ContextMenu("Rebuild Hull")]
        public void Rebuild()
        {
            // Bake the base hull AND the four element extremes in one pass (topology asserted
            // inside — a float that crosses a feature gate throws loudly here rather than
            // corrupting the blend). Four extra Generate calls at build time, nothing per frame.
            _morphSet = ButterflyHullForm.BakeMorphSet(CollectSettings());
            for (int i = 0; i < _appliedWeights.Length; i++) _appliedWeights[i] = -1f;
            EmitParts(_morphSet.BaseParts);
            HideLegacyModel();
        }

        void EmitParts(List<ButterflyHullForm.Part> parts)
        {
            _partMeshes.Clear();
            _partTransforms.Clear();
            _wings.Clear();

            for (int i = 0; i < parts.Count; i++)
            {
                var part = parts[i];
                Vector3 pivotShift = PivotShift(i, part);
                var local = new List<Vector3>(part.Verts.Count);
                for (int v = 0; v < part.Verts.Count; v++) local.Add(part.Verts[v] - pivotShift);

                var mesh = new Mesh { name = "Butterfly_" + part.Name };
                mesh.SetVertices(local);
                mesh.SetNormals(part.Normals);
                mesh.SetUVs(0, part.Uvs);
                mesh.subMeshCount = 2;
                mesh.SetTriangles(part.Body, ButterflyHullForm.BodySubmesh);
                mesh.SetTriangles(part.Wing, ButterflyHullForm.WingSubmesh);

                // Bounds are pinned to the bake's interval union — the box containing EVERY element
                // weight combination — so the per-frame morph writes below never pay a
                // recalculation and can never shrink culling under a blended pose. The interval is
                // in hull space; a child re-seats by its pivot, so subtract it back out.
                Vector3 boundsMin = _morphSet.BoundsMin[i] - pivotShift;
                Vector3 boundsMax = _morphSet.BoundsMax[i] - pivotShift;
                mesh.bounds = new Bounds((boundsMin + boundsMax) * 0.5f, boundsMax - boundsMin);
                _partMeshes.Add(mesh);

                if (i == 0)
                {
                    GetComponent<MeshFilter>().sharedMesh = mesh;   // Core, on our own renderer
                    _partTransforms.Add(transform);
                    continue;
                }

                var child = transform.Find(part.Name);
                if (!child)
                {
                    var go = new GameObject(part.Name) { layer = gameObject.layer };
                    child = go.transform;
                    child.SetParent(transform, false);
                    go.AddComponent<MeshFilter>();
                    go.AddComponent<MeshRenderer>();
                }
                child.localPosition = part.Pivot;
                child.localRotation = Quaternion.identity;
                child.GetComponent<MeshFilter>().sharedMesh = mesh;
                _partTransforms.Add(child);
                _wings.Add(child);
            }

            PropagateMaterials(force: true);
        }

        /// <summary>
        /// Blend the hull to the given element weights (each 0..1, in
        /// <see cref="ButterflyHullForm.MorphElements"/> order: charge, mass, space, time).
        /// Idempotent and cheap when nothing changed; when a weight moved, only parts the morphs
        /// actually touch are rewritten, and bounds are left alone (pinned at emit).
        ///
        /// <para>The wing hinges do NOT move under any element, which is what lets this write
        /// vertices only and leave <c>localPosition</c> entirely to nobody — so the morph and
        /// <see cref="ButterflyAnimation"/>'s wingbeat (which owns <c>localRotation</c>) never
        /// share a channel. Driven from that animation's LateUpdate, whose tweens carry the
        /// fleet's morph feel; this method is deliberately feel-free.</para>
        /// </summary>
        public void ApplyElementMorphWeights(float charge, float mass, float space, float time)
        {
            if (_morphSet == null) return;

            var weights = _blendWeights;
            weights[0] = Mathf.Clamp01(charge);
            weights[1] = Mathf.Clamp01(mass);
            weights[2] = Mathf.Clamp01(space);
            weights[3] = Mathf.Clamp01(time);

            bool changed = false;
            for (int i = 0; i < weights.Length; i++)
                changed |= !Mathf.Approximately(weights[i], _appliedWeights[i]);
            if (!changed) return;
            for (int i = 0; i < weights.Length; i++) _appliedWeights[i] = weights[i];

            for (int p = 0; p < _partMeshes.Count && p < _morphSet.BaseParts.Count; p++)
            {
                if (!_morphSet.PartMorphs[p]) continue;
                var mesh = _partMeshes[p];
                if (!mesh) continue;

                ButterflyHullForm.BlendPart(_morphSet, p, weights, _blendVerts, _blendNormals);

                // Hull-space geometry re-seated into the part's own frame, exactly as at emit.
                Vector3 pivotShift = PivotShift(p, _morphSet.BaseParts[p]);
                if (pivotShift != Vector3.zero)
                    for (int i = 0; i < _blendVerts.Count; i++) _blendVerts[i] -= pivotShift;

                mesh.SetVertices(_blendVerts, 0, _blendVerts.Count,
                                 MeshUpdateFlags.DontRecalculateBounds);
                mesh.SetNormals(_blendNormals, 0, _blendNormals.Count,
                                MeshUpdateFlags.DontRecalculateBounds);
            }
        }

        /// <summary>
        /// Keep the wings wearing the same materials as the core. The fleet paints the DOMAIN
        /// colour onto slot 1 of the object listed in <c>VesselCustomization._shipGeometries</c> —
        /// which is this GameObject — and that list is serialized, so parts created at runtime can
        /// never be in it. Mirroring the core also means they share its material instance rather
        /// than minting one each.
        /// </summary>
        void PropagateMaterials(bool force = false)
        {
            var source = _sourceRenderer ? _sourceRenderer : (_sourceRenderer = GetComponent<MeshRenderer>());
            if (!source) return;

            // Allocation-free watch: this runs every frame on every live Butterfly, and the
            // sharedMaterials array GETTER mints a managed copy per access — the trap the Scarab
            // already paid for. The non-allocating list read feeds the compare; the array is only
            // materialized on the rare force/changed path, where the children need it anyway.
            source.GetSharedMaterials(_materialWatchScratch);
            if (_materialWatchScratch.Count == 0) return;

            Material domain = _materialWatchScratch.Count > 1
                ? _materialWatchScratch[1] : _materialWatchScratch[0];
            if (!force && domain == _lastDomainMaterial) return;
            _lastDomainMaterial = domain;

            var mats = source.sharedMaterials;
            for (int i = 0; i < transform.childCount; i++)
            {
                var r = transform.GetChild(i).GetComponent<MeshRenderer>();
                if (r) r.sharedMaterials = mats;
            }
        }

        // One reference compare per frame. The domain material is swapped by ShipHelper at spawn
        // AND on any later domain change (the domain-changer toy), and neither raises an event
        // this component could bind to, so it is watched rather than pushed.
        void LateUpdate() => PropagateMaterials();

        /// <summary>
        /// Switch off any inherited model's RENDERERS — never its GameObjects. Colliders and
        /// serialized references live on that subtree; deactivating it would silently drop the
        /// ship out of the collision world. Disabled renderers are also excluded from the
        /// occlusion corridor's hull measurement, so the corridor sizes itself to THIS hull.
        /// </summary>
        void HideLegacyModel()
        {
            if (!legacyModelRoot) return;
            var renderers = legacyModelRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                if (renderers[i].transform.IsChildOf(transform)) continue;   // never hide ourselves
                renderers[i].enabled = false;
            }
        }
    }
}
